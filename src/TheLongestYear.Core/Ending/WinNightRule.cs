namespace TheLongestYear.Core.Ending;

/// <summary>Year One Ending (spec 2026-09-06 section 1): the night the board completes is the win night,
/// whatever the date. Pure; RunController.OnDayEnding supplies the inputs.</summary>
public static class WinNightRule
{
    public static bool ShouldArm(bool fullCcDone, bool victoryAcknowledged, bool endingArmed)
        => fullCcDone && !victoryAcknowledged && !endingArmed;
}
