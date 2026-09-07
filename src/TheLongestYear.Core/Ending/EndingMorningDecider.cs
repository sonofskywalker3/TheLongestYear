namespace TheLongestYear.Core.Ending;

public sealed record EndingSnapshot(
    bool Armed, bool WorldReady, bool OnFarm, bool Busy, bool FestivalToday, bool SeenMailPresent, bool StartedThisMorning);

public enum EndingAction { None, Start, Finish, ReArm }

/// <summary>Year One Ending (spec 2026-09-06 §2): the driver's per-tick decision. Busy = any event,
/// farm event, queued warp or open menu. Pure.</summary>
public static class EndingMorningDecider
{
    public static EndingAction Next(EndingSnapshot s)
    {
        if (!s.Armed || !s.WorldReady) return EndingAction.None;
        if (s.SeenMailPresent && !s.Busy) return EndingAction.Finish;
        if (s.Busy) return EndingAction.None;
        if (s.StartedThisMorning) return EndingAction.ReArm;   // event ended, no seen mail
        if (!s.OnFarm || s.FestivalToday) return EndingAction.None;
        return EndingAction.Start;
    }
}
