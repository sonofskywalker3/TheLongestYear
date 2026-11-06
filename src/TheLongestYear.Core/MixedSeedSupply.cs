using System;
using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core.Obtainability;

namespace TheLongestYear.Core;

/// <summary>A crop one of the pool's seeds grows, as far as the supply estimate reads it
/// (Data/Crops: the seed, the harvest, its seasons, the sum of its growth phases, RegrowDays, the
/// least one harvest gives). <see cref="OnlyRoute"/>: nothing but the pool gives a year-1 farm this
/// seed (every shop that sells it is closed in year 1), so the estimate sizes what a board may ask
/// of the crop.</summary>
public sealed record MixedSeedCrop(
    string SeedId, string HarvestId, IReadOnlyList<Season> Seasons, int GrowthDays, int RegrowDays, int HarvestMinStack, bool OnlyRoute);

/// <summary>What the availability build reads of another mod's Mixed Seeds pool: its rows and the
/// crops its seeds grow. Null on <see cref="ItemPools"/> when no mod keeps a pool, so a game without
/// one hashes its availability inputs exactly as before.</summary>
public sealed record MixedSeedInputs(IReadOnlyList<MixedSeedRow> Pool, IReadOnlyList<MixedSeedCrop> Crops);

/// <summary>Bags of Mixed Seeds that turn up on one day of the year.</summary>
public sealed record MixedSeedArrival(int DayOfYear, double Bags, string Source);

/// <summary>How many bags of Mixed Seeds a year-1 farmer can gather and when, and from that how much
/// of a crop that only the pool gives a season can yield (Jeff, 2026-11-06: size the ask from the
/// Mixed Seeds that can be gathered and how likely that crop is; never make Easy or Normal need a
/// Seed Maker).
/// <para>
/// <b>The supply</b> is a ceiling, the same kind the forage and fish bases are: every weed on every
/// map a year-1 farmer walks, cut the day it stands, and every bag Krobus sells. A weed drops a bag
/// 2.5% of the time (Object.cutWeed: Fiber half the time, else 5%); a green rain weed 2.25% (Moss
/// takes a tenth of the drops). The weed counts were taken on a Standard farm with no other mod's
/// maps, 2026-11-06, with <c>tly_weedsweep</c> every morning from Spring 2 to Fall 28: a weed that is
/// cut does not come back, so nearly all of them are the stock a year starts with and the weeds the
/// green rain leaves. Not counted, because each is small or needs luck of its own: mine barrels and
/// crates (5.8% each for one bag), dig spots on the farm, fishing treasure below Fishing 2, the
/// Wilderness Golem, prize tickets and the Seed Maker. The Mutant Bug Lair's 140 weeds are left out
/// too: it opens with the Dark Talisman quest, after the Community Center.
/// </para>
/// <para>
/// <b>A saved bag carries over, and each bag is counted once</b> (Jeff, 2026-11-06). Bags only
/// compete between seasons: inside one season the shares already split them between its crops. So a
/// bag is shared evenly between the seasons that still want it, which are the seasons the asks come
/// from (<see cref="Combination"/>), from the bag's own season on. In its own season it is planted
/// the day it turns up, in a later one on that season's first day. A bag that turns up too late in
/// its season for any of the pool's crops there to be harvested goes on to the later seasons whole:
/// the green rain falls on one of eight Summer days and the last of them is used, so its bags are
/// Fall's. The yields are worked out for every combination of seasons in the cold build and stored;
/// the board reads the one for the seasons it asks from, and an ask rolled outside a board (a weekly
/// or shrine goal) reads the one for all three, the smallest.
/// </para></summary>
public static class MixedSeedSupply
{
    /// <summary>Object.cutWeed: no Fiber (1 in 2), then Mixed Seeds at 0.05.</summary>
    public const double BagsPerWeed = 0.025;
    /// <summary>A green rain weed gives Moss in place of its drop 1 time in 10.</summary>
    public const double BagsPerGreenRainWeed = BagsPerWeed * 0.9;
    /// <summary>Krobus sells 10 bags each Thursday (Data/Shops ShadowShop, DAY_OF_WEEK Thursday).</summary>
    public const int KrobusBagsPerVisit = 10;
    private const int Thursday = 4;

    /// <summary>Weeds standing on Spring 1 on the maps open from the first day: Farm 546, Forest 132, Town 85.</summary>
    public const int SpringStartWeeds = 763;
    /// <summary>Weeds the Farm grows back overnight into Summer 1.</summary>
    public const int SummerStartWeeds = 15;
    /// <summary>Weeds on the Railroad the day it opens, Summer 3.</summary>
    public const int RailroadWeeds = 45;
    public const int RailroadOpensDay = Calendar.DaysPerMonth + 3;
    /// <summary>Weeds the green rain leaves on the maps a Summer farmer walks: Forest 915, Secret Woods
    /// 160, Mountain 100, Bus Stop 81, Farm 75, Backwoods 64, Railroad 48.</summary>
    public const int GreenRainWeeds = 1443;
    /// <summary>The last day the green rain can fall (Utility.isGreenRainDay: Summer 5, 6, 7, 14, 15, 16, 18 or 23).</summary>
    public const int GreenRainLastDay = Calendar.DaysPerMonth + 23;
    /// <summary>Weeds the Farm grows back overnight into Fall 1.</summary>
    public const int FallStartWeeds = 5;

    private static readonly Season[] GrowingSeasons = { Season.Spring, Season.Summer, Season.Fall };

    /// <summary>Every arrival of the year up to the end of Fall, in day order.</summary>
    public static IReadOnlyList<MixedSeedArrival> Arrivals { get; } = BuildArrivals();

    private static List<MixedSeedArrival> BuildArrivals()
    {
        int sewerDay = (AvailabilityWeeks.SewerWeek - 1) * Calendar.DaysPerWeek + 1;
        var arrivals = new List<MixedSeedArrival>
        {
            new(1, SpringStartWeeds * BagsPerWeed, "weeds standing on Spring 1 (Farm, Forest, Town)"),
            new(FirstDay(Season.Summer), SummerStartWeeds * BagsPerWeed, "weeds back on the Farm on Summer 1"),
            new(RailroadOpensDay, RailroadWeeds * BagsPerWeed, "weeds on the Railroad when it opens"),
            new(GreenRainLastDay, GreenRainWeeds * BagsPerGreenRainWeed, "weeds the green rain leaves, on its last possible day"),
        };
        if (FallStartWeeds > 0)
            arrivals.Add(new MixedSeedArrival(FirstDay(Season.Fall), FallStartWeeds * BagsPerWeed, "weeds back on the Farm on Fall 1"));
        for (int day = sewerDay; day <= LastDay(Season.Fall); day++)
            if (Calendar.DayOfMonthOf(day) % Calendar.DaysPerWeek == Thursday)
                arrivals.Add(new MixedSeedArrival(day, KrobusBagsPerVisit, "Krobus, Thursday"));
        return arrivals.OrderBy(a => a.DayOfYear).ToList();
    }

    public static int FirstDay(Season season) => (int)season * Calendar.DaysPerMonth + 1;
    public static int LastDay(Season season) => ((int)season + 1) * Calendar.DaysPerMonth;

    /// <summary>Bags that turn up from the first day of <paramref name="season"/> through <paramref name="dayOfYear"/>.</summary>
    public static double BagsInSeasonBy(Season season, int dayOfYear)
        => Arrivals.Where(a => a.DayOfYear >= FirstDay(season) && a.DayOfYear <= Math.Min(dayOfYear, LastDay(season))).Sum(a => a.Bags);

    /// <summary>How many times a seed planted outdoors on <paramref name="plantDay"/> is harvested by
    /// the end of <paramref name="season"/>: the first after its growth days, then one every regrow
    /// days. Zero when the first would come after the season's last day.</summary>
    public static int Harvests(int plantDay, Season season, int growthDays, int regrowDays)
    {
        int last = LastDay(season);
        int first = plantDay + Math.Max(1, growthDays);
        if (first > last) return 0;
        return 1 + (regrowDays > 0 ? (last - first) / regrowDays : 0);
    }

    private const string CombinationJoiner = "+";

    /// <summary>The name of a set of seasons asks come from: "Spring", "Spring+Fall",
    /// "Spring+Summer+Fall". Winter is never one of them.</summary>
    public static string Combination(IEnumerable<Season> seasons)
        => string.Join(CombinationJoiner, GrowingSeasons.Where(seasons.Contains));

    /// <summary>Every season asks could come from with this pool, which is the combination an ask
    /// outside a board reads: each season one of the pool-only crops grows in, and Fall, whose
    /// Artichoke grows from the same bags in any game.</summary>
    public static string EverySeason(MixedSeedInputs inputs)
        => Combination(GrowingSeasons.Where(s => s == Season.Fall
            || inputs.Crops.Any(c => c.OnlyRoute && (c.Seasons.Count == 0 || c.Seasons.Contains(s)))));

    /// <summary>The seven combinations of Spring, Summer and Fall.</summary>
    public static IReadOnlyList<IReadOnlyList<Season>> Combinations { get; } = Enumerable.Range(1, (1 << GrowingSeasons.Length) - 1)
        .Select(mask => (IReadOnlyList<Season>)GrowingSeasons.Where((_, i) => (mask & (1 << i)) != 0).ToList()).ToList();

    private static Season SeasonOfDay(int dayOfYear) => (Season)((dayOfYear - 1) / Calendar.DaysPerMonth);

    /// <summary>Whether a bag turning up on <paramref name="arrival"/> can still be planted in its own
    /// season for something: one of the pool-only crops of that season is harvested from it in time.</summary>
    private static bool UsableInOwnSeason(MixedSeedArrival arrival, IReadOnlyList<MixedSeedCrop> crops)
    {
        Season season = SeasonOfDay(arrival.DayOfYear);
        return crops.Any(c => c.OnlyRoute && (c.Seasons.Count == 0 || c.Seasons.Contains(season))
            && Harvests(arrival.DayOfYear, season, c.GrowthDays, c.RegrowDays) > 0);
    }

    /// <summary>What one crop can yield in each season (Spring to Winter) when the asks come from the
    /// seasons in <paramref name="asked"/>: for each arrival, the part of its bags that season gets
    /// (an even split between the asked seasons from its own on that can use it), times the seed's
    /// share of a bag that season, times the harvests the planting still gets, times the least a
    /// harvest gives. A season not in <paramref name="asked"/> yields nothing.</summary>
    public static double[] YieldBySeason(
        MixedSeedCrop crop, MixedSeedInputs inputs, IReadOnlyCollection<Season> asked, Func<string, IReadOnlyList<Season>?> cropSeasons)
    {
        if (crop == null) throw new ArgumentNullException(nameof(crop));
        if (inputs == null) throw new ArgumentNullException(nameof(inputs));
        var bySeason = new double[Calendar.MonthsPerYear];
        foreach (Season season in GrowingSeasons)
        {
            if (!asked.Contains(season)) continue;
            if (crop.Seasons.Count > 0 && !crop.Seasons.Contains(season)) continue;
            if (!MixedSeedPool.Shares(season, indoors: false, inputs.Pool, cropSeasons).TryGetValue(crop.SeedId, out double share)) continue;
            foreach (MixedSeedArrival arrival in Arrivals)
            {
                Season own = SeasonOfDay(arrival.DayOfYear);
                if (own > season) continue;
                bool ownUses = UsableInOwnSeason(arrival, inputs.Crops);
                int takers = GrowingSeasons.Count(s => asked.Contains(s) && (s > own || (s == own && ownUses)));
                if (takers == 0 || (own == season && !ownUses)) continue;
                int plantDay = own == season ? arrival.DayOfYear : FirstDay(season);
                bySeason[(int)season] += arrival.Bags / takers * share
                    * Harvests(plantDay, season, crop.GrowthDays, crop.RegrowDays) * Math.Max(1, crop.HarvestMinStack);
            }
        }
        return bySeason;
    }

    /// <summary>The quantity basis of every crop only the pool gives (<see cref="MixedSeedCrop.OnlyRoute"/>),
    /// by qualified harvest id and then by <see cref="Combination"/> of the seasons asks come from: its
    /// <see cref="YieldBySeason"/>, the largest when two seeds grow one crop. A combination that
    /// yields the crop nothing is left out, and so is a crop no combination yields. Read like the
    /// other bases: the difficulty's band of it is the ask (<see cref="AskBands"/>).</summary>
    public static IReadOnlyDictionary<string, IReadOnlyDictionary<string, double[]>> BoardBases(MixedSeedInputs? inputs)
    {
        var bases = new Dictionary<string, IReadOnlyDictionary<string, double[]>>(StringComparer.Ordinal);
        if (inputs == null || inputs.Pool.Count == 0) return bases;
        var seasons = inputs.Crops.GroupBy(c => c.SeedId, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First().Seasons, StringComparer.Ordinal);
        IReadOnlyList<Season>? SeasonsOf(string seed) => seasons.TryGetValue(seed, out IReadOnlyList<Season>? s) ? s : null;
        foreach (MixedSeedCrop crop in inputs.Crops.Where(c => c.OnlyRoute).OrderBy(c => c.SeedId, StringComparer.Ordinal))
        {
            string id = BundleParsing.NormalizeItemId(crop.HarvestId);
            var byCombination = bases.TryGetValue(id, out var had)
                ? new SortedDictionary<string, double[]>(had.ToDictionary(p => p.Key, p => p.Value), StringComparer.Ordinal)
                : new SortedDictionary<string, double[]>(StringComparer.Ordinal);
            foreach (IReadOnlyList<Season> asked in Combinations)
            {
                double[] yield = YieldBySeason(crop, inputs, asked, SeasonsOf);
                if (yield.All(y => y <= 0)) continue;
                string key = Combination(asked);
                if (byCombination.TryGetValue(key, out double[]? other))
                    for (int i = 0; i < other.Length; i++) yield[i] = Math.Max(yield[i], other[i]);
                byCombination[key] = yield;
            }
            if (byCombination.Count > 0) bases[id] = byCombination;
        }
        return bases;
    }

    /// <summary>The basis an ask outside a board reads: each crop's <see cref="BoardBases"/> entry for
    /// <see cref="EverySeason"/>, where every bag is shared the most ways.</summary>
    public static IReadOnlyDictionary<string, double[]> Bases(MixedSeedInputs? inputs)
    {
        var bases = new Dictionary<string, double[]>(StringComparer.Ordinal);
        if (inputs == null || inputs.Pool.Count == 0) return bases;
        string every = EverySeason(inputs);
        foreach (var (id, byCombination) in BoardBases(inputs))
            if (byCombination.TryGetValue(every, out double[]? yield)) bases[id] = yield;
        return bases;
    }
}
