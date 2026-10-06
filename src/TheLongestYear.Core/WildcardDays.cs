using System;

namespace TheLongestYear.Core;

/// <summary>Wildcard days, the run-state half (spec section 8): plan the week's day at the week
/// start, reveal and store the twist on that morning, and report what is stored for today. The
/// twist is rolled once and kept on <see cref="RunState"/>; a reload reads it back, never re-rolls.</summary>
public static class WildcardDays
{
    private const int NoWeek = -1;
    private const int NoDay = 0;

    /// <summary>Plan the current week's wildcard day if it is not planned yet. With the option off
    /// any stale plan from an earlier week is dropped. True when a new plan was stored.</summary>
    public static bool PlanWeek(RunState run, bool enabled)
    {
        int week = run.WeekOfYear;
        if (!enabled)
        {
            if (run.WildcardWeek != NoWeek) Forget(run);
            return false;
        }
        if (run.WildcardWeek == week) return false;
        run.WildcardWeek = week;
        run.WildcardDay = WildcardSchedule.DayFor(run.Seed, week, WildcardSchedule.BlockedDays((int)run.Season));
        run.WildcardTwist = null;
        run.WildcardTwistDay = NoDay;
        return true;
    }

    /// <summary>True when the stored plan is this week's and has a day.</summary>
    public static bool HasPlanThisWeek(RunState run)
        => run.WildcardWeek == run.WeekOfYear && run.WildcardDay != NoDay;

    /// <summary>Today's twist on the wildcard morning: the stored one when it was already revealed
    /// today, otherwise rolled now and stored. Null on any other day.</summary>
    public static string? RevealToday(RunState run, Func<bool> minecartsRepaired, out bool revealedNow)
    {
        revealedNow = false;
        if (!HasPlanThisWeek(run) || run.DayOfMonth != run.WildcardDay) return null;
        string? stored = StoredTwistToday(run);
        if (stored != null) return stored;
        run.WildcardTwist = WildcardSchedule.TwistFor(run.Seed, run.WeekOfYear, minecartsRepaired(),
            WildcardSchedule.SnowAllowed((int)run.Season, run.DayOfMonth));
        run.WildcardTwistDay = run.DayOfMonth;
        revealedNow = true;
        return run.WildcardTwist;
    }

    /// <summary>The twist already stored for today, or null. Never rolls (the load path).</summary>
    public static string? StoredTwistToday(RunState run)
        => run.WildcardWeek == run.WeekOfYear
           && run.WildcardTwist != null
           && run.WildcardTwistDay == run.DayOfMonth
           && run.WildcardTwistDay != NoDay
            ? run.WildcardTwist
            : null;

    /// <summary>True on the night that follows an extra-growth wildcard day (read at DayEnding).</summary>
    public static bool GrowthTonight(RunState run) => StoredTwistToday(run) == WildcardSchedule.ExtraGrowth;

    /// <summary>The twist whose overnight half runs tonight (read at DayEnding): the snow day (no
    /// outdoor growth) or the night event. Null on any other night and on day 28, the rewind
    /// night, whose farm-event slot belongs to the day-28 driver.</summary>
    public static string? NightTwistTonight(RunState run)
    {
        if (run.DayOfMonth == RewindDay) return null;
        string? twist = StoredTwistToday(run);
        return twist == WildcardSchedule.SnowDay || twist == WildcardSchedule.NightEvent ? twist : null;
    }

    private const int RewindDay = 28;

    /// <summary>Debug: make today the wildcard day with <paramref name="twistId"/>.</summary>
    public static void ForceToday(RunState run, string twistId)
    {
        run.WildcardWeek = run.WeekOfYear;
        run.WildcardDay = run.DayOfMonth;
        run.WildcardTwist = twistId;
        run.WildcardTwistDay = run.DayOfMonth;
    }

    /// <summary>Debug: drop today's twist (the day stays planned).</summary>
    public static void ClearTwist(RunState run)
    {
        run.WildcardTwist = null;
        run.WildcardTwistDay = NoDay;
    }

    private static void Forget(RunState run)
    {
        run.WildcardWeek = NoWeek;
        run.WildcardDay = NoDay;
        run.WildcardTwist = null;
        run.WildcardTwistDay = NoDay;
    }
}
