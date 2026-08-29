using System.Collections.Generic;
using StardewModdingAPI;
using StardewValley;
using TheLongestYear.Core;

namespace TheLongestYear.Donations
{
    /// <summary>
    /// Mod-side wrapper around <see cref="BoostPurchase.TryBuy"/>, the boost twin of
    /// <see cref="UpgradePurchaseService"/>: it supplies the live week-of-year (Core never reads
    /// game state), logs the outcome, plays the purchase sound on success and shows a HUD message
    /// when the player cannot afford it.
    /// <para>
    /// Persistence follows the same rule as upgrade purchases: the spent JP and the run flags live
    /// in <see cref="MetaStore.State"/> / <see cref="MetaStore.Run"/> and are committed by the
    /// game's own Saving event. Nothing is written eagerly, which keeps the anti-save-scum
    /// invariant documented on <see cref="MetaStore.Save"/> intact.
    /// </para>
    /// </summary>
    internal sealed class BoostPurchaseService
    {
        private readonly IMonitor _monitor;
        private readonly MetaStore _store;

        public BoostPurchaseService(IMonitor monitor, MetaStore store)
        {
            _monitor = monitor;
            _store = store;
        }

        /// <summary>The week the purchase is charged against: whatever week the run is in right now.</summary>
        public int WeekOfYear => _store.Run.WeekOfYear;

        /// <summary>Attempt to buy a boost for the current week. Returns the rule's result so the
        /// menu can refresh its rows.</summary>
        public BoostPurchase.Result TryBuy(BoostId id)
        {
            int week = _store.Run.WeekOfYear;
            BoostPurchase.Result result = BoostPurchase.TryBuy(_store.State, _store.Run, id, week);
            Report(id, week, result);
            return result;
        }

        private void Report(BoostId id, int week, BoostPurchase.Result result)
        {
            long cost = CostOf(id);
            switch (result)
            {
                case BoostPurchase.Result.Success:
                    Game1.playSound("purchase");
                    _monitor.Log($"Boost bought: {id} for {cost} JP (week {week}). JP remaining: {_store.State.JunimoPoints}.", LogLevel.Info);
                    break;
                case BoostPurchase.Result.NotEnoughJp:
                    Game1.playSound("cancel");
                    Game1.addHUDMessage(new HUDMessage(
                        Strings.Get("boost.not-enough-jp", new Dictionary<string, string>
                        {
                            ["cost"] = cost.ToString(),
                            ["have"] = _store.State.JunimoPoints.ToString(),
                        }),
                        HUDMessage.error_type));
                    _monitor.Log($"Boost {id} not bought: costs {cost} JP, you have {_store.State.JunimoPoints}.", LogLevel.Info);
                    break;
                case BoostPurchase.Result.AlreadyActive:
                    Game1.playSound("cancel");
                    _monitor.Log($"Boost {id} not bought: already active (week {week}).", LogLevel.Info);
                    break;
                case BoostPurchase.Result.NotAvailable:
                    Game1.playSound("cancel");
                    _monitor.Log($"Boost {id} not bought: not available in week {week}.", LogLevel.Info);
                    break;
            }
        }

        /// <summary>Catalog cost of a boost, for logs and the "you have X" HUD line.</summary>
        public static long CostOf(BoostId id)
        {
            foreach (BoostDefinition definition in BoostCatalog.All)
            {
                if (definition.Id == id)
                    return definition.Cost;
            }
            return 0;
        }
    }
}
