namespace TheLongestYear.Core.Intro
{
    /// <summary>
    /// Identifiers for the day-1 narrative intro. The intro is a single event the driver starts
    /// explicitly (it uses the in-event <c>changeLocation</c> command to move from the farm porch
    /// to the Community Center), so there are no Data/Events preconditions to get wrong anymore.
    ///
    /// <para><see cref="CcSeenMail"/> is added at the end of the event and promoted to the
    /// cross-run <c>MetaState.HasSeenIntro</c> on save; <see cref="IntroDoneMail"/> is the legacy
    /// per-run suppression flag kept for <c>tly_replayintro</c> bookkeeping.</para>
    /// </summary>
    public static class IntroEventKeys
    {
        /// <summary>Event id used for the combined intro event (lands in player.eventsSeen on completion).</summary>
        public const string IntroEventId = "tly_intro";

        public const string CcSeenMail    = "tly_intro_cc_seen";
        public const string IntroDoneMail = "tly_intro_done";
    }
}
