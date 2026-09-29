using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework.Content;
using StardewModdingAPI;
using StardewValley;
using StardewValley.GameData.SpecialOrders;
using TheLongestYear.Core.Availability;

namespace TheLongestYear.Loop
{
    /// <summary>Special-order rules no loop can switch on (only Mr. Qi's Walnut Room orders grant
    /// them), read from the live Data/SpecialOrders so mod orders count. Feeds
    /// <see cref="YearOneCondition.Allows"/> wherever spawn, drop and shop conditions are read.</summary>
    internal static class ClosedSpecialOrderRules
    {
        /// <summary>Empty (nothing closed) if the asset cannot be read: failing open only ever
        /// keeps a route, never invents a closed one.</summary>
        public static IReadOnlySet<string> Load(IMonitor monitor)
        {
            try
            {
                var orders = Game1.content.Load<Dictionary<string, SpecialOrderData>>("Data/SpecialOrders")
                    .Where(kv => kv.Value != null)
                    .Select(kv => new RawSpecialOrder(
                        kv.Key, kv.Value.OrderType,
                        (kv.Value.SpecialRule ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)));
                return YearOneCondition.RulesOnlyQiGrants(orders);
            }
            catch (ContentLoadException ex)
            {
                monitor?.Log($"Special-order rules unreadable ({ex.Message}); no rule-gated row is closed.", LogLevel.Warn);
                return new HashSet<string>(StringComparer.Ordinal);
            }
        }
    }
}
