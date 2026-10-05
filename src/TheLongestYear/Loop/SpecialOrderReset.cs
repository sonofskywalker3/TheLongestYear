using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework.Content;
using StardewModdingAPI;
using StardewValley;
using StardewValley.GameData.SpecialOrders;
using StardewValley.SpecialOrders;
using TheLongestYear.Core;

namespace TheLongestYear.Loop
{
    /// <summary>Loop-reset glue for <see cref="SpecialOrderResetRules"/>. Special orders live on
    /// <c>Game1.player.team</c>, which loadForNewGame never rebuilds (Game1.cs:2902 touches only
    /// sharedDailyLuck), so without this a town order, the board's offer and the completed list all
    /// rode into the next loop. Applies to every player, with or without Keep Special Orders Board.</summary>
    internal static class SpecialOrderReset
    {
        public static void Apply(IMonitor monitor)
        {
            FarmerTeam team = Game1.player?.team;
            if (team == null) return;

            // Town orders taken this loop, in progress or finished but unclaimed. Dropped outright,
            // not failed: OnFail would send donated items to the lost and found and queue overnight
            // removals for a night the rewind skips, and the reset wipes inventory and mail anyway.
            int dropped = 0;
            for (int i = team.specialOrders.Count - 1; i >= 0; i--)
            {
                SpecialOrder order = team.specialOrders[i];
                if (order != null && !SpecialOrderResetRules.IsTownOrder(order.orderType.Value)) continue;
                team.specialOrders.RemoveAt(i);
                dropped++;
            }

            // The board's offer and its "took one this week" mark. Vanilla's own helper; the board
            // re-rolls when it is next opened (SpecialOrdersBoard ctor, forceRefresh: false) or on the
            // next Monday's refresh, seeded from the new loop's uniqueID and DaysPlayed.
            SpecialOrder.RemoveAllSpecialOrders(SpecialOrderResetRules.TownOrderType);

            // Forget completed town orders so the non-repeatable ones come back.
            List<string> forget = SpecialOrderResetRules.CompletedToForget(team.completedSpecialOrders, LoadOrderTypes(monitor));
            foreach (string id in forget)
                team.completedSpecialOrders.Remove(id);

            monitor.Log(
                $"In-place reset: special orders: dropped {dropped} town order(s), cleared the town board's offer, " +
                $"forgot {forget.Count} completed town order(s). Qi and other boards untouched.",
                LogLevel.Info);
        }

        /// <summary>OrderType per Data/SpecialOrders id. Empty if the asset cannot be read, which
        /// forgets nothing (failing safe: an order stays completed rather than being guessed at).</summary>
        private static IReadOnlyDictionary<string, string> LoadOrderTypes(IMonitor monitor)
        {
            try
            {
                return Game1.content.Load<Dictionary<string, SpecialOrderData>>("Data/SpecialOrders")
                    .Where(kv => kv.Value != null)
                    .ToDictionary(kv => kv.Key, kv => kv.Value.OrderType);
            }
            catch (ContentLoadException ex)
            {
                monitor.Log($"Reset: Data/SpecialOrders unreadable ({ex.Message}); completed orders left as they are.", LogLevel.Warn);
                return new Dictionary<string, string>();
            }
        }
    }
}
