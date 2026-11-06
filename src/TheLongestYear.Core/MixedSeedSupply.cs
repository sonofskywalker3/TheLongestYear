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
/// <b>Each bag is counted once, in the season it turns up, planted that day.</b> A bag kept for a
/// later season is the player's own gain; counting it again there would promise the same bag to two
/// asks. The green rain falls on one of eight Summer days and the last of them is used, which is too
/// late for any Summer crop here, so its bags size nothing.
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

    /// <summary>What one crop can yield in each season (Spring to Winter) from the bags that turn up
    /// in it: bags, times the seed's share of a bag that season, times the harvests a planting that
    /// day still gets, times the least a harvest gives.</summary>
    public static double[] YieldBySeason(MixedSeedCrop crop, IReadOnlyList<MixedSeedRow> pool, Func<string, IReadOnlyList<Season>?> cropSeasons)
    {
        if (crop == null) throw new ArgumentNullException(nameof(crop));
        var bySeason = new double[Calendar.MonthsPerYear];
        foreach (Season season in GrowingSeasons)
        {
            if (crop.Seasons.Count > 0 && !crop.Seasons.Contains(season)) continue;
            if (!MixedSeedPool.Shares(season, indoors: false, pool, cropSeasons).TryGetValue(crop.SeedId, out double share)) continue;
            foreach (MixedSeedArrival arrival in Arrivals)
            {
                if (arrival.DayOfYear < FirstDay(season) || arrival.DayOfYear > LastDay(season)) continue;
                bySeason[(int)season] += arrival.Bags * share
                    * Harvests(arrival.DayOfYear, season, crop.GrowthDays, crop.RegrowDays) * Math.Max(1, crop.HarvestMinStack);
            }
        }
        return bySeason;
    }

    /// <summary>The quantity basis of every crop only the pool gives (<see cref="MixedSeedCrop.OnlyRoute"/>),
    /// by qualified harvest id: its <see cref="YieldBySeason"/>, the largest when two seeds grow one
    /// crop. A crop the estimate gives nothing in any season is left out. Read like the other bases:
    /// the difficulty's band of it is the ask (<see cref="AskBands"/>).</summary>
    public static IReadOnlyDictionary<string, double[]> Bases(MixedSeedInputs? inputs)
    {
        var bases = new Dictionary<string, double[]>(StringComparer.Ordinal);
        if (inputs == null || inputs.Pool.Count == 0) return bases;
        var seasons = inputs.Crops.GroupBy(c => c.SeedId, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First().Seasons, StringComparer.Ordinal);
        IReadOnlyList<Season>? SeasonsOf(string seed) => seasons.TryGetValue(seed, out IReadOnlyList<Season>? s) ? s : null;
        foreach (MixedSeedCrop crop in inputs.Crops.Where(c => c.OnlyRoute).OrderBy(c => c.SeedId, StringComparer.Ordinal))
        {
            double[] yield = YieldBySeason(crop, inputs.Pool, SeasonsOf);
            if (yield.All(y => y <= 0)) continue;
            string id = BundleParsing.NormalizeItemId(crop.HarvestId);
            if (bases.TryGetValue(id, out double[]? had))
                for (int i = 0; i < had.Length; i++) yield[i] = Math.Max(yield[i], had[i]);
            bases[id] = yield;
        }
        return bases;
    }
}
