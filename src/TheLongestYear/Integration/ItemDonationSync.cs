using System.Linq;
using StardewValley;
using TheLongestYear.Core;

namespace TheLongestYear.Integration
{
    /// <summary>
    /// Mirrors the run's per-slot donation ledger from the vanilla CC's own bundle state, the
    /// source of truth for what the player has deposited (spec 2026-08-29-per-slot-ledger). Whole
    /// replace, never a union: the ledger can lag the board (the live DonationObserver only sees
    /// deposits while the JunimoNoteMenu is open) but can never be ahead of it. Runs on save load
    /// (which is also the migration from the old id-only ledger), before the Season Goals page and
    /// before the day-end gate, so the page and the gate always judge the same board.
    ///
    /// JP is deliberately NOT awarded here: the live observer already paid for what it caught.
    /// Single-player + master + TLY-active only. Returns the number of filled slots mirrored, or
    /// -1 when the board was unavailable and the ledger was left untouched. The pure slot
    /// derivation lives in <see cref="CcDonationReconciler"/>.
    /// </summary>
    internal static class ItemDonationSync
    {
        public static int Reconcile(RunState run)
        {
            if (run == null) return -1;
            if (!RunActivation.IsActive) return -1;
            if (!Game1.IsMasterGame || Game1.IsMultiplayer) return -1;

            var worldState = Game1.netWorldState?.Value;
            var bundleData = worldState?.BundleData;
            var bundles = worldState?.Bundles;
            if (bundleData == null || bundles?.FieldDict == null) return -1;

            // NetBundles' indexer returns the bool[] slot array directly; FieldDict.ContainsKey is
            // the safe presence check (indexing a missing key would throw, see VaultPaymentSync).
            var slots = CcDonationReconciler.DonatedSlots(
                bundleData,
                idx => bundles.FieldDict.ContainsKey(idx) ? bundles[idx] : null).ToList();
            run.ReplaceDonations(slots);
            return slots.Count;
        }
    }
}
