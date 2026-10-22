namespace TheLongestYear.Core.Rewind;

/// <summary>What vanilla's HUD switch (<c>Game1.displayHUD</c>) should be while the rewind holds it.
/// The hold runs from the bedroom to the fade-up after the theme pick, and the season reset lands in
/// the middle of it: <c>loadForNewGame</c> switches the HUD back on, so without a correction the
/// clock and toolbar sat on the black behind the planning hub for the whole pick. While held the
/// switch is forced off every frame; on release it always goes back on, because release is the
/// moment the player has the day.</summary>
public static class RewindHudRule
{
    /// <summary>The value displayHUD takes when the hold lets go.</summary>
    public const bool ValueOnRelease = true;

    /// <summary>The value to write this frame, or null when the current value is already right.</summary>
    public static bool? Correction(bool hudHeld, bool displayHud)
        => hudHeld && displayHud ? false : null;
}
