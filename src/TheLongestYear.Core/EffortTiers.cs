namespace TheLongestYear.Core;

/// <summary>Absolute difficulty bands for goal sampling (rule E). Tiers are fixed bands on the
/// effort scale, not relative to the pool being sampled.</summary>
public enum EffortTier { Easy, Medium, Hard, Extreme }

public static class EffortTiers
{
    public const int EasyMax = 2;
    public const int MediumMax = 5;
    public const int HardMax = 8;

    /// <summary>Absolute bands on the effort scale (spec 2026-08-28-obtainable-board, section 4):
    /// Easy 0 to 2, Medium 3 to 5, Hard 6 to 8, Extreme 9 and up. Relative quartiles made the
    /// hardest of two easy items Extreme and unaskable in Spring (review 2026-08-28).</summary>
    public static EffortTier Tier(int effort)
        => effort <= EasyMax ? EffortTier.Easy : effort <= MediumMax ? EffortTier.Medium : effort <= HardMax ? EffortTier.Hard : EffortTier.Extreme;

    /// <summary>Effort 6 or more (spec 2026-08-28-obtainable-board-2-stretch, section 3): the
    /// hard-item swap's threshold for "counts as this bundle's hard item".</summary>
    public static bool IsHard(int effort) => Tier(effort) >= EffortTier.Hard;

    /// <summary>The price-bucket fallback for an id no effort rule claims.</summary>
    public static EffortTier FromRarity(Rarity rarity) => rarity switch
    {
        Rarity.Common => EffortTier.Easy,
        Rarity.Uncommon => EffortTier.Medium,
        Rarity.Rare => EffortTier.Hard,
        _ => EffortTier.Extreme,
    };
}
