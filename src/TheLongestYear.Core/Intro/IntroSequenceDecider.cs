namespace TheLongestYear.Core.Intro
{
    /// <summary>What the driver should do this tick.</summary>
    public enum IntroAction
    {
        None,            // not a fresh-intro context
        Waiting,         // an event is playing (or just ended)
        WaitForOpening,  // fresh morning, flag not planted yet: the arrival event (or the skip) will plant it
        OpenPicker       // flag present: open the theme picker
    }

    /// <summary>Immutable snapshot of the inputs the decider needs (no game refs).</summary>
    public readonly record struct IntroSnapshot(
        bool HasSeenIntro,
        Season Season,
        int DayOfMonth,
        bool CcSeen,
        bool EventActive);

    /// <summary>Shared gate: is this the one morning the intro chain should own?</summary>
    public static class IntroGate
    {
        public static bool IsFreshIntroMorning(bool hasSeenIntro, Season season, int dayOfMonth)
            => !hasSeenIntro && season == Season.Spring && dayOfMonth == 1;
    }

    /// <summary>Pure step machine. The opening is vanilla's arrival event, replaced by
    /// OpeningEventInjector; it ends by adding the cc-seen flag, as does the Skip intro path. The
    /// driver never starts an event; it waits for the flag and opens the picker.</summary>
    public static class IntroSequenceDecider
    {
        public static IntroAction Next(IntroSnapshot s)
        {
            if (!IntroGate.IsFreshIntroMorning(s.HasSeenIntro, s.Season, s.DayOfMonth))
                return IntroAction.None;
            if (s.EventActive)
                return IntroAction.Waiting;
            if (!s.CcSeen)
                return IntroAction.WaitForOpening;
            return IntroAction.OpenPicker;
        }
    }
}
