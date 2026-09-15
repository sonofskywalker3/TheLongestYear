namespace TheLongestYear.Core;

/// <summary>
/// Which part of a festival the player is in, as far as the time-loop clock is concerned.
///
/// FestivalTimeFlow lets the in-game clock run during festivals and ejects the player when the
/// festival's scheduled end time arrives. That is only right while the player is wandering the
/// grounds. Two kinds of festival phase must freeze the clock and hold the eject:
///
///   1. A main-event contest on the game's own real-time timer: the Egg Hunt (52 s) and the ice
///      fishing contest (120 s). Ejecting mid-contest is what left a player stuck "holding" the
///      festival's fishing rod after the Festival of Ice (Nexus bug, 2026-09-15).
///   2. A scripted sequence where the game has taken control away from the player: the Flower
///      Dance, the Luau soup tasting, grange judging, the Spirit's Eve maze intro, the Winter Star
///      gift exchange, the Moonlight Jellies. Vanilla runs those with playerControlSequence off.
///
/// The rule is pure so it can be tested without the game: pass in the event's timer and its
/// player-control flag.
/// </summary>
public static class FestivalPhase
{
    /// <summary>
    /// True when the player is free to wander the festival grounds: no contest timer is counting
    /// down and the game has not taken control for a scripted sequence. Only then may the clock
    /// tick and the end-of-festival eject fire.
    /// </summary>
    public static bool IsFreeRoam(int festivalTimerMs, bool playerControlSequence)
    {
        if (festivalTimerMs > 0) return false;
        return playerControlSequence;
    }
}
