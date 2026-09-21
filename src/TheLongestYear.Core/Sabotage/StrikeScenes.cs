using System;
using System.Collections.Generic;

namespace TheLongestYear.Core.Sabotage;

/// <summary>When an overnight strike scene plays (spec 2026-09-21).</summary>
public static class StrikeScenes
{
    public static bool IsDue(DarknessEvent e, IReadOnlyCollection<string> playedThisLoop)
    {
        if (playedThisLoop is null) throw new ArgumentNullException(nameof(playedThisLoop));
        return !Contains(playedThisLoop, e.ToString());
    }

    public static bool IsSkippable(DarknessEvent e, IReadOnlyCollection<string> seenOnSave)
    {
        if (seenOnSave is null) throw new ArgumentNullException(nameof(seenOnSave));
        return Contains(seenOnSave, e.ToString());
    }

    public static void MarkPlayed(DarknessEvent e, RunState run, MetaState meta)
    {
        if (run is null) throw new ArgumentNullException(nameof(run));
        if (meta is null) throw new ArgumentNullException(nameof(meta));
        (run.StrikeScenesPlayed ??= new()).Add(e.ToString());
        (meta.StrikeScenesSeen ??= new()).Add(e.ToString());
    }

    private static bool Contains(IReadOnlyCollection<string> set, string name)
    {
        foreach (string s in set) if (s == name) return true;
        return false;
    }
}
