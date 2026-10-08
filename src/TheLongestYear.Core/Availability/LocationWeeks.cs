using System;
using System.Collections.Generic;

namespace TheLongestYear.Core.Availability;

/// <summary>The weeks a place can first be stood in: pacing <paramref name="Week"/>, Hard's
/// <paramref name="Hard"/> and Extreme's <paramref name="Extreme"/> (the columns of
/// <see cref="LocationGating"/>).</summary>
public sealed record PlaceWeek(int Week, int Hard, int Extreme)
{
    /// <summary>The hard week a model built in this mode reads: Extreme (HardAll) its own column,
    /// every other mode Hard's, the same split as <see cref="LocationGating.HardWeekFor"/>.</summary>
    public int HardFor(WeekMode mode) => mode == WeekMode.HardAll ? Extreme : Hard;

    /// <summary>A place's own gate from <see cref="LocationGating"/> (week 1 when it has none).</summary>
    public static PlaceWeek OwnGate(string location)
        => new(LocationGating.WeekFor(location), LocationGating.HardWeekFor(location, WeekMode.HardGates),
            LocationGating.HardWeekFor(location, WeekMode.HardAll));

    public PlaceWeek LaterOf(PlaceWeek other)
        => new(Math.Max(Week, other.Week), Math.Max(Hard, other.Hard), Math.Max(Extreme, other.Extreme));

    public PlaceWeek EarlierOf(PlaceWeek other)
        => new(Math.Min(Week, other.Week), Math.Min(Hard, other.Hard), Math.Min(Extreme, other.Extreme));
}

/// <summary>When each map of the live world can first be reached, read from the maps' own doors
/// (mod-support work, 2026-10-08).
///
/// <see cref="LocationGating"/> only knows the vanilla gates by name (the Desert, the Sewer, the
/// Secret Woods...), so a modded map read as open from day 1 whatever stood in front of it. That
/// is the safe direction only while nothing is placed from such a map; the moment a rule places
/// an item from one (a Stardew Valley Expanded fish caught only in the Highlands), "unknown map,
/// week 1" is the dangerous guess. This walks the map warps and door warps from the farm, one
/// way, the way a player walks them: a map's week is the latest gate on the easiest path to it.
/// A map no door leads to (entered by a script, a cutscene, a mod's own warp code) is not
/// placed at all, unless it carries a known gate of its own (the Sewer is entered through a
/// ladder action, the Desert by bus; LocationGating already dates both). Forbidden maps (Ginger
/// Island and the other excluded markers) are never entered.
///
/// A Data/Locations key "Farm_&lt;type&gt;" is the farm (the game reads the farm's data from
/// the key for its farm type).</summary>
public sealed class LocationWeeks
{
    private const string FarmDataKeyPrefix = "Farm_";

    private readonly IReadOnlyDictionary<string, PlaceWeek> _reached;

    private LocationWeeks(IReadOnlyDictionary<string, PlaceWeek> reached) => _reached = reached;

    /// <summary>Maps the walk reached from the farm, with their weeks. For the log and tests.</summary>
    public IReadOnlyDictionary<string, PlaceWeek> Reached => _reached;

    /// <param name="directedLinks">Every map warp and door warp, From the map it stands in To the
    /// map it leads to.</param>
    /// <param name="isForbidden">Maps the run never enters (ItemPoolBuilder.IsExcludedLocation).</param>
    public static LocationWeeks Build(
        IReadOnlyList<RawLocationLink> directedLinks,
        Func<string, bool> isForbidden,
        string start = ReachabilityGraph.FarmLocation)
    {
        if (directedLinks == null) throw new ArgumentNullException(nameof(directedLinks));
        if (isForbidden == null) throw new ArgumentNullException(nameof(isForbidden));

        var next = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (RawLocationLink link in directedLinks)
        {
            if (link == null || string.IsNullOrEmpty(link.From) || string.IsNullOrEmpty(link.To)) continue;
            if (!next.TryGetValue(link.From, out List<string>? list))
                next[link.From] = list = new List<string>();
            if (!list.Contains(link.To)) list.Add(link.To);
        }

        var best = new Dictionary<string, PlaceWeek>(StringComparer.Ordinal);
        if (string.IsNullOrEmpty(start) || isForbidden(start))
            return new LocationWeeks(best);
        best[start] = PlaceWeek.OwnGate(start);
        var queue = new Queue<string>();
        queue.Enqueue(start);
        // Weeks only ever go down and are bounded below by 1, so relaxing until nothing improves
        // terminates; the maps number in the hundreds.
        while (queue.Count > 0)
        {
            string here = queue.Dequeue();
            if (!next.TryGetValue(here, out List<string>? targets)) continue;
            PlaceWeek hereWeek = best[here];
            foreach (string there in targets)
            {
                if (isForbidden(there)) continue;
                PlaceWeek candidate = hereWeek.LaterOf(PlaceWeek.OwnGate(there));
                if (best.TryGetValue(there, out PlaceWeek? known))
                {
                    PlaceWeek merged = known.EarlierOf(candidate);
                    if (merged == known) continue;
                    candidate = merged;
                }
                best[there] = candidate;
                queue.Enqueue(there);
            }
        }
        return new LocationWeeks(best);
    }

    /// <summary>The weeks for a Data/Locations key or map name, or false when the walk never
    /// reached it and it has no gate of its own: nothing proves when (or whether) a player gets
    /// there, so a rule must not place an item from it.</summary>
    public bool TryGet(string location, out PlaceWeek week)
    {
        week = null!;
        if (string.IsNullOrEmpty(location)) return false;
        string key = location.StartsWith(FarmDataKeyPrefix, StringComparison.Ordinal)
            ? ReachabilityGraph.FarmLocation
            : location;
        if (_reached.TryGetValue(key, out PlaceWeek? reached))
        {
            week = reached;
            return true;
        }
        if (LocationGating.IsGated(key))
        {
            week = PlaceWeek.OwnGate(key);
            return true;
        }
        return false;
    }
}
