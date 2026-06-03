namespace TheLongestYear.Core.Day28
{
    /// <summary>Which day-28 bedtime branch is queued (set by RunController.OnDayEnding from the
    /// gate's RunAction). None = no cutscene this morning.</summary>
    public enum Day28Branch
    {
        None,
        Fail,     // gate closed → rewind dialogue → JP shop → reset to Spring 1
        Continue  // gate open → congratulations → roll into the next season
    }

    /// <summary>What the driver should do this tick.</summary>
    public enum Day28Action
    {
        None,            // nothing pending — let the normal flow run
        Waiting,         // an event is playing (or just ended) — do nothing this tick
        StartCutscene,   // pending + not started → start the forced Junimo event
        RunContinuation  // event finished → run the branch continuation (shop+reset / continue)
    }

    /// <summary>Immutable snapshot of the inputs the decider needs (no game refs).
    /// <paramref name="EventActive"/> folds in the post-end window the way the intro does.</summary>
    public readonly record struct Day28Snapshot(
        Day28Branch Branch,
        bool Started,
        bool EventActive);

    /// <summary>Pure step machine mirroring <c>IntroSequenceDecider</c>. The driver owns the
    /// settled-frame / menu / cooldown guards; this only sequences start → wait → continue.</summary>
    public static class Day28CutsceneDecider
    {
        public static Day28Action Next(Day28Snapshot s)
        {
            if (s.Branch == Day28Branch.None)
                return Day28Action.None;
            if (s.EventActive)
                return Day28Action.Waiting;     // covers "before start, something else is up" and "just ended"
            if (!s.Started)
                return Day28Action.StartCutscene;
            return Day28Action.RunContinuation; // started + no event active → the scene has ended
        }
    }
}
