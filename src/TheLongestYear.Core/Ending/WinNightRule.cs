namespace TheLongestYear.Core.Ending;

/// <summary>Year One Ending (spec 2026-09-06 section 1): the night the board completes is the win night,
/// whatever the date. Pure; RunController.OnDayEnding supplies the inputs.</summary>
public static class WinNightRule
{
    public static bool ShouldArm(bool fullCcDone, bool victoryAcknowledged, bool endingArmed)
        => fullCcDone && !victoryAcknowledged && !endingArmed;

    /// <summary>Tomorrow's (day, monthIndex); day 28 rolls to day 1 of the next month, Winter to Spring.</summary>
    public static (int day, int monthIndex) Tomorrow(int dayOfMonth, int monthIndex)
        => Calendar.IsMonthEnd(dayOfMonth)
            ? (1, (monthIndex + 1) % Calendar.MonthsPerYear)
            : (dayOfMonth + 1, monthIndex);
}
