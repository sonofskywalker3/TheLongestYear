using System;
using System.Collections.Generic;

namespace TheLongestYear.Core;

/// <summary>At most <see cref="Max"/> weekly goals per theme list may come from <see cref="Ids"/>
/// (qualified item ids). Built from game data by the mod: fruit-tree fruits and crab-pot catches.</summary>
public sealed record GoalGroupCap(IReadOnlySet<string> Ids, int Max)
{
    /// <summary>The three jellies. Each is a slow, single-spot catch, so two of them in one week's
    /// goals is two separate fishing trips for one theme (Jeff, 2026-09-07: Sea Jelly and River
    /// Jelly both asked in week 1). At most one per week.</summary>
    public static readonly IReadOnlySet<string> JellyIds = new HashSet<string>(StringComparer.Ordinal)
    {
        "(O)SeaJelly", "(O)RiverJelly", "(O)CaveJelly",
    };
}
