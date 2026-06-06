using StardewValley;
using StardewValley.Locations;
using TheLongestYear.Core;
using TheLongestYear.Donations;

namespace TheLongestYear.Integration
{
    /// <summary>
    /// Reconciles the run's vault ledger from the vanilla CC's own paid-state — the source of
    /// truth for whether a money bundle has been paid. Additive only: it unions any vanilla-complete
    /// vault bundle (34–37) into <see cref="RunState.VaultBundlesPaid"/> via the idempotent
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
            var dict = Game1.netWorldState.Value?.Bundles?.FieldDict;
            if (dict == null) return;

            foreach (int idx in VaultRules.VaultIndices)
            {
                // Guard: isBundleComplete indexes bundles[idx] directly and throws
                // KeyNotFoundException if the index isn't present (see WorldResetService notes).
                if (!dict.ContainsKey(idx)) continue;
                if (cc.isBundleComplete(idx))
                    DonationService.Active?.OnVaultBundlePaid(idx);
            }
        }
    }
}
