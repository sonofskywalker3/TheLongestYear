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

        /// <summary>Runs the live purchase hooks for an upgrade granted outside this service (a
        /// focus card), so it takes effect the same way a shrine purchase does.</summary>
        public void NotifyPurchased(string id) => Purchased?.Invoke(id);

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

        /// <summary>What the next banish costs at the current difficulty (shown and charged).</summary>
        public long BanishCost() => BanishLadder.NextCost(_store.State, PriceFactor);

        /// <summary>Buy a banish for the next board (<see cref="BanishRules.Buy"/>).</summary>
        public BanishRules.BuyResult TryBanish(BanishKind kind, string key, string label)
        {
            long cost = BanishCost();
            BanishRules.BuyResult result = BanishRules.Buy(_store.State, kind, key, label, PriceFactor);
            if (result == BanishRules.BuyResult.Bought)
                Game1.playSound("purchase");
            _monitor.Log(
                $"Banish {kind} '{key}' ({label}): {result}" +
                (result == BanishRules.BuyResult.Bought ? $" for {cost} JP" : $" (costs {cost} JP)") +
                $". JP {_store.State.JunimoPoints}, pending {_store.State.PendingBanishes.Count}.",
                result == BanishRules.BuyResult.Bought ? LogLevel.Info : LogLevel.Debug);
            return result;
        }

        /// <summary>Take back a banish bought this visit; logs and returns the refund.</summary>
        public long CancelBanish(int index)
        {
            string what = index >= 0 && index < _store.State.PendingBanishes.Count
                ? $"{_store.State.PendingBanishes[index].Kind} '{_store.State.PendingBanishes[index].Key}'"
                : "(none)";
            long refund = BanishRules.Cancel(_store.State, index);
            if (refund > 0) Game1.playSound("coin");
            _monitor.Log($"Banish {what} taken back: refunded {refund} JP. JP {_store.State.JunimoPoints}.", LogLevel.Info);
            return refund;
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
                case UpgradePurchase.PurchaseResult.NoAnimalRoom:
                {
                    string family = AnimalCapacityRule.FamilyOf(def.Id);
                    _monitor.Log(
                        $"Cannot purchase '{def.Id}': the kept {family} buildings hold " +
                        $"{AnimalCapacityRule.Capacity(_store.State, family)} animals and " +
                        $"{AnimalCapacityRule.DemandFor(_store.State, def.Id)} already count against that room.",
                        LogLevel.Info);
                    break;
                }
            }
        }
    }
}
