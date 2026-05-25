using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core;

/// <summary>
/// One weekly-championable contract: the items of a single theme that must be donated in a
/// single season, plus the bonus/liability ids active when this theme is championed.
/// Satisfaction is cumulative — any of the run's donated item ids count, by design (spec §6).
/// </summary>
public sealed class Contract
{
    public Season Season { get; }
    public Theme Theme { get; }
    public IReadOnlyList<string> RequiredItemIds { get; }
    public string BonusId { get; }
    public string LiabilityId { get; }

    public Contract(Season season, Theme theme, IEnumerable<string> requiredItemIds, string bonusId, string liabilityId)
    {
        Season = season;
        Theme = theme;
        RequiredItemIds = requiredItemIds?.ToList() ?? throw new ArgumentNullException(nameof(requiredItemIds));
        BonusId = bonusId ?? throw new ArgumentNullException(nameof(bonusId));
        LiabilityId = liabilityId ?? throw new ArgumentNullException(nameof(liabilityId));
    }

    /// <summary>True when every required item id is present in the run's cumulative donations.</summary>
    public bool IsSatisfiedBy(ISet<string> donatedItemIds)
        => RequiredItemIds.All(donatedItemIds.Contains);
}
