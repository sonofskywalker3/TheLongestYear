namespace TheLongestYear.Core.Intro
{
    /// <summary>What the driver should do this tick.</summary>
    public enum IntroAction
    {
        None,        // not a fresh-intro context — let the normal flow run
        Waiting,     // a cutscene is playing — do nothing
        StartPorch,  // warp to the Farm so the porch (Lewis) event fires
        WarpToCc,    // porch done — warp into the Community Center so the Junimo event fires
        OpenPicker   // both cutscenes done — warp home and open the theme picker
    }

    /// <summary>Immutable snapshot of the inputs the decider needs (no game refs).</summary>
    public readonly record struct IntroSnapshot(
        bool HasSeenIntro,
        Season Season,
        int DayOfMonth,
        bool PorchSeen,
        bool CcSeen,
        bool EventActive);

    /// <summary>Shared gate: is this the one morning the intro chain should own?</summary>
    public static class IntroGate
    {
        public static bool IsFreshIntroMorning(bool hasSeenIntro, Season season, int dayOfMonth)
            => !hasSeenIntro && season == Season.Spring && dayOfMonth == 1;
    }

    /// <summary>Pure step machine. Mail flags (PorchSeen/CcSeen) are the progression state.</summary>
    public static class IntroSequenceDecider
    {
        public static IntroAction Next(IntroSnapshot s)
        {
            if (!IntroGate.IsFreshIntroMorning(s.HasSeenIntro, s.Season, s.DayOfMonth))
                return IntroAction.None;
            if (s.EventActive)
                return IntroAction.Waiting;
            if (!s.PorchSeen)
                return IntroAction.StartPorch;
            if (!s.CcSeen)
                return IntroAction.WarpToCc;
            return IntroAction.OpenPicker;
        }
    }
}
