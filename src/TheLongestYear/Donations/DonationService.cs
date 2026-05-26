using System;
using System.Linq;
using StardewModdingAPI;
using TheLongestYear.Core;

namespace TheLongestYear.Donations
{
    /// <summary>
    /// Applies the JP economy to real Community Center activity: rarity-scaled JP per donated item,
    /// the donated id into the run ledger (so contracts/gates respond), one-time bundle/room
    /// completion bonuses, and the championship bonus-list multiplier. JP lands in MetaState
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

        /// <summary>Optional callback that returns the current YearPlan. Wired by ModEntry/RunController
        /// after construction so DonationService can look up the championed contract's bonus list.</summary>
        public Func<YearPlan> CurrentPlanProvider { get; set; }

        public DonationService(IMonitor monitor, MetaStore store, GameplayConfig config)
        {
            _monitor = monitor;
            _store = store;
            _config = config;
            _jp = new JpCalculator(config.Jp);
        }

        private RunState Run => _store.Run;

        /// <summary>A successful donation of <paramref name="count"/> of an item to the CC.
        /// Pays base rarity JP; if the player has a championed theme this week AND the donated
        /// id is in that contract's bonus list, the JP is multiplied by ChampionBonusMultiplier.</summary>
        public void OnItemDonated(string qualifiedItemId, int count)
        {
            if (string.IsNullOrEmpty(qualifiedItemId) || count <= 0)
                return;

            Rarity rarity = ItemRarityResolver.Resolve(qualifiedItemId, _config.RarityThresholds);
            long baseJp = _jp.PerItem(rarity, Run.WeekOfYear) * count;

            bool bonusApplies = IsChampionedBonusItem(qualifiedItemId);
            long awarded = bonusApplies
                ? (long)Math.Round(baseJp * _config.ChampionBonusMultiplier, MidpointRounding.AwayFromZero)
                : baseJp;

            _store.State.JunimoPoints += awarded;
            Run.RecordDonation(qualifiedItemId);

            string bonusTag = bonusApplies ? $" (bonus x{_config.ChampionBonusMultiplier})" : "";
            _monitor.Log(
                $"Donated {count}x {qualifiedItemId} ({rarity}) -> +{awarded} JP{bonusTag} (now {_store.State.JunimoPoints}).",
                LogLevel.Info);
        }

        /// <summary>True if the player has a championed theme this week, the YearPlan is available,
        /// and the donated id is in that (season, theme) contract's bonus list.</summary>
        private bool IsChampionedBonusItem(string itemId)
        {
            if (!Run.CurrentChampion.HasValue) return false;
            YearPlan plan = CurrentPlanProvider?.Invoke();
            if (plan == null) return false;
            Contract contract = plan.Get(Run.Season, Run.CurrentChampion.Value);
            return contract.BonusItemIds.Contains(itemId);
        }

        /// <summary>A bundle just completed — award its one-time completion bonus (season-scaled).</summary>
        public void OnBundleCompleted(int bundleIndex)
        {
            if (!Run.TryMarkBundleAwarded(bundleIndex))
                return;

            long bonus = _jp.BundleBonus(Run.WeekOfYear);
            _store.State.JunimoPoints += bonus;
            _monitor.Log(
                $"Bundle {bundleIndex} complete -> +{bonus} JP (now {_store.State.JunimoPoints}).",
                LogLevel.Info);
        }

        /// <summary>A room/area just completed — award its one-time completion bonus (season-scaled).</summary>
        public void OnRoomCompleted(int area)
        {
            if (!Run.TryMarkRoomAwarded(area))
                return;

            long bonus = _jp.RoomBonus(Run.WeekOfYear);
            _store.State.JunimoPoints += bonus;
            _monitor.Log(
                $"Room {area} complete -> +{bonus} JP (now {_store.State.JunimoPoints}).",
                LogLevel.Info);
        }
    }
}
