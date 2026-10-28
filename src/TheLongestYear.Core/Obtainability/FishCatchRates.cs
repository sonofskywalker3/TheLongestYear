using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace TheLongestYear.Core.Obtainability;

/// <summary>The fishing catch model, the exact form of the game's own fish pick
/// (GameLocation.GetFishFromLocationData) that tools/fish-sim replayed by sampling (Jeff's rulings
/// 2026-09-04 for the ask basis and 2026-09-16 for rare catches): a level-10 angler casting at depth
/// 5 with plain bait, two catches a game hour. For each place (a location and one of its fish areas),
/// season, weather and hour, a cast walks the location's rows (its own plus the inheritable Default
/// rows) in precedence order, ties shuffled; the first row whose gates pass ends the cast, with its
/// item or with trash. A tie group of rows with chances p is shuffled, so row i is first to pass with
/// chance p_i times the integral over t in [0,1] of the product over j != i of (1 - p_j t).
/// Built from the obtainability inputs only, so it runs in the cold build and nowhere else.</summary>
public static class FishCatchRates
{
    public const int AnglerLevel = 10;
    public const int CastDepth = 5;
    public const double CatchesPerHour = 2.0;
    public const double MinWeeklyBasis = 2.0;
    public const double RareBestDay = 2.0;
    private const int DaysPerWeek = 7;
    private const int DayStart = 600;
    private const int DayEnd = 2600;
    private const int HourUnits = 100;
    private const int WindowUnits = 1000;
    private const double MaxGenericChance = 0.9;
    private const double LevelChancePerLevel = 1.0 / 50;
    private const string DefaultLocation = "Default";

    /// <summary>The share of days that rain per season, and the rainy days a season holds (gen_cs.py).</summary>
    private static readonly double[] RainShare = { 0.18, 0.15, 0.18, 0.0 };
    private static readonly int[] RainyDays = { 5, 4, 5, 0 };
    private static readonly string[] SeasonNames = { "spring", "summer", "fall", "winter" };

    /// <summary>The vanilla maps a loop fishes (sim.py LOCS); every map a mod adds is fished too,
    /// except island maps (not year 1) and the other farm types' maps.</summary>
    private static readonly HashSet<string> VanillaFishingMaps = new(StringComparer.Ordinal)
    {
        "Town", "Beach", "Forest", "Mountain", "Sewer", "UndergroundMine", "Farm_Standard", "Desert",
        "Woods", "Railroad", "Backwoods", "BusStop",
    };

    private static readonly HashSet<string> LegendaryIds = new(StringComparer.Ordinal)
        { "(O)163", "(O)159", "(O)160", "(O)775", "(O)682" };

    /// <summary>Every item the model lands at least once, by qualified id.</summary>
    public static IReadOnlyDictionary<string, CatchRate> Build(ObtainabilityInputs inputs)
    {
        if (inputs == null) throw new ArgumentNullException(nameof(inputs));
        IReadOnlyList<LocationSpawn> rows = SpawnSources.ExpandLocationFish(inputs.LocationFish);
        List<CastRow> inherited = Group(rows.Where(r => r.Location == DefaultLocation && r.CanBeInherited), inputs.Objects);
        // perHour[(id, season, rain)][hour index] = best per-cast chance over places
        var perHour = new Dictionary<(string Id, int Season, bool Rain), double[]>();
        int hours = (DayEnd - DayStart) / HourUnits;

        foreach (var location in rows.Where(r => IsFished(r.Location)).GroupBy(r => r.Location))
        {
            List<CastRow> own = Group(location, inputs.Objects);
            List<CastRow> pool = own.Concat(inherited).ToList();
            // Every fish area the rows name, and the water outside them (rows with no area), as sim.py.
            IEnumerable<string?> areas = own.Select(r => r.Row.FishAreaId).Distinct();
            foreach (string? area in areas)
                for (int season = 0; season < SeasonNames.Length; season++)
                    foreach (bool rain in new[] { false, true })
                        for (int h = 0; h < hours; h++)
                            foreach (var (id, p) in Cast(pool, area, season, rain, DayStart + h * HourUnits, inputs.FishRows))
                            {
                                if (!perHour.TryGetValue((id, season, rain), out double[]? day))
                                    perHour[(id, season, rain)] = day = new double[hours];
                                if (p > day[h]) day[h] = p;
                            }
        }

        var result = new Dictionary<string, CatchRate>(StringComparer.Ordinal);
        foreach (string id in perHour.Keys.Select(k => k.Id).Distinct().OrderBy(i => i, StringComparer.Ordinal))
        {
            double[] Day(int season, bool rain) => perHour.TryGetValue((id, season, rain), out double[]? d) ? d : new double[hours];
            var weekly = new double[SeasonNames.Length];
            double bestDay = 0;
            for (int season = 0; season < SeasonNames.Length; season++)
            {
                double[] sun = Day(season, false), wet = Day(season, true);
                bestDay = Math.Max(bestDay, Math.Max(Sum(sun, 0, hours), Sum(wet, 0, hours)));
                double weighted = (1 - RainShare[season]) * Best10(sun) + RainShare[season] * Best10(wet);
                double basis = Math.Max(weighted * DaysPerWeek, Best10(wet) * RainyDays[season]);
                weekly[season] = LegendaryIds.Contains(id) || basis < MinWeeklyBasis ? 0 : Math.Round(basis, 1);
            }
            if (bestDay > 0) result[id] = new CatchRate(weekly, Math.Round(bestDay, 2));
        }
        return result;
    }

    /// <summary>True when a full best day lands fewer than <see cref="RareBestDay"/> of it.</summary>
    public static bool IsRare(IReadOnlyDictionary<string, CatchRate> rates, string id)
        => rates.TryGetValue(id, out CatchRate? rate) && rate.BestDay < RareBestDay;

    private static bool IsFished(string location)
        => VanillaFishingMaps.Contains(location)
           || (!VanillaLocationKeys.All.Contains(location) && location != DefaultLocation && !SpawnSources.IsIslandLocation(location));

    private static double Sum(double[] perCast, int from, int to)
    {
        double total = 0;
        for (int h = from; h < to && h < perCast.Length; h++) total += CatchesPerHour * perCast[h];
        return total;
    }

    private static double Best10(double[] perCast)
    {
        int window = WindowUnits / HourUnits;
        double best = 0;
        for (int start = 0; start + window <= perCast.Length; start++) best = Math.Max(best, Sum(perCast, start, start + window));
        return best;
    }

    /// <summary>A Data/Locations row as one cast sees it: the outcomes when it passes, as (item or null
    /// for trash, share). A RandomItemId row arrives as adjacent entries; they are one row, its item
    /// picked evenly.</summary>
    private sealed record CastRow(LocationSpawn Row, IReadOnlyList<(string? Id, double Share)> Outcomes);

    private static List<CastRow> Group(IEnumerable<LocationSpawn> rows, IReadOnlyDictionary<string, ObjInfo> objects)
    {
        var result = new List<CastRow>();
        var pending = new List<LocationSpawn>();
        void Flush()
        {
            if (pending.Count == 0) return;
            var outcomes = new List<(string? Id, double Share)>();
            foreach (LocationSpawn entry in pending)
                foreach (var (id, share) in Items(entry.ItemId, objects))
                    outcomes.Add((id, share / pending.Count));
            result.Add(new CastRow(pending[0], outcomes));
            pending.Clear();
        }
        foreach (LocationSpawn row in rows)
        {
            if (!(row.IsRandom && pending.Count > 0 && pending[0] with { ItemId = "" } == row with { ItemId = "" })) Flush();
            pending.Add(row);
            if (!row.IsRandom) Flush();
        }
        Flush();
        return result;
    }

    /// <summary>The items a row's ItemId gives: one id, a query's ids evenly, or trash (null) for an
    /// empty id or a query that names nothing it can list (a secret note, a mod's own query).</summary>
    private static IEnumerable<(string? Id, double Share)> Items(string itemId, IReadOnlyDictionary<string, ObjInfo> objects)
    {
        if (string.IsNullOrWhiteSpace(itemId)) { yield return (null, 1); yield break; }
        QueryResult resolved = ItemQueries.Resolve(itemId, objects);
        if (resolved.ItemIds.Count == 0) { yield return (null, 1); yield break; }
        foreach (string id in resolved.ItemIds) yield return (id, 1.0 / resolved.ItemIds.Count);
    }

    /// <summary>The chance one cast at this place and hour lands each item.</summary>
    private static IEnumerable<(string Id, double Chance)> Cast(
        List<CastRow> pool, string? area, int season, bool rain, int time, IReadOnlyDictionary<string, FishRow> fishRows)
    {
        var landed = new Dictionary<string, double>(StringComparer.Ordinal);
        double reach = 1.0;   // chance the cast gets this far down the precedence order
        foreach (var tie in pool.GroupBy(r => r.Row.Precedence).OrderBy(g => g.Key))
        {
            // Per row: the chance it passes, and how that splits over its outcomes.
            var passing = new List<(double Pass, List<(string? Id, double Chance)> Split)>();
            foreach (CastRow row in tie)
            {
                double gate = RowGate(row.Row, area, season, rain);
                if (gate <= 0) continue;
                var split = new List<(string? Id, double Chance)>();
                foreach (var (id, share) in row.Outcomes)
                {
                    double fish = id == null ? 1 : GenericChance(id, rain, time, fishRows);
                    if (fish > 0) split.Add((id, gate * share * fish));
                }
                double pass = split.Sum(s => s.Chance);
                if (pass > 0) passing.Add((Math.Min(1, pass), split));
            }
            if (passing.Count == 0) continue;
            for (int i = 0; i < passing.Count; i++)
            {
                double first = FirstOfTie(passing.Select(p => p.Pass).ToList(), i);
                foreach (var (id, chance) in passing[i].Split)
                    if (id != null)
                        landed[id] = landed.GetValueOrDefault(id) + reach * first * (chance / passing[i].Pass);
            }
            reach *= passing.Aggregate(1.0, (miss, p) => miss * (1 - p.Pass));
            if (reach <= 0) break;
        }
        return landed.Select(p => (p.Key, p.Value));
    }

    /// <summary>The chance row <paramref name="i"/> of a shuffled tie group is the first to pass:
    /// p_i times the integral over [0,1] of the product over j != i of (1 - p_j t), the product
    /// expanded as a polynomial in t and integrated term by term.</summary>
    internal static double FirstOfTie(IReadOnlyList<double> pass, int i)
    {
        var poly = new List<double> { 1.0 };
        for (int j = 0; j < pass.Count; j++)
        {
            if (j == i) continue;
            var next = new double[poly.Count + 1];
            for (int k = 0; k < poly.Count; k++)
            {
                next[k] += poly[k];
                next[k + 1] -= poly[k] * pass[j];
            }
            poly = next.ToList();
        }
        double integral = 0;
        for (int k = 0; k < poly.Count; k++) integral += poly[k] / (k + 1);
        return pass[i] * integral;
    }

    /// <summary>The chance a row's own gates pass on this cast (sim.py cast): area, season, level,
    /// cast distance, bait and position rows, its Chance, then its Condition.</summary>
    private static double RowGate(LocationSpawn row, string? area, int season, bool rain)
    {
        if (row.FishAreaId != null && row.FishAreaId != area) return 0;
        if (row.Season != null && (int)row.Season.Value != season) return 0;
        if (AnglerLevel < row.MinFishingLevel) return 0;
        if (CastDepth < row.MinDistanceFromShore) return 0;
        if (row.MaxDistanceFromShore > -1 && CastDepth > row.MaxDistanceFromShore) return 0;
        if (row.RequireMagicBait || row.NeedsPosition) return 0;
        return Math.Clamp(row.Chance, 0, 1) * ConditionChance(row.Condition, season, rain);
    }

    /// <summary>The chance a game state query passes (sim.py cond_ok): seasons and weather are read,
    /// RANDOM is its chance, and every other query is taken as false (festival days, mail, items,
    /// special orders, the year and the hour), so a row behind one never counts.</summary>
    internal static double ConditionChance(string? condition, int season, bool rain)
    {
        if (string.IsNullOrWhiteSpace(condition)) return 1;
        double chance = 1;
        foreach (string raw in condition.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            bool negated = raw.StartsWith('!');
            string[] t = (negated ? raw[1..] : raw).Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (t.Length == 0) continue;
            double pass = t[0] switch
            {
                "LOCATION_SEASON" => Has(t.Skip(2), season) ? 1 : 0,
                "SEASON" => Has(t.Skip(1), season) ? 1 : 0,
                "WEATHER" => rain ? 1 : 0,
                "RANDOM" when t.Length > 1 && double.TryParse(t[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double p) => Math.Clamp(p, 0, 1),
                _ => 0,
            };
            chance *= negated ? 1 - pass : pass;
            if (chance <= 0) return 0;
        }
        return chance;
    }

    private static bool Has(IEnumerable<string> names, int season)
        => names.Any(n => string.Equals(n, SeasonNames[season], StringComparison.OrdinalIgnoreCase));

    /// <summary>The fish's own bite roll (sim.py generic_ok): a fish with a Data/Fish row must be in
    /// its time window and weather and within the angler's level, then passes with its base chance,
    /// less for a fish living deeper than the cast, plus the level bonus, at most 0.9. An item with no
    /// Data/Fish row, and a trap fish, always pass.</summary>
    private static double GenericChance(string id, bool rain, int time, IReadOnlyDictionary<string, FishRow> fishRows)
    {
        if (!fishRows.TryGetValue(id, out FishRow? fish) || fish.IsTrap) return 1;
        if (!InWindow(fish.TimeSpans, time)) return 0;
        if (string.Equals(fish.Weather, "rainy", StringComparison.OrdinalIgnoreCase) && !rain) return 0;
        if (string.Equals(fish.Weather, "sunny", StringComparison.OrdinalIgnoreCase) && rain) return 0;
        if (AnglerLevel < fish.MinFishingLevel) return 0;
        double chance = fish.BaseChance;
        chance -= Math.Max(0, fish.MaxDepth - CastDepth) * fish.DepthMultiplier * fish.BaseChance;
        chance += AnglerLevel * LevelChancePerLevel;
        return Math.Clamp(Math.Min(chance, MaxGenericChance), 0, 1);
    }

    private static bool InWindow(string spans, int time)
    {
        string[] t = (spans ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i + 1 < t.Length; i += 2)
            if (int.TryParse(t[i], out int from) && int.TryParse(t[i + 1], out int to) && from <= time && time < to)
                return true;
        return false;
    }
}
