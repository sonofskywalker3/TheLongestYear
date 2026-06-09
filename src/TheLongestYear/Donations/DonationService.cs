using System;
using System.Linq;
using StardewModdingAPI;
using TheLongestYear.Core;

namespace TheLongestYear.Donations
{
    /// <summary>
    /// Applies the JP economy to real Community Center activity: rarity-scaled JP per donated item,
    /// the donated id into the run ledger (so the gate responds), one-time bundle/room
    /// completion bonuses, and the weekly-selection bonus-list multiplier. JP lands in MetaState
    /// (committed with the next save — never eagerly).
    /// </summary>
    internal sealed class DonationService
    {
        /// <summary>Set on save load so the static Harmony patches can reach the live service.</summary>
        internal static DonationService Active;

        private readonly IMonitor _monitor;
        private readonly MetaStore _store;
        private readonly GameplayConfig _config;
        private readonly JpCalculator _jp;

        /// <summary>Fires after a successful CC donation has been recorded to the ledger + JP
        /// awarded. WeeklyThemeQuestService subscribes to refresh the per-week quest's progress
        /// text (and auto-complete when all 4 bonus items are donated). Kept as an Action rather
        /// than a typed event so the donation layer stays free of cross-package references.</summary>
        public Action AfterDonation;

        public DonationService(IMonitor monitor, MetaStore store, GameplayConfig config)
        {
            _monitor = monitor;
            _store = store;
            _config = config;
            _jp = new JpCalculator(config.Jp);
        }

        private RunState Run => _store.Run;

        /// <summary>A successful donation of <paramref name="count"/> of an item to the CC.
        /// Pays base rarity JP; if the player has a selected theme this week AND the donated id
        /// is in that week's bonus list, the JP is multiplied by SelectionBonusMultiplier.</summary>
        public void OnItemDonated(string qualifiedItemId, int count)
        {
            if (string.IsNullOrEmpty(qualifiedItemId) || count <= 0)
                return;

            Rarity rarity = ItemRarityResolver.Resolve(qualifiedItemId, _config.RarityThresholds);
            long baseJp = _jp.PerItem(rarity, Run.WeekOfYear) * count;

            bool bonusApplies = IsSelectedBonusItem(qualifiedItemId);
            long awarded = bonusApplies
                ? (long)Math.Round(baseJp * _config.SelectionBonusMultiplier, MidpointRounding.AwayFromZero)
                : baseJp;
            awarded = JpBoostHelper.Apply(_store.State, awarded);

            _store.State.JunimoPoints += awarded;
            Run.RecordDonation(qualifiedItemId);

            string bonusTag = bonusApplies ? $" (bonus x{_config.SelectionBonusMultiplier})" : "";
            int jpBoostTier = JpBoostHelper.HighestTier(_store.State);
            string boostTag = jpBoostTier > 0 ? $" (jp_boost tier {jpBoostTier})" : "";
            // Per-item donation line is Trace: a full CC restoration donates dozens-to-hundreds of
            // items per run, which would otherwise flood SMAPI-latest.txt (the log testers attach to
            // bug reports). Milestone logs (bundle/room complete, run/month/reset) stay at Info.
            _monitor.Log(
                $"Donated {count}x {qualifiedItemId} ({rarity}) -> +{awarded} JP{bonusTag}{boostTag} (now {_store.State.JunimoPoints}).",
                LogLevel.Trace);

            AfterDonation?.Invoke();
        }

        /// <summary>True if the player has a selected theme this week AND the donated id is in
        /// this week's sampled bonus list (see <see cref="BonusItemSampler"/>). The list is
        /// populated by <c>RunController.PopulateBonusItemsForCurrentSelection</c> at selection
        /// time and persists in <see cref="RunState.CurrentWeekBonusItems"/>.</summary>
        private bool IsSelectedBonusItem(string itemId)
        {
            if (!Run.CurrentSelection.HasValue) return false;
            // Egg color-variants are interchangeable for the bonus (174≡182, 176≡180) — match the
            // weekly-quest credit so donating either shade also earns the 1.5x JP. See CcItemEquivalence.
            return Run.CurrentWeekBonusItems.Exists(b => CcItemEquivalence.Matches(b, itemId));
        }

        /// <summary>A bundle just completed — award its one-time completion bonus (season-scaled).</summary>
        public void OnBundleCompleted(int bundleIndex)
        {
            if (!Run.TryMarkBundleAwarded(bundleIndex))
                return;

            long bonus = JpBoostHelper.Apply(_store.State, _jp.BundleBonus(Run.WeekOfYear));
            _store.State.JunimoPoints += bonus;
            _monitor.Log(
                $"Bundle {bundleIndex} complete -> +{bonus} JP (now {_store.State.JunimoPoints}).",
                LogLevel.Info);
        }

        /// <summary>The player paid a real CC Vault money bundle (this save's actual index). Records it into the
        /// run ledger (idempotent) and awards JP proportional to the gold spent. Unlike a normal
        /// bundle there is NO completion bonus — the vault is a one-item (money) bundle, so it pays
        /// only the gold-scaled amount. No RunActivation check: <see cref="Active"/> is null on
        /// non-TLY saves and the reconcile caller gates, matching OnItemDonated/OnBundleCompleted.</summary>
        public void OnVaultBundlePaid(int bundleIndex)
        {
            if (!Run.TryMarkVaultBundlePaid(bundleIndex))
                return;

            int gold = Integration.VaultBundleMap.GoldForIndex(bundleIndex);
            long jp = JpBoostHelper.Apply(_store.State, _jp.VaultPayment(gold));
            _store.State.JunimoPoints += jp;
            _monitor.Log(
                $"Vault bundle {bundleIndex} paid ({gold:N0}g) -> +{jp} JP (now {_store.State.JunimoPoints}).",
                LogLevel.Info);
        }

        /// <summary>A room/area just completed — award its one-time completion bonus (season-scaled).</summary>
        public void OnRoomCompleted(int area)
        {
            if (!Run.TryMarkRoomAwarded(area))
                return;

            long bonus = JpBoostHelper.Apply(_store.State, _jp.RoomBonus(Run.WeekOfYear));
            _store.State.JunimoPoints += bonus;
            _monitor.Log(
                $"Room {area} complete -> +{bonus} JP (now {_store.State.JunimoPoints}).",
                LogLevel.Info);
        }
    }
}
