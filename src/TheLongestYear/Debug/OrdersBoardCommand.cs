using System.Linq;
using HarmonyLib;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Locations;
using StardewValley.SpecialOrders;
using TheLongestYear.Core;

namespace TheLongestYear.DebugCommands
{
    /// <summary>tly_ordersboard: read-only report for Keep Special Orders Board and the reset's
    /// special-order hygiene. Logs DaysPlayed, ownership, the reach value, what the (patched) vanilla
    /// gate returns, whether the Town board is up with its "SpecialOrders" action tile, and every
    /// order the team holds, offers and has completed.</summary>
    internal static class OrdersBoardCommand
    {
        public const string Usage =
            "Debug (read-only): report the Special Orders board state: gate, Town board tiles, team orders, offer and completed list. Usage: tly_ordersboard";

        public static void Run(IMonitor monitor, string[] args)
        {
            if (!Context.IsWorldReady) { monitor.Log("Load a save first.", LogLevel.Warn); return; }
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

        private static string Type(SpecialOrder o) => SpecialOrderResetRules.IsTownOrder(o.orderType.Value) ? "town" : o.orderType.Value;
    }
}
