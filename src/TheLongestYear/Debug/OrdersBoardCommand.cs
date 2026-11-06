using System.Linq;
using HarmonyLib;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Locations;
using StardewValley.SpecialOrders;
using StardewValley.SpecialOrders.Objectives;
using TheLongestYear.Core;

namespace TheLongestYear.DebugCommands
{
    /// <summary>tly_ordersboard: report for Keep Special Orders Board and the reset's
    /// special-order hygiene. Logs DaysPlayed, ownership, the reach value, what the (patched) vanilla
    /// gate returns, whether the Town board is up with its "SpecialOrders" action tile, and every
    /// order the team holds, offers and has completed.</summary>
    internal static class OrdersBoardCommand
    {
        public const string Usage =
            "Debug: report the Special Orders board state: gate, Town board tiles, team orders, offer and completed list. Usage: tly_ordersboard. " +
            "tly_ordersboard donate <orderKey> fills that held order's drop box with what each donate objective asks for and confirms it, " +
            "the way closing the box does, so the order completes and pays through the game's own path.";

        public static void Run(IMonitor monitor, string[] args)
        {
            if (!Context.IsWorldReady) { monitor.Log("Load a save first.", LogLevel.Warn); return; }
            if (args.Length >= 2 && args[0] == "donate") { Donate(monitor, args[1]); return; }
            FarmerTeam team = Game1.player.team;
            uint days = Game1.stats.DaysPlayed;
            bool owned = TheLongestYear.Loop.SpecialOrdersBoardPatch.Owned();

            Town town = Game1.getLocationFromName("Town") as Town;
            object showing = town == null ? null : AccessTools.Field(typeof(Town), "isShowingSpecialOrdersBoard")?.GetValue(town);
            string action = town?.doesTileHaveProperty(61, 93, "Action", "Buildings");

            monitor.Log(
                $"tly_ordersboard: DaysPlayed={days} owned={owned} reach={SpecialOrdersBoardKeep.ReachValue(days)} " +
                $"gate={SpecialOrder.IsSpecialOrdersBoardUnlocked()} expectedOpen={SpecialOrdersBoardKeep.IsBoardOpen(owned, days)} " +
                $"townShowing={showing ?? "n/a"} boardAction={action ?? "none"}",
                LogLevel.Info);
            monitor.Log("  active: " + string.Join(", ",
                team.specialOrders.Select(o => $"{o.questKey.Value}[{Type(o)}|{o.questState.Value}|due {o.dueDate.Value}]")), LogLevel.Info);
            monitor.Log("  offered: " + string.Join(", ",
                team.availableSpecialOrders.Select(o => $"{o.questKey.Value}[{Type(o)}]")), LogLevel.Info);
            monitor.Log("  acceptedTypes: " + string.Join(", ", team.acceptedSpecialOrderTypes.Select(t => t.Length == 0 ? "town" : t)), LogLevel.Info);
            monitor.Log($"  completed ({team.completedSpecialOrders.Count}): " + string.Join(", ", team.completedSpecialOrders), LogLevel.Info);
        }

        /// <summary>Puts each donate objective's full count into the order's drop box, then runs the
        /// two calls the box's menu makes on close (UpdateDonationCounts, ConfirmCompleteDonations).</summary>
        private static void Donate(IMonitor monitor, string orderKey)
        {
            SpecialOrder order = Game1.player.team.specialOrders.FirstOrDefault(o => o.questKey.Value == orderKey);
            if (order == null) { monitor.Log($"tly_ordersboard donate: the team does not hold '{orderKey}'.", LogLevel.Warn); return; }

            foreach (DonateObjective objective in order.objectives.OfType<DonateObjective>())
            {
                Item match = ItemRegistry.GetObjectTypeDefinition().GetAllIds()
                    .Select(id => ItemRegistry.Create("(O)" + id, 1, 0, allowNull: true))
                    .FirstOrDefault(item => item != null && objective.IsValidItem(item));
                if (match == null)
                {
                    monitor.Log($"tly_ordersboard donate: no object fits '{objective.description.Value}'.", LogLevel.Warn);
                    continue;
                }
                match.Stack = objective.GetMaxCount();
                order.donatedItems.Add(match);
                monitor.Log($"tly_ordersboard donate: {match.QualifiedItemId} x{match.Stack} into the box.", LogLevel.Info);
            }
            order.UpdateDonationCounts();
            order.ConfirmCompleteDonations();
            monitor.Log(
                $"tly_ordersboard donate: {orderKey} is now {order.questState.Value}; completed list has it: " +
                $"{Game1.player.team.completedSpecialOrders.Contains(orderKey)}.", LogLevel.Info);
        }

        private static string Type(SpecialOrder o) => SpecialOrderResetRules.IsTownOrder(o.orderType.Value) ? "town" : o.orderType.Value;
    }
}
