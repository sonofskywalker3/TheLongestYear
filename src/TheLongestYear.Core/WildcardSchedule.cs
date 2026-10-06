using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core;

/// <summary>Wildcard days (Randomizer, spec section 8): one day of each week carries a twist,
/// good, bad or odd. This is the Core half: which day, which twist. Never a festival or passive
/// festival day, never day 28 (the rewind night).</summary>
public static class WildcardSchedule
{
    private const int DaySalt = 0x3C1D;
    private const int TwistSalt = 0x5E2B;
    private const int DaysPerWeek = 7;
    private const int WeeksPerSeason = 4;
    private const int LastDay = 28;

    public const string DoubleForage = "double_forage";
    public const string FastBites = "fast_bites";
    public const string ExtraGrowth = "extra_growth";
    public const string ShopSale = "shop_sale";
    public const string MaxLuck = "max_luck";
    public const string MinesClosedDay = "mines_closed_day";
    public const string SlowBites = "slow_bites";
    public const string SellDown = "sell_down";
    public const string EnergyDrain = "energy_drain";
    public const string SnowDay = "snow_day";
    public const string NightEvent = "night_event";
    public const string DebrisReturn = "debris_return";
    public const string Rockslide = "rockslide";

    public static readonly IReadOnlyList<string> AllTwists = new[]
    {
        DoubleForage, FastBites, ExtraGrowth, ShopSale, MaxLuck,
        MinesClosedDay, SlowBites, SellDown, EnergyDrain,
        SnowDay, NightEvent, DebrisReturn, Rockslide,
    };

    /// <summary>Days a wildcard may not land on: the cart's blocked days plus day 28.</summary>
    public static IReadOnlyList<int> BlockedDays(int seasonIndex)
    {
        var blocked = new List<int>(CartSchedule.BlockedDays(seasonIndex));
        if (!blocked.Contains(LastDay)) blocked.Add(LastDay);
        return blocked;
    }

    /// <summary>The wildcard day-of-month for the week, or 0 when every day in it is blocked.</summary>
    public static int DayFor(int seed, int weekOfYear, IReadOnlyCollection<int> blockedDays)
    {
        int weekStart = ((weekOfYear - 1) % WeeksPerSeason) * DaysPerWeek + 1;
        var open = new List<int>();
        for (int d = weekStart; d < weekStart + DaysPerWeek; d++)
            if (d != LastDay && !blockedDays.Contains(d)) open.Add(d);
        if (open.Count == 0) return 0;
        var rng = RollSeed.Rng(seed, weekOfYear, DaySalt);
        return open[rng.Next(open.Count)];
    }

    /// <summary>Out-of-season snow (spec section 8) is never drawn in Winter (it would not be out
    /// of season) or on day 1 (vanilla forces Sun on a season's first day).</summary>
    public static bool SnowAllowed(int seasonIndex, int dayOfMonth)
        => seasonIndex != WinterIndex && dayOfMonth != FirstDay;

    private const int WinterIndex = 3;
    private const int FirstDay = 1;

    /// <summary>The week's twist, uniform over the pool; the rockslide needs repaired minecarts,
    /// the snow day needs <paramref name="snowAllowed"/> (<see cref="SnowAllowed"/>).</summary>
    public static string TwistFor(int seed, int weekOfYear, bool minecartsRepaired, bool snowAllowed = true)
    {
        var pool = AllTwists.Where(t => (minecartsRepaired || t != Rockslide) && (snowAllowed || t != SnowDay)).ToList();
        var rng = RollSeed.Rng(seed, weekOfYear, TwistSalt);
        return pool[rng.Next(pool.Count)];
    }
}
