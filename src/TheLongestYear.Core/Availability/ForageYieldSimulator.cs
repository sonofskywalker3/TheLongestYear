using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core.Availability;

/// <summary>One item's simulated yield: how much of it a player could have gathered by a cutoff
/// day, and which maps it came from.</summary>
public sealed record ForageYieldResult(
    string ItemId, double ExpectedTotal, int SpawningDays, IReadOnlyList<string> Locations);

/// <summary>"If you checked everywhere every day, how many could you actually have?"
///
/// Walks the loop day by day and accumulates the EXPECTED number of each forage item a player
/// would pick up, assuming the most favourable play the game allows: every forage map visited
/// every day it is reachable, and cleared each time.
///
/// The per-day arithmetic mirrors decompile GameLocation.spawnObjects exactly:
///   1. the map rolls <c>Random(MinDailyForageSpawn, MaxDailyForageSpawn + 1)</c> spawn attempts,
///      capped by <c>MaxSpawnedForageAtOnce - numberOfSpawnedObjectsOnMap</c>;
///   2. each attempt picks ONE entry uniformly from the season-filtered candidate list
///      (<c>r.ChooseFrom(possibleForage)</c>);
///   3. that entry then has to pass its own <c>Chance</c> roll (<c>r.NextBool(forage.Chance)</c>).
/// So an item's expected daily yield at a map is
/// <c>attempts * (itsEntries / allEntries) * Chance</c>, summed over its entries.
///
/// Because the player clears every map daily, <c>numberOfSpawnedObjectsOnMap</c> is 0 each
/// morning, so the MaxAtOnce cap only binds on maps whose MaxDaily exceeds it.
///
/// This is deliberately an OPTIMISTIC ceiling, which is what a "maximum realistic ask" has to be
/// measured against. Where the simulator cannot know something it assumes in the player's favour:
/// every spawn attempt finds a free tile (the game retries 11 times, and a cleared map has room),
/// and a Condition string it cannot evaluate is treated as passing. Both inflate rather than
/// deflate, so a requirement clamped against this number is safe in the direction that matters.
///
/// Not modelled, on purpose: fish (a different mechanism entirely - time of day, weather and
/// fishing level), bushes, digging spots, and any non-forage route to the same item (shop, monster
/// drop, artisan). An item obtainable another way can only be MORE available than this says.</summary>
public static class ForageYieldSimulator
{
    /// <summary>Locations whose forage the simulator should never count, because standing there is
    /// not something the loop can assume. Keyed off the same markers LocationGating uses.</summary>
    private const int FirstDay = 1;

    /// <summary>Expected yield of every forage item by <paramref name="lastDayOfYear"/> (1-112).
    /// Items are keyed by qualified id.</summary>
    public static IReadOnlyDictionary<string, ForageYieldResult> SimulateTo(
        int lastDayOfYear,
        IReadOnlyList<RawSpawnEntry> spawns,
        IReadOnlyList<RawLocationForageRate> rates)
    {
        if (spawns == null) throw new ArgumentNullException(nameof(spawns));
        if (rates == null) throw new ArgumentNullException(nameof(rates));
        if (lastDayOfYear is < FirstDay or > Calendar.DaysPerYear)
            throw new ArgumentOutOfRangeException(nameof(lastDayOfYear), lastDayOfYear,
                $"Day must be {FirstDay}-{Calendar.DaysPerYear}.");

        Dictionary<string, RawLocationForageRate> rateByLocation = rates
            .GroupBy(r => r.Location, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

        // Group the spawn rows per map once; the day loop then only re-filters by season.
        var byLocation = spawns
            .GroupBy(s => s.Location ?? "", StringComparer.Ordinal)
            .ToList();

        var totals = new Dictionary<string, double>(StringComparer.Ordinal);
        var days = new Dictionary<string, int>(StringComparer.Ordinal);
        var locations = new Dictionary<string, SortedSet<string>>(StringComparer.Ordinal);

        for (int day = FirstDay; day <= lastDayOfYear; day++)
        {
            Season season = Calendar.SeasonOfDay(day);

            foreach (IGrouping<string, RawSpawnEntry> map in byLocation)
            {
                if (!rateByLocation.TryGetValue(map.Key, out RawLocationForageRate? rate))
                    continue;

                // A map you cannot stand in yet produces nothing you can collect.
                if (day < FirstDayOfWeek(LocationGating.WeekFor(map.Key)))
                    continue;

                List<RawSpawnEntry> candidates = map
                    .Where(s => s.Season == null || s.Season == season)
                    .ToList();
                if (candidates.Count == 0)
                    continue;

                double attempts = ExpectedAttempts(rate);
                if (attempts <= 0)
                    continue;

                foreach (IGrouping<string, RawSpawnEntry> item in candidates.GroupBy(s => s.ItemId, StringComparer.Ordinal))
                {
                    // Each of this item's entries is one face of the uniform ChooseFrom roll, and
                    // each carries its own Chance.
                    double perDay = item.Sum(e => attempts * (1.0 / candidates.Count) * Clamp01(e.Chance));
                    if (perDay <= 0)
                        continue;

                    totals[item.Key] = totals.GetValueOrDefault(item.Key) + perDay;
                    days[item.Key] = days.GetValueOrDefault(item.Key) + 1;
                    if (!locations.TryGetValue(item.Key, out SortedSet<string>? seen))
                        locations[item.Key] = seen = new SortedSet<string>(StringComparer.Ordinal);
                    seen.Add(map.Key);
                }
            }
        }

        return totals.ToDictionary(
            kv => kv.Key,
            kv => new ForageYieldResult(
                kv.Key,
                kv.Value,
                days.GetValueOrDefault(kv.Key),
                locations.TryGetValue(kv.Key, out SortedSet<string>? l) ? l.ToList() : new List<string>()),
            StringComparer.Ordinal);
    }

    /// <summary>Mean of the game's <c>Random(MinDaily, MaxDaily + 1)</c> draw, held to the
    /// MaxAtOnce ceiling the way spawnObjects does on a map the player cleared this morning.</summary>
    private static double ExpectedAttempts(RawLocationForageRate rate)
    {
        int min = Math.Max(0, rate.MinDaily);
        int max = Math.Max(min, rate.MaxDaily);
        double mean = (min + max) / 2.0;
        int ceiling = Math.Max(0, rate.MaxAtOnce);
        return Math.Min(mean, ceiling);
    }

    private static double Clamp01(double chance) => Math.Clamp(chance, 0.0, 1.0);

    /// <summary>First day of year of a 1-based week.</summary>
    private static int FirstDayOfWeek(int week) => ((Math.Max(1, week) - 1) * Calendar.DaysPerWeek) + 1;
}
