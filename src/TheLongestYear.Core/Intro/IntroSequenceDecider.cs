namespace TheLongestYear.Core.Intro
{
    /// <summary>What the driver should do this tick.</summary>
    public enum IntroAction
    {
        None,        // not a fresh-intro context — let the normal flow run
        Waiting,     // an event is playing (or just ended) — do nothing
        StartIntro,  // fresh morning, intro not played yet — start the combined intro event
        OpenPicker   // intro done — open the theme picker
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

    /// <summary>Pure step machine. The single intro event ends by setting CcSeen, which is how the
    /// driver knows the cutscene is finished and the picker should open.</summary>
    public static class IntroSequenceDecider
    {
        public static IntroAction Next(IntroSnapshot s)
        {
            if (!IntroGate.IsFreshIntroMorning(s.HasSeenIntro, s.Season, s.DayOfMonth))
                return IntroAction.None;
            if (s.EventActive)
                return IntroAction.Waiting;
            if (!s.CcSeen)
                return IntroAction.StartIntro;
            return IntroAction.OpenPicker;
        }
    }
}
