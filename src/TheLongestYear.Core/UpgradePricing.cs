using System;

namespace TheLongestYear.Core;

/// <summary>The single home of what a shrine upgrade actually costs.
///
/// Every read of <see cref="UpgradeDefinition.Cost"/> that a player can see or be charged goes
/// through here, so the displayed price and the charged price cannot disagree. That is not
/// hypothetical tidiness: 0.14.2 shipped a fix for exactly this shape of bug in Shop Discount,
/// where the posted shelf price and the gold actually deducted came from two different code paths
/// and vanilla gated the sale on the undiscounted number.
///
/// Spec 2026-08-26 difficulty-modifiers, section 3.6.</summary>
public static class UpgradePricing
{
    /// <summary>The JP price charged and displayed for an upgrade. Rounded away from zero, floor
    /// 0. A free upgrade stays free at every step, because zero times anything is zero.</summary>
    public static long EffectiveCost(UpgradeDefinition definition, double factor)
    {
        if (definition == null) throw new ArgumentNullException(nameof(definition));
        if (factor == 1.0)
            return definition.Cost;

        long scaled = (long)Math.Round(definition.Cost * factor, MidpointRounding.AwayFromZero);
        return scaled < 0 ? 0 : scaled;
    }

    /// <summary>Convenience overload for callers holding the run's stamped profile.</summary>
    /// <summary>Price for THIS player: Gifts of the Junimos read the shared ladder
    /// (<see cref="GiftLadder.CostFor"/>) instead of their catalog cost, then the factor applies.</summary>
    public static long EffectiveCost(UpgradeDefinition definition, double factor, MetaState state)
    {
        if (definition == null) throw new ArgumentNullException(nameof(definition));
        if (state == null || !GiftLadder.IsGift(definition))
            return EffectiveCost(definition, factor);
        long baseCost = GiftLadder.CostFor(state);
        if (factor == 1.0) return baseCost;
        long scaled = (long)Math.Round(baseCost * factor, MidpointRounding.AwayFromZero);
        return scaled < 0 ? 0 : scaled;
    }

    public static long EffectiveCost(UpgradeDefinition definition, DifficultyProfile profile)
    {
        if (profile == null) throw new ArgumentNullException(nameof(profile));
        return EffectiveCost(definition, profile.ShrinePriceFactor);
    }
}
