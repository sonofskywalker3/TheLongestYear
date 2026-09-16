using System.Collections.Generic;

namespace TheLongestYear.Core.Rewind;

/// <summary>When the rewind (bedroom, Town pan, morning) offers the player a skip. Same rule as the
/// season turns: from the second showing on a save (Jeff, 2026-09-16: "I've seen each scene
/// multiple times but I can't get a skip button to show up for any of it"). A save that has already
/// finished a loop counts as having seen it, so saves from before the seen flag existed get the
/// button straight away.</summary>
public static class RewindSkipRule
{
    /// <summary>The entry recorded in <see cref="MetaState.SeasonTurnsSeen"/> once a rewind has played.</summary>
    public const string SeenName = "Rewind";

    public static bool IsSkippable(IReadOnlySet<string>? seen, int completedResets)
        => completedResets > 0 || (seen != null && seen.Contains(SeenName));
}
