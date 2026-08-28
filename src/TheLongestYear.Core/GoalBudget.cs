using System;

namespace TheLongestYear.Core;

/// <summary>
/// Theme-week goal budget (spec 2026-08-28-theme-week-budget): how many goals a theme may ask
/// for this week given what its pool still holds and how many weeks the season has left. The
/// season cap is a ceiling, never a target, so week 1 cannot take everything the season has
/// and leave week 4 with nothing (the cliff both 2026-08-28 sims showed in every season).
/// </summary>
public static class GoalBudget
{
    public const int WeeksPerSeason = 4;

    /// <summary>Weeks of the season still to play, counting this one (4 on week 1, 1 on week 4).</summary>
    public static int WeeksLeftInSeason(int weekOfYear)
    {
        if (weekOfYear < 1) return WeeksPerSeason;
        return WeeksPerSeason - ((weekOfYear - 1) % WeeksPerSeason);
    }

    /// <summary>
    /// Goals to ask for this week. <paramref name="dueLines"/> are the open lines the day-28 gate
    /// demands this season, <paramref name="fillerLines"/> the other open lines in the theme's
    /// pool; filler counts only as far as the season's allowance lets it be asked over the weeks
    /// left. The result is the pool spread evenly over the remaining weeks, at least 1 when
    /// anything is askable and never above <paramref name="seasonCap"/>.
    /// </summary>
    public static int For(int seasonCap, int dueLines, int fillerLines, int fillerAllowance, int weeksLeft)
    {
        if (seasonCap <= 0) return 0;
        if (weeksLeft < 1) weeksLeft = 1;
        dueLines = Math.Max(0, dueLines);
        fillerLines = Math.Max(0, fillerLines);
        long fillerBudget = fillerAllowance >= GoalSamplingRules.UnlimitedFiller
            ? fillerLines
            : Math.Min(fillerLines, (long)Math.Max(0, fillerAllowance) * weeksLeft);
        long askable = dueLines + fillerBudget;
        if (askable <= 0) return 0;
        long perWeek = (askable + weeksLeft - 1) / weeksLeft;
        return (int)Math.Clamp(perWeek, 1, seasonCap);
    }
}
