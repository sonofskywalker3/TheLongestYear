using System.Collections.Generic;

namespace TheLongestYear.Core;

/// <summary>At most <see cref="Max"/> weekly goals per theme list may come from <see cref="Ids"/>
/// (qualified item ids). Built from game data by the mod: fruit-tree fruits and crab-pot catches.</summary>
public sealed record GoalGroupCap(IReadOnlySet<string> Ids, int Max);
