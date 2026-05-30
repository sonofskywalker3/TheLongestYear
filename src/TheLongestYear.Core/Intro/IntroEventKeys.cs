namespace TheLongestYear.Core.Intro
{
    /// <summary>
    /// Single source of truth for the day-1 narrative intro: event ids, the per-run /
    /// cross-run mail-flag names, and the fully-formed Data/Events keys (id + preconditions).
    ///
    /// Precondition letters matter: <c>u</c>=DayOfMonth, <c>Season</c>=season, <c>n</c>=has-mail,
    /// <c>!n</c>=not-mail. The original keys used <c>D</c>/<c>s</c>/<c>m</c>, which the game maps to
    /// Dating/Shipped/EarnedMoney — so both events silently failed every precondition check and
    /// never fired. Do NOT reintroduce single-letter D/s/m here.
    /// </summary>
    public static class IntroEventKeys
    {
        public const string PorchEventId = "tly_intro_porch";
        public const string CcEventId    = "tly_intro_cc";

        public const string PorchSeenMail = "tly_intro_porch_seen";
        public const string CcSeenMail     = "tly_intro_cc_seen";
        public const string IntroDoneMail  = "tly_intro_done";

        // Porch: Spring 1, porch not seen this run, intro not done cross-run.
        public static string PorchKey =>
            $"{PorchEventId}/u 1/Season spring/!n {PorchSeenMail}/!n {IntroDoneMail}";

        // CC: gated on the porch event having fired this run (forward-only chain), cc not seen, intro not done.
        public static string CcKey =>
            $"{CcEventId}/n {PorchSeenMail}/!n {CcSeenMail}/!n {IntroDoneMail}";
    }
}
