using System;
using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core.Lexicon;

namespace TheLongestYear.Core.Obtainability;

/// <summary>The maps and shops a supported mod's layer says are year-2 or Ginger Island content, by
/// the name after the <c>location:</c> / <c>shop:</c> prefix of a route's <see cref="ObtainConditions.Requires"/>.
/// A route reading data from such a map or shop is flagged, so the year-1 fold leaves it out.</summary>
public sealed record YearTwoMarks(IReadOnlyDictionary<string, YearTwoFlag> Maps, IReadOnlyDictionary<string, YearTwoFlag> Shops)
{
    private const string LocationPrefix = "location:";
    private const string ShopPrefix = "shop:";

    public static readonly YearTwoMarks None = new(
        new Dictionary<string, YearTwoFlag>(StringComparer.Ordinal), new Dictionary<string, YearTwoFlag>(StringComparer.Ordinal));

    /// <summary>No year-2 flag at all, so <see cref="Mark"/> changes nothing. Says nothing about
    /// <see cref="MapWeeks"/>, <see cref="MapLinks"/> or <see cref="OpenedEarly"/>.</summary>
    public bool IsEmpty => Maps.Count == 0 && Shops.Count == 0;

    /// <summary>Maps a layer holds to an opening week of year 1 (<see cref="LayerInputs.MapWeeks"/>):
    /// read by the map walk and by <see cref="MapWeekGate"/>.</summary>
    public IReadOnlyDictionary<string, int> MapWeeks { get; init; } = new Dictionary<string, int>(StringComparer.Ordinal);

    /// <summary>Maps this mod gates by name that a supported mod opens sooner, with the week its back
    /// door can first be walked (<see cref="LayerInputs.MapBackDoors"/>, less any another installed mod
    /// closes): the map's own gate is the earlier of the two.</summary>
    public IReadOnlyDictionary<string, int> OpenedEarly { get; init; } = new Dictionary<string, int>(StringComparer.Ordinal);

    /// <summary>Back doors that are open only on a save owning an upgrade (<see cref="MapBackDoor.WhenUpgrade"/>).
    /// They are not in <see cref="OpenedEarly"/> until <see cref="For"/> puts the owned ones there.</summary>
    public IReadOnlyList<MapBackDoor> UpgradeDoors { get; init; } = Array.Empty<MapBackDoor>();

    /// <summary>Per door upgrade, the ids of the mods whose layers name a back door it opens.</summary>
    public IReadOnlyDictionary<string, IReadOnlySet<string>> UpgradeDoorMods { get; init; }
        = new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal);

    /// <summary>The upgrades this save owns that open a back door, in ordinal order: what names the
    /// save's variant (<see cref="AnswerSet.VariantName"/>).</summary>
    public IReadOnlyList<string> DoorUpgrades(Func<string, bool> hasUpgrade)
    {
        if (hasUpgrade is null) throw new ArgumentNullException(nameof(hasUpgrade));
        return UpgradeDoors.Select(d => d.WhenUpgrade!).Distinct(StringComparer.Ordinal).Where(hasUpgrade)
            .OrderBy(u => u, StringComparer.Ordinal).ToList();
    }

    /// <summary>These marks for one save: the back doors of the upgrades it owns are opened.</summary>
    public YearTwoMarks For(Func<string, bool> hasUpgrade)
    {
        IReadOnlyList<string> owned = DoorUpgrades(hasUpgrade);
        if (owned.Count == 0) return this;
        var early = new Dictionary<string, int>(OpenedEarly, StringComparer.Ordinal);
        foreach (MapBackDoor door in UpgradeDoors)
            if (owned.Contains(door.WhenUpgrade!))
                early[door.Map] = early.TryGetValue(door.Map, out int had) ? Math.Min(had, door.Week) : door.Week;
        return this with { OpenedEarly = early };
    }

    /// <summary>Ways between maps a layer adds to the walk (<see cref="LayerInputs.MapLinks"/>).</summary>
    public IReadOnlyList<RawLocationLink> MapLinks { get; init; } = Array.Empty<RawLocationLink>();

    /// <summary>A source whose Requires holds <c>location:&lt;map&gt;</c> (Maps) or <c>shop:&lt;id&gt;</c>
    /// (Shops) gets YearTwo or GingerIsland set (ordinal, whole-string match of the part after the
    /// prefix); any other source returns as is.</summary>
    public ObtainSource Mark(ObtainSource source)
    {
        if (IsEmpty) return source;
        bool yearTwo = source.Conditions.YearTwo, island = source.Conditions.GingerIsland;
        foreach (string req in source.Conditions.Requires)
        {
            YearTwoFlag flag;
            if ((req.StartsWith(LocationPrefix, StringComparison.Ordinal) && Maps.TryGetValue(req.Substring(LocationPrefix.Length), out flag))
                || (req.StartsWith(ShopPrefix, StringComparison.Ordinal) && Shops.TryGetValue(req.Substring(ShopPrefix.Length), out flag)))
            {
                if (flag == YearTwoFlag.GingerIsland) island = true; else yearTwo = true;
            }
        }
        if (yearTwo == source.Conditions.YearTwo && island == source.Conditions.GingerIsland) return source;
        return source with { Conditions = source.Conditions with { YearTwo = yearTwo, GingerIsland = island } };
    }
}
