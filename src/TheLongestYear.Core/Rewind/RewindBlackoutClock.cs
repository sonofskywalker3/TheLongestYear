namespace TheLongestYear.Core.Rewind;

/// <summary>The rewind blackout's backstop clock. The backstop lifts the black if the screen never
/// goes quiet, so the player is never left looking at nothing. A menu draws above the black, so
/// time spent in one (the Junimo Shrine, the Herd Book, the planning hub) is not time spent
/// looking at nothing, and it does not count: a player who shopped in the shrine for three
/// minutes tripped the backstop and saw the old farmhouse behind the menu (Celjhyn, 1.2.0).</summary>
public static class RewindBlackoutClock
{
    /// <summary>The held time after a tick of <paramref name="elapsedMs"/>.</summary>
    public static float HeldAfter(float heldMs, float elapsedMs, bool menuOpen)
        => menuOpen ? heldMs : heldMs + elapsedMs;
}
