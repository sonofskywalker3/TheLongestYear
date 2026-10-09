using StardewValley;
using StardewValley.Locations;
using TheLongestYear.Core;
using TheLongestYear.Donations;

namespace TheLongestYear.Integration
{
    /// <summary>
    /// Reconciles the run's vault ledger from the vanilla CC's own paid-state, the source of
    /// truth for whether a money bundle has been paid. Additive only: it unions any vanilla-complete
    /// vault bundle (this save's actual indices, remix-aware) into <see cref="RunState.VaultBundlesPaid"/> via the idempotent
    /// <see cref="DonationService.OnVaultBundlePaid"/>; it never removes.
    ///
    /// Backstops the live <see cref="DonationObserver"/> path for two cases the observer can't see:
    ///   - a payment made on an OLDER mod version (already complete on load, so no false→true
    ///     transition to observe) — the mid-run upgrade migration,
    ///   - any in-session payment the observer missed.
    /// Called before the day-end gate eval, the green journal, and the shrine (all need an accurate
    /// ledger). Single-player + master + TLY-active only.
    /// </summary>
    internal static class VaultPaymentSync
    {
        public static void Reconcile(RunState run)
        {
            if (run == null) return;
            if (!RunActivation.IsActive) return;
            if (!Game1.IsMasterGame || Game1.IsMultiplayer) return;

            if (Game1.getLocationFromName("CommunityCenter") is not CommunityCenter cc) return;
            if (Game1.netWorldState.Value?.Bundles?.FieldDict == null) return;

            // Read slot 0, the flag vanilla's purchase button sets, NOT cc.isBundleComplete: that
            // wants all three slots of the money bundle's array true, and vanilla never sets the
            // other two, so it never saw a paid Vault bundle (VaultRules.IsMoneyBundlePaid).
            // OnVaultBundlePaid is idempotent per index (RunState.TryMarkVaultBundlePaid), so a
            // bundle the live DonationObserver already paid for earns no second JP.
            foreach (int idx in VaultRules.PaidOnBoard(VaultBundleMap.Indices(), cc.bundlesDict()))
                DonationService.Active?.OnVaultBundlePaid(idx);
        }
    }
}
