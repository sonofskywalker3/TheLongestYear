namespace TheLongestYear.Core.Ending;

/// <summary>Year One Ending (spec 2026-09-06 section 3): vanilla event ids the mod hands to the game
/// for the CURRENT run only, and which must never survive into the next loop.
///
/// <para>The hand-off: the ending's "Keep playing" branch adds <see cref="CeremonyEventId"/> (the
/// vanilla Community Center completion ceremony) to <c>Game1.player.eventsSeen</c> so vanilla flips
/// its post-completion world for the rest of that year: the abandoned JojaMart lightning strike on
/// the next storm, Pierre open on Wednesdays, the Joja shutdown. The mod plays its own ending in that
/// slot, so the ceremony itself stays suppressed
/// (<c>EventSuppressionPatch.SuppressedEventIds</c>); only the flag is handed over.</para>
///
/// <para>Why it must not persist: the cross-loop memory (<c>MetaState.SeenEventsEver</c>) is merged
/// from <c>eventsSeen</c> on every save and re-seeded back into <c>eventsSeen</c> on every reset. If
/// the hand-off id entered that memory, the loop after a "Keep playing" would start on Spring 1 with
/// the game believing the ceremony already happened: a destroyed JojaMart, Pierre keeping post-
/// completion hours and a lightning cutscene on the first storm, on a run whose board has zero
/// bundles done. Nexus bug 1113630 was the mirror image of this. The id is also in the replayable
/// scan's exclusion set, so it can never be flagged replayable and skipped that way instead: this
/// helper is the only thing keeping it out.</para></summary>
public static class PostCompletionEvents
{
    /// <summary>Vanilla's Community Center completion ceremony. Handed to <c>eventsSeen</c> by the
    /// ending's "Keep playing" branch for the current run only.</summary>
    public const string CeremonyEventId = "191393";

    /// <summary>True when this event id is a current-run hand-off: written to <c>eventsSeen</c> on
    /// purpose, but never recorded in the cross-loop memory and never re-seeded after a reset.</summary>
    public static bool IsHandedOffOnly(string id)
        => string.Equals(id, CeremonyEventId, System.StringComparison.Ordinal);
}
