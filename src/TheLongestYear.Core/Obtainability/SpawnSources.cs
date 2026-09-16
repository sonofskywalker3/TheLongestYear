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
    private const string NoBaitNote = " (Magic Bait has no source in the model; recorded as island gated)";

    /// <summary>Maps that only exist during a passive festival (BeachNightMarket.cs, Submarine.cs).</summary>
    private static readonly IReadOnlyDictionary<string, string> FestivalOnlyLocations =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Submarine"] = "NightMarket",
            ["BeachNightMarket"] = "NightMarket",
        };

    /// <summary>A Data/Locations fish row with this catch limit is a one-off (the five legendaries,
    /// GameLocation.cs CheckGenericFishRequirements reads CatchLimit against the player's caught list).
    /// One try in the whole year is not a repeatable try, so the row is Chance (ruling 2026-09-16).</summary>
    private const int OnceAYearCatchLimit = 1;

    /// <summary>The five legendaries by id as well: Legend has a second Data/Locations row (Backwoods,
    /// spring, no catch limit on the row) that the catch-limit rule alone would read as dependable.
    /// Legend, Crimsonfish, Angler, Glacierfish, Mutant Carp.</summary>
    private static readonly HashSet<string> LegendaryIds = new(StringComparer.Ordinal)
        { "(O)163", "(O)159", "(O)160", "(O)775", "(O)682" };

    /// <summary>Species a full day at their best spot lands under 2 expected catches (about an 86%
    /// chance of at least one), so a catch is a lottery ticket rather than a dependable route: Octopus
    /// 1.2, Pufferfish 1.5, Sea Jelly 1.1 to 1.4, Cave Jelly 1.9 a day for a level 10 angler with bait
    /// (docs/superpowers/notes/fish-catch-rates-2026-09-04.md; ruling 2026-09-16). Every other species
    /// clears 2.0 on its best day and stays dependable.</summary>
    private static readonly HashSet<string> RareCatchIds = new(StringComparer.Ordinal)
        { "(O)149", "(O)128", "(O)SeaJelly", "(O)CaveJelly" };

    /// <summary>Fishing trash (FishingRod.cs 495; MineShaft.cs 1193-1197): caught when nothing bites.</summary>
    private static readonly string[] TrashIds = { "(O)167", "(O)168", "(O)169", "(O)170", "(O)171", "(O)172" };

    public static bool IsIslandLocation(string location)
        => location.StartsWith(IslandPrefix, StringComparison.Ordinal);

    private const string LocationFishQuery = "LOCATION_FISH";

    /// <summary>Expands every row whose ItemId starts with "LOCATION_FISH &lt;name&gt;" into copies of
    /// &lt;name&gt;'s own rows (recursively, following that location's own delegations too), re-homed to
    /// the delegating location. A row that delegates to a location with no rows of its own contributes
    /// nothing.
    /// <para>The delegating row's own gates still apply: the game checks a Data/Locations fish row's
    /// Season, MinFishingLevel, RequireMagicBait and Condition and only then resolves its ItemId query,
    /// which re-checks the same four on the target location's rows (GameLocation.cs 13764-13775,
    /// ItemQueryResolver.LOCATION_FISH 260). So a copied row carries both rows' gates, and a row the
    /// target marks as not inheritable is skipped entirely (GameLocation.cs 13764).</para></summary>
    public static IReadOnlyList<LocationSpawn> ExpandLocationFish(IReadOnlyList<LocationSpawn> fish)
    {
        var byLocation = fish.GroupBy(r => r.Location).ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);
        // "path" is the chain of locations currently being descended, not everything seen at this
        // location: a location is removed again on the way back up, so a second delegation to the same
        // target still expands while a real cycle (A to B to A) still stops.
        IEnumerable<LocationSpawn> RowsOf(string location, HashSet<string> path)
        {
            if (!path.Add(location) || !byLocation.TryGetValue(location, out List<LocationSpawn>? rows)) yield break;
            foreach (LocationSpawn row in rows)
            {
                string[] parts = row.ItemId.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2 && parts[0] == LocationFishQuery)
                    foreach (LocationSpawn inherited in RowsOf(parts[1], path))
                    {
                        if (!inherited.CanBeInherited) continue;
                        yield return inherited with
                        {
                            Location = location,
                            Season = inherited.Season ?? row.Season,
                            Condition = BothConditions(row.Condition, inherited.Condition),
                            MinFishingLevel = Math.Max(row.MinFishingLevel, inherited.MinFishingLevel),
                            RequireMagicBait = row.RequireMagicBait || inherited.RequireMagicBait,
                        };
                    }
                else
                    yield return row;
            }
            path.Remove(location);
        }
        var result = new List<LocationSpawn>();
        foreach (string location in byLocation.Keys)
            result.AddRange(RowsOf(location, new HashSet<string>(StringComparer.Ordinal)));
        return result;
    }

    /// <summary>Both conditions have to hold. A comma is AND between game-state-query clauses
    /// (GameStateQuery.cs), which is how <see cref="ConditionSeasons.Read"/> reads them too.</summary>
    private static string? BothConditions(string? outer, string? inner)
        => string.IsNullOrWhiteSpace(outer) ? inner
            : string.IsNullOrWhiteSpace(inner) ? outer
            : outer.Trim() + ", " + inner.Trim();

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
        Derived.Input bait = Derived.Of(snapshot, MagicBaitId);

        // A row needing bait can only be fished where the bait itself can be had; route the fish's
        // table through the bait's, and pick up the bait's island and year 2 flags when every bait
        // source needs them. A source that is already luck (a random query, a random alternative) has
        // no dependable half of its own, so it must not borrow the bait's dependable table: that would
        // turn a chance fish dependable, and would erase the chance half whenever the bait itself is
        // fully dependable.
        // With no bait source at all the row is still recorded rather than dropped: Magic Bait is Mr
        // Qi's, so the row is flagged island and Unresolved, and lands on its own days.
        IEnumerable<(string ItemId, ObtainSource Source)> ThroughBait(bool requiresBait, string id, ObtainSource source)
        {
            if (!requiresBait) { yield return (id, source); yield break; }
            if (bait.IsEmpty)
            {
                yield return (id, source with
                {
                    Conditions = source.Conditions with { GingerIsland = true, Unresolved = true },
                    Detail = source.Detail + NoBaitNote,
                });
                yield break;
            }
            foreach (ObtainSource s in bait.Emit(source.Kind, t => t.Then(source.Lands), source.Conditions,
                source.Detail, source.Setup, luck: source.Reliability != Reliability.Dependable))
                yield return (id, s);
        }

        foreach (LocationSpawn row in rows)
        {
            if (Spawn(row, festivals, SourceKind.Fish, $"Fish at {row.Location}") is not ObtainSource baseTemplate)
                continue;
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
                    Reliability = resolved.Chance || row.CatchLimit == OnceAYearCatchLimit || LegendaryIds.Contains(id) || RareCatchIds.Contains(id)
                        ? Reliability.Chance : baseTemplate.Reliability,
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
                SourceKind.Trash, DayTable.Always, Reliability.Dependable, ObtainConditions.None,
                "fishing trash, repeatable (ruling 2026-09-16)"));
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
