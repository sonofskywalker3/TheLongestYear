using StardewValley;
using TheLongestYear.Core;

namespace TheLongestYear.Integration
{
    /// <summary>
    /// Reconciles the run's item-donation ledger from the vanilla CC's own per-slot bundle state —
    /// the source of truth for what the player has actually deposited. Additive + idempotent: it
    /// unions every completed concrete ingredient into <see cref="RunState.DonatedItemIds"/> via
    /// <see cref="RunState.RecordDonation"/> (a set-add); it never removes and never awards JP.
    ///
    /// The item analogue of <see cref="VaultPaymentSync"/>. Backstops the live
    /// <c>DonationObserver</c>, which only sees deposits while the JunimoNoteMenu is open and can
    /// miss one — leaving the season gate to read "failed" though vanilla shows the bundle complete
    /// (beta report, khauser13). Called before the day-end gate eval so the gate trusts the game's
    /// authoritative state, not the lossy observer. Single-player + master + TLY-active only.
    ///
    /// JP is deliberately NOT awarded here (unlike a fresh observer-caught donation): this is a
    /// ledger backstop for the gate, and the live observer already paid JP for what it caught. The
    /// pure id derivation lives in <see cref="CcDonationReconciler"/>.
    /// </summary>
    internal static class ItemDonationSync
    {
        public static void Reconcile(RunState run)
        {
            if (run == null) return;
            if (!RunActivation.IsActive) return;
            if (!Game1.IsMasterGame || Game1.IsMultiplayer) return;

            var worldState = Game1.netWorldState?.Value;
            var bundleData = worldState?.BundleData;
            var bundles = worldState?.Bundles;
            if (bundleData == null || bundles?.FieldDict == null) return;

            // NetBundles' indexer returns the bool[] slot array directly; FieldDict.ContainsKey is
            // the safe presence check (indexing a missing key would throw — see VaultPaymentSync).
            foreach (string id in CcDonationReconciler.DonatedConcreteIds(
                bundleData,
                idx => bundles.FieldDict.ContainsKey(idx) ? bundles[idx] : null))
            {
                // Cumulative-ledger only: this unions historical deposits, so it must not touch the
                // weekly bonus-suppression list (RecordDonation would).
                run.RecordCumulativeDonation(id);
            }
        }
    }
}
