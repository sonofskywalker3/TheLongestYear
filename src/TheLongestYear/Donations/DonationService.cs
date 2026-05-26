using StardewModdingAPI;
using TheLongestYear.Core;

namespace TheLongestYear.Donations
{
    /// <summary>
    /// Applies the JP economy to real Community Center activity: rarity-scaled JP per donated item,
    /// the donated id into the run ledger (so contracts/gates respond), and one-time bundle/room
    /// completion bonuses. JP lands in MetaState (committed with the next save — never eagerly).
    /// </summary>
    internal sealed class DonationService
    {
        /// <summary>Set on save load so the static Harmony patches can reach the live service.</summary>
        internal static DonationService Active;

        private readonly IMonitor _monitor;
        private readonly MetaStore _store;
        private readonly GameplayConfig _config;
        private readonly JpCalculator _jp;

        public DonationService(IMonitor monitor, MetaStore store, GameplayConfig config)
        {
            _monitor = monitor;
            _store = store;
            _config = config;
            _jp = new JpCalculator(config.Jp);
        }

        private RunState Run => _store.Run;

        /// <summary>A successful donation of <paramref name="count"/> of an item to the CC.</summary>
        public void OnItemDonated(string qualifiedItemId, int count)
        {
            if (string.IsNullOrEmpty(qualifiedItemId) || count <= 0)
                return;

            Rarity rarity = ItemRarityResolver.Resolve(qualifiedItemId, _config.RarityThresholds);
            long jp = _jp.PerItem(rarity, Run.WeekOfYear) * count;
            _store.State.JunimoPoints += jp;
            Run.RecordDonation(qualifiedItemId);

            _monitor.Log(
                $"Donated {count}x {qualifiedItemId} ({rarity}) -> +{jp} JP (now {_store.State.JunimoPoints}).",
                LogLevel.Info);
        }

        /// <summary>A bundle just completed — award its one-time completion bonus.</summary>
        public void OnBundleCompleted(int bundleIndex)
        {
            if (!Run.TryMarkBundleAwarded(bundleIndex))
                return;

            _store.State.JunimoPoints += _config.Jp.BundleCompletionBonus;
            _monitor.Log(
                $"Bundle {bundleIndex} complete -> +{_config.Jp.BundleCompletionBonus} JP (now {_store.State.JunimoPoints}).",
                LogLevel.Info);
        }

        /// <summary>A room/area just completed — award its one-time completion bonus.</summary>
        public void OnRoomCompleted(int area)
        {
            if (!Run.TryMarkRoomAwarded(area))
                return;

            _store.State.JunimoPoints += _config.Jp.RoomCompletionBonus;
            _monitor.Log(
                $"Room {area} complete -> +{_config.Jp.RoomCompletionBonus} JP (now {_store.State.JunimoPoints}).",
                LogLevel.Info);
        }
    }
}
