using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core;

/// <summary>
/// Which days the Traveling Cart is in town. Vanilla is <c>dayOfMonth % 7 % 5 == 0</c> (Fri and
/// Sun). With Random Cart Days each day of a week rolls independently, festivals are skipped,
/// and a week never comes up empty unless every day in it is a festival.
/// </summary>
public static class CartSchedule
{
    private const int Salt = 0x4A2D;
    private const int DaysPerWeek = 7;
    private const int VisitChanceOutOf = 7;
    private const int VisitChanceHits = 2;
    private const int SeedMultiplier = 31;
    private const int WeekMultiplier = 7919;
    private const int VanillaMod = 7;
    private const int VanillaModSecond = 5;
    private const int PassiveFestivalFirstDay = 15;
    private const int PassiveFestivalLastDay = 17;
    private const int SpringIndex = 0;
    private const int WinterIndex = 3;

    /// <summary>First day (1, 8, 15 or 22) of the week containing <paramref name="dayOfMonth"/>.</summary>
    public static int WeekStartOf(int dayOfMonth) => ((dayOfMonth - 1) / DaysPerWeek) * DaysPerWeek + 1;

    public static IReadOnlyList<int> VanillaDaysInWeek(int weekStartDay)
    {
        var days = new List<int>();
        for (int d = weekStartDay; d < weekStartDay + DaysPerWeek; d++)
            if (d % VanillaMod % VanillaModSecond == 0)
                days.Add(d);
        return days;
    }

    public static IReadOnlyList<int> RandomDaysInWeek(int seed, int weekOfYear, int weekStartDay, IReadOnlyCollection<int> blockedDays)
    {
        // One Random drawn in day order; reseeding per day gives near-identical first draws.
        var rng = new Random(unchecked(seed * SeedMultiplier + weekOfYear * WeekMultiplier) ^ Salt);
        var days = new List<int>();
        var open = new List<int>();
        for (int d = weekStartDay; d < weekStartDay + DaysPerWeek; d++)
        {
            bool hit = rng.Next(VisitChanceOutOf) < VisitChanceHits;
            if (blockedDays.Contains(d)) continue;
            open.Add(d);
            if (hit) days.Add(d);
        }
        if (days.Count == 0 && open.Count > 0)
            days.Add(open[rng.Next(open.Count)]);
        days.Sort();
        return days;
    }

    public static IReadOnlyList<int> BlockedDays(int seasonIndex)
    {
        var blocked = new List<int>(WeatherScheduler.FestivalDays(seasonIndex));
        // Desert Festival and Night Market (which keeps its own boat cart).
        if (seasonIndex == SpringIndex || seasonIndex == WinterIndex)
            for (int d = PassiveFestivalFirstDay; d <= PassiveFestivalLastDay; d++)
                blocked.Add(d);
        return blocked;
    }

    /// <summary>The cart days for the week containing the given game date. Week number and start
    /// come from the date itself (never from RunState.WeekOfYear, which lags at day start).</summary>
    public static IReadOnlyList<int> ForWeek(RunState run, int seasonIndex, int dayOfMonth, bool random)
    {
        int weekStartDay = WeekStartOf(dayOfMonth);
        if (!random) return VanillaDaysInWeek(weekStartDay);
        int weekOfYear = Calendar.WeekOfYear(seasonIndex, dayOfMonth);
        if (run.CartDaysWeek != weekOfYear || run.CartDays == null)
        {
            run.CartDays = RandomDaysInWeek(run.Seed, weekOfYear, weekStartDay, BlockedDays(seasonIndex)).ToList();
            run.CartDaysWeek = weekOfYear;
        }
        return run.CartDays;
    }
}
