using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core;

/// <summary>Relative difficulty bands for goal sampling (rule E). Tiers are quartiles of effort
/// within the pool being sampled, so they are relative to what that theme can ask.</summary>
public enum EffortTier { Easy, Medium, Hard, Extreme }

/// <summary>Inclusive upper effort bound of each of the first three tiers; above Hard is Extreme.</summary>
public sealed record TierCutoffs(int Easy, int Medium, int Hard);

public static class EffortTiers
{
    private const int QuartileCount = 4;

    public static TierCutoffs Cutoffs(IReadOnlyCollection<int> efforts)
    {
        if (efforts == null) throw new ArgumentNullException(nameof(efforts));
        if (efforts.Count == 0)
            return new TierCutoffs(int.MaxValue, int.MaxValue, int.MaxValue);
        List<int> sorted = efforts.OrderBy(e => e).ToList();
        int Quartile(int k) => sorted[Math.Max(0, k * sorted.Count / QuartileCount - 1)];
        return new TierCutoffs(Quartile(1), Quartile(2), Quartile(3));
    }

    public static EffortTier Tier(int effort, TierCutoffs cutoffs)
    {
        if (cutoffs == null) throw new ArgumentNullException(nameof(cutoffs));
        if (effort <= cutoffs.Easy) return EffortTier.Easy;
        if (effort <= cutoffs.Medium) return EffortTier.Medium;
        if (effort <= cutoffs.Hard) return EffortTier.Hard;
        return EffortTier.Extreme;
    }

    /// <summary>The price-bucket fallback for an id no effort rule claims.</summary>
    public static EffortTier FromRarity(Rarity rarity) => rarity switch
    {
        Rarity.Common => EffortTier.Easy,
        Rarity.Uncommon => EffortTier.Medium,
        Rarity.Rare => EffortTier.Hard,
        _ => EffortTier.Extreme,
    };
}
