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
        IReadOnlyDictionary<string, ObjInfo> objects, IReadOnlyDictionary<string, FestivalDates> festivals)
    {
        foreach (LocationSpawn row in rows)
        {
            if (Spawn(row, festivals, SourceKind.Fish, $"Fish at {row.Location}") is not ObtainSource baseTemplate)
                continue;
            // Resolve first, then look each concrete fish up: a query listing fish must not lose their data.
            QueryResult resolved = ItemQueries.Resolve(row.ItemId, objects);
            if (resolved.ItemIds.Count == 0)
            {
                foreach (var emitted in ItemQueries.Emit(row.ItemId, objects, baseTemplate))
                    yield return emitted;
                continue;
            }
            foreach (string id in resolved.ItemIds)
            {
                fishRows.TryGetValue(id, out FishRow? fish);
                string time = fish == null || string.IsNullOrWhiteSpace(fish.TimeSpans) || fish.TimeSpans.Trim() == AllDayTimeSpans
                    ? "" : $", time {TimeText(fish.TimeSpans)}";
                int level = Math.Max(row.MinFishingLevel, fish?.MinFishingLevel ?? 0);
                ObtainConditions c = baseTemplate.Conditions;
                yield return (id, baseTemplate with
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
                        Requires = row.RequireMagicBait ? c.Requires.Append("item:(O)908 Magic Bait").ToList() : c.Requires,
                    },
                });
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
            if (reading.Weeks.IsEmpty) continue;
            var template = new ObtainSource(
                SourceKind.ArtifactSpot, DayTable.InWeeks(reading.Weeks), Reliability.Chance,
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
            if (reading.Weeks.IsEmpty) continue;
            var template = new ObtainSource(
                SourceKind.GarbageCan, DayTable.InWeeks(reading.Weeks), Reliability.Chance,
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
        WeekMask weeks = reading.Weeks & (row.Season is Season s ? WeekMask.ForSeason(s) : WeekMask.All);
        ObtainConditions conditions = LocationConditions(row.Location, reading);
        if (FestivalOnlyLocations.TryGetValue(row.Location, out string? festivalId)
            && festivals.TryGetValue(festivalId, out FestivalDates? festival))
        {
            weeks &= festival.Weeks;
            conditions = conditions with { FewDays = true };
        }
        if (weeks.IsEmpty) return null;
        Reliability reliability = reading.Chance || row.IsRandom ? Reliability.Chance : Reliability.Dependable;
        return new ObtainSource(kind, DayTable.InWeeks(weeks), reliability, conditions, detail);
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
