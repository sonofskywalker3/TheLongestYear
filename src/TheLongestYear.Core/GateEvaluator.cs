namespace TheLongestYear.Core;

public enum GateResult
{
    /// <summary>Keep playing (mid-week, or a passed checkpoint that isn't the final win).</summary>
    Continue,
    /// <summary>Championed contract not finished by week's end — run ends.</summary>
    WeeklyFail,
    /// <summary>Not all five contracts finished by month's end — run ends.</summary>
    MonthlyFail,
    /// <summary>Winter cleared with the whole CC done — loop breaks.</summary>
    Win
}

/// <summary>
/// Decides, at the end of a day, whether the run continues, fails, or wins.
/// Weekly gate (championed contract) is checked before the monthly gate (all five).
/// </summary>
public sealed class GateEvaluator
{
    public GateResult EvaluateDayEnd(int dayOfMonth, int monthIndex, bool championedComplete, bool allFiveComplete)
    {
        if (!Calendar.IsWeekEnd(dayOfMonth))
            return GateResult.Continue;

        if (!championedComplete)
            return GateResult.WeeklyFail;

        if (Calendar.IsMonthEnd(dayOfMonth))
        {
            if (!allFiveComplete)
                return GateResult.MonthlyFail;

            if (monthIndex == Calendar.MonthsPerYear - 1)
                return GateResult.Win;
        }

        return GateResult.Continue;
    }
}
