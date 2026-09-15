using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core.Obtainability;

/// <summary>Sources that come straight out of spawn tables: forage, location fish, crab pots,
/// artifact spots, garbage cans and fishing trash.</summary>
public static class SpawnSources
{
    private const string IslandPrefix = "Island";
    private const string RainyWeather = "rainy";
    private const string AllDayTimeSpans = "600 2600";
    private const string FishingSkill = "Fishing";
    private const string MagicBaitId = "(O)908";

    private static readonly ObtainFilter BaitAnyFilter = ObtainFilter.Any with { IncludeGingerIsland = true };
    private static readonly ObtainFilter BaitDependableFilter = ObtainFilter.DependableOnly with { IncludeGingerIsland = true };

    /// <summary>Maps that only exist during a passive festival (BeachNightMarket.cs, Submarine.cs).</summary>
    private static readonly IReadOnlyDictionary<string, string> FestivalOnlyLocations =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Submarine"] = "NightMarket",
            ["BeachNightMarket"] = "NightMarket",
        };

    /// <summary>Fishing trash (FishingRod.cs 495; MineShaft.cs 1193-1197): caught when nothing bites.</summary>
    private static readonly string[] TrashIds = { "(O)167", "(O)168", "(O)169", "(O)170", "(O)171", "(O)172" };

    public static bool IsIslandLocation(string location)
        => location.StartsWith(IslandPrefix, StringComparison.Ordinal);

    public static IEnumerable<(string ItemId, ObtainSource Source)> Forage(
        IEnumerable<LocationSpawn> rows, IReadOnlyDictionary<string, ObjInfo> objects,
        IReadOnlyDictionary<string, FestivalDates> festivals)
    {
        foreach (LocationSpawn row in rows)
            if (Spawn(row, festivals, SourceKind.Forage, $"Forage at {row.Location}") is ObtainSource template)
                foreach (var emitted in ItemQueries.Emit(row.ItemId, objects, template))
                    yield return emitted;
    }

    public static IEnumerable<(string ItemId, ObtainSource Source)> LocationFish(
        IEnumerable<LocationSpawn> rows, IReadOnlyDictionary<string, FishRow> fishRows,
        IReadOnlyDictionary<string, ObjInfo> objects, IReadOnlyDictionary<string, FestivalDates> festivals,
        ObtainabilityModel snapshot)
    {
        DayTable bait = snapshot.Table(MagicBaitId, BaitAnyFilter);
        DayTable baitDep = snapshot.Table(MagicBaitId, BaitDependableFilter);
        IReadOnlyList<ObtainSource> baitSources = snapshot.Sources(MagicBaitId);
        bool baitIsland = baitSources.Count > 0 && baitSources.All(s => s.Conditions.GingerIsland);

        // A row needing bait can only be fished where the bait itself can be had; route the fish's
        // table through the bait's, and pick up the bait's island flag when every bait source needs it.
        // A source that is already luck (a random query, a random alternative) has no dependable half
        // of its own, so it must not borrow the bait's dependable table: that would turn a chance fish
        // dependable, and would erase the chance half whenever the bait itself is fully dependable.
        IEnumerable<(string ItemId, ObtainSource Source)> ThroughBait(bool requiresBait, string id, ObtainSource source)
        {
            if (!requiresBait) { yield return (id, source); yield break; }
            ObtainConditions conditions = source.Conditions with { GingerIsland = source.Conditions.GingerIsland || baitIsland };
            DayTable dependable = source.Reliability == Reliability.Dependable ? baitDep.Then(source.Lands) : DayTable.None;
            foreach (ObtainSource s in SourcePair.Of(
                source.Kind, dependable, bait.Then(source.Lands), conditions, source.Detail, source.Setup))
                yield return (id, s);
        }

        foreach (LocationSpawn row in rows)
        {
            if (Spawn(row, festivals, SourceKind.Fish, $"Fish at {row.Location}") is not ObtainSource baseTemplate)
                continue;
            if (row.RequireMagicBait && bait.IsEmpty) continue;   // no bait anywhere: the row cannot be fished
            // Resolve first, then look each concrete fish up: a query listing fish must not lose their data.
            QueryResult resolved = ItemQueries.Resolve(row.ItemId, objects);
            if (resolved.ItemIds.Count == 0)
            {
                foreach (var emitted in ItemQueries.Emit(row.ItemId, objects, baseTemplate))
                    foreach (var final in ThroughBait(row.RequireMagicBait, emitted.ItemId, emitted.Source))
                        yield return final;
                continue;
            }
            foreach (string id in resolved.ItemIds)
            {
                fishRows.TryGetValue(id, out FishRow? fish);
                string time = fish == null || string.IsNullOrWhiteSpace(fish.TimeSpans) || fish.TimeSpans.Trim() == AllDayTimeSpans
                    ? "" : $", time {TimeText(fish.TimeSpans)}";
                int level = Math.Max(row.MinFishingLevel, fish?.MinFishingLevel ?? 0);
                ObtainConditions c = baseTemplate.Conditions;
                ObtainSource finalSource = baseTemplate with
                {
                    Reliability = resolved.Chance ? Reliability.Chance : baseTemplate.Reliability,
                    Detail = baseTemplate.Detail + time + (resolved.Note.Length == 0 ? "" : $" ({resolved.Note})"),
                    Conditions = c with
                    {
                        Skill = level > 0 ? FishingSkill : c.Skill,
                        SkillLevel = level,
                        RainOnly = c.RainOnly || string.Equals(fish?.Weather, RainyWeather, StringComparison.OrdinalIgnoreCase),
                        CatchLimit = row.CatchLimit,
                        Unresolved = c.Unresolved || resolved.Unresolved,
                        Requires = row.RequireMagicBait ? c.Requires.Append($"item:{MagicBaitId} Magic Bait").ToList() : c.Requires,
                    },
                };
                foreach (var final in ThroughBait(row.RequireMagicBait, id, finalSource))
                    yield return final;
            }
        }
    }

    public static IEnumerable<(string ItemId, ObtainSource Source)> CrabPot(IEnumerable<FishRow> rows)
    {
        foreach (FishRow row in rows.Where(r => r.IsTrap))
            yield return (row.ItemId, new ObtainSource(
                SourceKind.CrabPot, DayTable.Always, Reliability.Dependable,
                ObtainConditions.None with { Requires = new[] { "crafting:Crab Pot" } },
                "Data/Fish trap row"));
    }

    public static IEnumerable<(string ItemId, ObtainSource Source)> ArtifactSpots(
        IEnumerable<ArtifactSpotRow> rows, IReadOnlyDictionary<string, ObjInfo> objects,
        IReadOnlyDictionary<string, FestivalDates> festivals)
    {
        foreach (ArtifactSpotRow row in rows)
        {
            ConditionReading reading = ConditionSeasons.Read(row.Condition, festivals);
            DayTable table = ConditionSeasons.Availability(reading, WeekMask.All);
            if (table.IsEmpty) continue;
            var template = new ObtainSource(
                SourceKind.ArtifactSpot, table, Reliability.Chance,
                LocationConditions(row.Location, reading),
                $"artifact spot, {row.Location}, chance {row.Chance:0.###}");
            foreach (var emitted in ItemQueries.Emit(row.ItemId, objects, template))
                yield return emitted;
        }
    }

    public static IEnumerable<(string ItemId, ObtainSource Source)> GarbageCans(
        IEnumerable<GarbageRow> rows, IReadOnlyDictionary<string, ObjInfo> objects,
        IReadOnlyDictionary<string, FestivalDates> festivals)
    {
        foreach (GarbageRow row in rows)
        {
            ConditionReading reading = ConditionSeasons.Read(row.Condition, festivals);
            DayTable table = ConditionSeasons.Availability(reading, WeekMask.All);
            if (table.IsEmpty) continue;
            var template = new ObtainSource(
                SourceKind.GarbageCan, table, Reliability.Chance,
                ConditionSeasons.Apply(ObtainConditions.None, reading), $"garbage can {row.CanId}");
            foreach (var emitted in ItemQueries.Emit(row.ItemId, objects, template))
                yield return emitted;
        }
    }

    public static IEnumerable<(string ItemId, ObtainSource Source)> FishingTrash()
    {
        foreach (string id in TrashIds)
            yield return (id, new ObtainSource(
                SourceKind.Trash, DayTable.Always, Reliability.Chance, ObtainConditions.None, "fishing trash"));
    }

    /// <summary>The source every item of a spawn row shares, or null when the row can never spawn in year 1.</summary>
    private static ObtainSource? Spawn(
        LocationSpawn row, IReadOnlyDictionary<string, FestivalDates> festivals, SourceKind kind, string detail)
    {
        ConditionReading reading = ConditionSeasons.Read(row.Condition, festivals);
        WeekMask seasonMask = row.Season is Season s ? WeekMask.ForSeason(s) : WeekMask.All;
        ObtainConditions conditions = LocationConditions(row.Location, reading);
        DayTable table;
        if (FestivalOnlyLocations.TryGetValue(row.Location, out string? festivalId)
            && festivals.TryGetValue(festivalId, out FestivalDates? festival))
        {
            int startDoy = Calendar.DayOfYear((int)festival.Season, festival.StartDay);
            int endDoy = Calendar.DayOfYear((int)festival.Season, festival.EndDay);
            // A true intersection: a day only counts when both the row's own condition and the
            // festival's exact dates agree, so a sparse condition inside the window is not papered
            // over by combining two independently-computed tables afterward.
            table = DayTable.Available(d => d >= startDoy && d <= endDoy && ConditionSeasons.IsAvailableOn(reading, seasonMask, d));
            conditions = conditions with { FewDays = true };
        }
        else
        {
            table = ConditionSeasons.Availability(reading, seasonMask);
        }
        if (table.IsEmpty) return null;
        Reliability reliability = reading.Chance || row.IsRandom ? Reliability.Chance : Reliability.Dependable;
        return new ObtainSource(kind, table, reliability, conditions, detail);
    }

    private static ObtainConditions LocationConditions(string location, ConditionReading reading)
        => ConditionSeasons.Apply(
            ObtainConditions.None with { Requires = new[] { "location:" + location }, GingerIsland = IsIslandLocation(location) },
            reading);

    /// <summary>"600 1200 1800 2000" to "600-1200 1800-2000".</summary>
    private static string TimeText(string spans)
    {
        string[] t = spans.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var pairs = new List<string>();
        for (int i = 0; i + 1 < t.Length; i += 2) pairs.Add($"{t[i]}-{t[i + 1]}");
        return string.Join(" ", pairs);
    }
}
