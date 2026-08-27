namespace TheLongestYear.Core;

/// <summary>
/// The pure rule for buying a permanent upgrade. Mutates <see cref="MetaState"/> only on
/// <see cref="PurchaseResult.Success"/>. JP persists with the game save via the existing
/// MetaStore.Saving hook — Plan 05 never writes save data eagerly.
/// </summary>
public static class UpgradePurchase
{
    public enum PurchaseResult
    {
        /// <summary>Purchase recorded; JP deducted; upgrade id added to OwnedUpgrades.</summary>
        Success,
        /// <summary>The upgrade definition was null (e.g. unknown id from a debug command).</summary>
        NotInCatalog,
        /// <summary>The player already owns this upgrade.</summary>
        AlreadyOwned,
        /// <summary>The upgrade has a prerequisite the player does not yet own.</summary>
        PrerequisiteMissing,
        /// <summary>The upgrade requires a meta-state condition the player has not satisfied
        /// (e.g. "Start with Chicken" requires ever having owned a chicken).</summary>
        MetaRequirementMissing,
        /// <summary>The player does not have enough Junimo Points.</summary>
        NotEnoughJp
    }

    /// <param name="priceFactor">The run's difficulty shrine-price factor (spec 2026-08-26).
    /// The affordability check and the deduction BOTH use it, via the same
    /// <see cref="UpgradePricing"/> call, so what the menu shows is what the player pays.</param>
    public static PurchaseResult TryPurchase(
        MetaState state, UpgradeDefinition? definition, double priceFactor = 1.0)
    {
        if (definition == null)
            return PurchaseResult.NotInCatalog;
        if (state.HasUpgrade(definition.Id))
            return PurchaseResult.AlreadyOwned;
        if (definition.PrerequisiteId != null && !state.HasUpgrade(definition.PrerequisiteId))
            return PurchaseResult.PrerequisiteMissing;
        if (!state.MeetsMetaRequirement(definition.MetaRequirement))
            return PurchaseResult.MetaRequirementMissing;
        long cost = UpgradePricing.EffectiveCost(definition, priceFactor);
        if (state.JunimoPoints < cost)
            return PurchaseResult.NotEnoughJp;

        state.JunimoPoints -= cost;
        state.OwnedUpgrades.Add(definition.Id);
        return PurchaseResult.Success;
    }
}
