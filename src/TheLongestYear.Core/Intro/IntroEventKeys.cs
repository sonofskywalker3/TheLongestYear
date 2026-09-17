namespace TheLongestYear.Core.Intro
{
    /// <summary>
    /// Cross-run bookkeeping flags for the opening. <see cref="IntroEventId"/> is recorded in
    /// player.eventsSeen by <c>ClearIntroState</c> for <c>tly_replayintro</c> cleanup.
    /// <see cref="CcSeenMail"/> is added when the opening finishes and promoted to the cross-run
    /// <c>MetaState.HasSeenIntro</c> on save; <see cref="IntroDoneMail"/> is the legacy
    /// per-run suppression flag kept for replay bookkeeping.
    /// </summary>
    public static class IntroEventKeys
    {
        /// <summary>Event id used for the combined intro event (lands in player.eventsSeen on completion).</summary>
        public const string IntroEventId = "tly_intro";

        public const string CcSeenMail    = "tly_intro_cc_seen";
        public const string IntroDoneMail = "tly_intro_done";
    }
}
