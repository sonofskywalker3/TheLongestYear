using StardewModdingAPI;
using StardewValley;
using TheLongestYear.Core;

namespace TheLongestYear.Donations
{
    /// <summary>
    /// Mod-side wrapper around <see cref="UpgradePurchase.TryPurchase"/>: logs, plays a sound on
    /// success, and exposes the result so the menu can update its row state. Keeps the Core rule
    /// pure and game-agnostic.
    /// </summary>
    internal sealed class UpgradePurchaseService
    {
        private readonly IMonitor _monitor;
        private readonly MetaStore _store;
        private readonly GameplayConfig _config;

        public UpgradePurchaseService(IMonitor monitor, MetaStore store, GameplayConfig config)
        {
            _monitor = monitor;
            _store = store;
            _config = config;
        }

        /// <summary>The run's stamped shrine-price factor. Read per call, not cached: a reset
        /// re-stamps the profile mid-session.</summary>
        private double PriceFactor => _store.State.EffectiveDifficulty(_config).ShrinePriceFactor;

        /// <summary>What this upgrade actually costs at the current difficulty. The menus use the
        /// same helper, so the shown price and the charged price are the same number.</summary>
        public long EffectiveCost(UpgradeDefinition definition)
            => UpgradePricing.EffectiveCost(definition, PriceFactor, _store.State);

        /// <summary>Fires with the upgrade id after a successful purchase (wired by ModEntry for
        /// upgrades whose effect needs a live refresh, e.g. a Data/Shops cache invalidation).</summary>
        public System.Action<string> Purchased;

        /// <summary>Attempt to buy by id. Returns the rule's result so the caller can react.</summary>
        public UpgradePurchase.PurchaseResult TryPurchase(string upgradeId)
        {
            UpgradeDefinition def = UpgradeCatalog.TryGet(upgradeId);
            // Price BEFORE the buy: a Gifts of the Junimos row climbs the ladder the moment it is owned.
            long charged = def == null ? 0 : EffectiveCost(def);
            UpgradePurchase.PurchaseResult result = UpgradePurchase.TryPurchase(_store.State, def, PriceFactor);
            LogResult(upgradeId, def, result, charged);
            if (result == UpgradePurchase.PurchaseResult.Success)
                Purchased?.Invoke(def.Id);
            return result;
        }

        private void LogResult(string requestedId, UpgradeDefinition def, UpgradePurchase.PurchaseResult result, long charged)
        {
            switch (result)
            {
                case UpgradePurchase.PurchaseResult.Success:
                    Game1.playSound("purchase");
                    _monitor.Log(
                        $"Purchased '{def.Id}' ({def.DisplayName}) for {charged} JP. " +
                        $"JP remaining: {_store.State.JunimoPoints}.",
                        LogLevel.Info);
                    break;
                case UpgradePurchase.PurchaseResult.NotInCatalog:
                    _monitor.Log($"Cannot purchase '{requestedId}': not in catalog.", LogLevel.Warn);
                    break;
                case UpgradePurchase.PurchaseResult.AlreadyOwned:
                    _monitor.Log($"Cannot purchase '{def.Id}': already owned.", LogLevel.Info);
                    break;
                case UpgradePurchase.PurchaseResult.PrerequisiteMissing:
                    _monitor.Log($"Cannot purchase '{def.Id}': requires '{def.PrerequisiteId}' first.", LogLevel.Info);
                    break;
                case UpgradePurchase.PurchaseResult.NotEnoughJp:
                    _monitor.Log(
                        $"Cannot purchase '{def.Id}': costs {EffectiveCost(def)} JP, you have {_store.State.JunimoPoints}.",
                        LogLevel.Info);
                    break;
                case UpgradePurchase.PurchaseResult.MetaRequirementMissing:
                    _monitor.Log(
                        $"Cannot purchase '{def.Id}': meta-requirement '{def.MetaRequirement}' not yet met.",
                        LogLevel.Info);
                    break;
            }
        }
    }
}
