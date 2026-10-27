using System;
using System.Collections.Generic;
using System.Linq;
using StardewModdingAPI;
using StardewValley;
using StardewValley.GameData.Shops;
using StardewValley.Internal;
using TheLongestYear.Core;
using TheLongestYear.Loop;

namespace TheLongestYear
{
    public sealed partial class ModEntry
    {
        private const string CartSweepArg = "sweep";

        /// <summary>Debug: <c>tly_cart</c> logs the cart stock exactly as the shop would show it
        /// today (through CartSlotLimitPatch, so it also fixes today's remembered slots, as opening
        /// the cart would). <c>tly_cart sweep n</c> runs today's real stock through
        /// <see cref="CartSlotArrangement"/> under n other day seeds, without touching the run, and
        /// reports how often an extra and a Year 2 item land in a shown slot.</summary>
        private void CmdCart(string command, string[] args)
        {
            if (!Context.IsWorldReady) { this.Monitor.Log("Load a save first.", LogLevel.Warn); return; }
            if (args.Length >= 2 && string.Equals(args[0], CartSweepArg, StringComparison.OrdinalIgnoreCase)
                && int.TryParse(args[1], out int days))
            {
                CartSweep(days);
                return;
            }

            var stock = ShopBuilder.GetShopStock(CartSlotLimitPatch.TravelerShopId);
            var pool = new HashSet<string>(CartYearTwoSlot.Pool(Game1.Date.TotalDays), StringComparer.Ordinal);
            this.Monitor.Log($"tly_cart: day {Game1.Date.TotalDays}, {stock.Count} shown:", LogLevel.Info);
            foreach (var kv in stock)
                this.Monitor.Log($"  {kv.Key.QualifiedItemId} {kv.Key.DisplayName} {kv.Value.Price}g x{kv.Value.Stock}"
                                 + (pool.Contains(kv.Key.QualifiedItemId) ? " [Year 2]" : ""), LogLevel.Info);
        }

        private void CartSweep(int days)
        {
            ShopData shop = DataLoader.Shops(Game1.content)[CartSlotLimitPatch.TravelerShopId];
            CartSlotLimitPatch.Enabled = false;   // the raw vanilla stock, no cap or arrangement
            Dictionary<ISalable, ItemStockInformation> raw;
            try { raw = ShopBuilder.GetShopStock(CartSlotLimitPatch.TravelerShopId, shop); }
            finally { CartSlotLimitPatch.Enabled = _config.LimitTravelingCartStock; }

            var ids = raw.Keys.Select(k => CartDayStock.KeyFor(k.QualifiedItemId, k.IsRecipe)).ToList();
            int main = CartYearTwoSlot.MainDrawCount(shop, ids);
            int allowed = CartSlotRules.VisibleSlots(UpgradeChecker.GetTier("cart_slot", CartSlotRules.MaxSlots),
                CartSlotLimitPatch.StartingSlotsProvider?.Invoke() ?? CartSlotRules.MinSlots);
            IReadOnlyList<string> pool = CartYearTwoSlot.Pool(Game1.Date.TotalDays);
            var guaranteed = CartYearTwoSlot.GuaranteedKeys(shop);
            int seed = _meta.Run.Seed;
            int extraShown = 0, yearTwoShown = 0, built = 0, rolled = 0, rolledHidden = 0;
            var samples = new List<string>();
            for (int day = 0; day < days; day++)
            {
                CartArrangement a = CartSlotArrangement.Arrange(ids, main, guaranteed, pool, allowed,
                    CartYearTwoSlot.ShuffleRng(seed, day), CartYearTwoSlot.YearTwoRng(seed, day));
                var shown = a.Order.Take(allowed).ToList();
                if (shown.Any(k => ids.IndexOf(k) >= main)) extraShown++;
                bool hit = CartYearTwoSlot.YearTwoRng(seed, day).NextDouble() < CartSlotArrangement.YearTwoChance;
                if (hit) rolled++;
                if (shown.Any(pool.Contains)) yearTwoShown++;
                else if (hit) rolledHidden++;
                if (a.Added != null && CartYearTwoSlot.Build(a.Added, shop, CartYearTwoSlot.YearTwoRng(seed, day)) is { } b)
                {
                    built++;
                    if (samples.Count < 5) samples.Add($"{b.Item.QualifiedItemId} {b.Item.DisplayName} {b.Info.Price}g x{b.Info.Stock}");
                }
            }
            this.Monitor.Log($"tly_cart sweep: {days} days, {allowed} slot(s), stock {ids.Count} ({main} main draw, " +
                             $"{ids.Count - main} extras), pool {pool.Count}. Extra shown {extraShown}, Year 2 rolled {rolled} " +
                             $"(not shown {rolledHidden}), Year 2 shown {yearTwoShown}, " +
                             $"Year 2 built {built}. Seed {seed}. Samples: {string.Join("; ", samples)}", LogLevel.Info);
        }
    }
}
