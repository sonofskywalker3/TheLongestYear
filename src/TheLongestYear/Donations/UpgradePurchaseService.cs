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

        public UpgradePurchaseService(IMonitor monitor, MetaStore store)
        {
            _monitor = monitor;
            _store = store;
        }

        /// <summary>Attempt to buy by id. Returns the rule's result so the caller can react.</summary>
        public UpgradePurchase.PurchaseResult TryPurchase(string upgradeId)
        {
            UpgradeDefinition def = UpgradeCatalog.TryGet(upgradeId);
            UpgradePurchase.PurchaseResult result = UpgradePurchase.TryPurchase(_store.State, def);
            LogResult(upgradeId, def, result);
            return result;
        }

        private void LogResult(string requestedId, UpgradeDefinition def, UpgradePurchase.PurchaseResult result)
        {
            switch (result)
            {
                case UpgradePurchase.PurchaseResult.Success:
                    Game1.playSound("purchase");
                    _monitor.Log(
                        $"Purchased '{def.Id}' ({def.DisplayName}) for {def.Cost} JP. " +
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
                        $"Cannot purchase '{def.Id}': costs {def.Cost} JP, you have {_store.State.JunimoPoints}.",
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
