using System;
using System.Collections.Generic;
using System.Linq;
using StardewValley;
using StardewValley.GameData.Shops;
using StardewValley.Internal;
using TheLongestYear.Core;
using TheLongestYear.Core.Obtainability;

namespace TheLongestYear.Loop
{
    /// <summary>
    /// Game-side half of <see cref="CartSlotArrangement"/>: reads the Traveler shop's own lines to
    /// split its stock into the main random draw and the extras, finds the Year 2 pool in
    /// Data/Shops (<see cref="YearTwoShopItems"/>, once per day), and builds the Year 2 item when
    /// vanilla did not stock it, priced and stock-limited by the cart's random-object line.
    /// </summary>
    internal static class CartYearTwoSlot
    {
        private const string GuaranteeField = "VisitsUntilY1Guarantee";
        private const string SyncedKey = "TLY_YearTwoSlot";
        private const int ShuffleSalt = 0x2C47;
        private const int YearTwoSalt = 0x5A1E;

        private static int _poolDay = -1;
        private static IReadOnlyList<string> _pool = Array.Empty<string>();

        // RollSeed, not seed ^ day * prime: the legacy Random turns related seeds into related
        // first draws (see RollSeed), and these are rolled on consecutive days.
        public static Random ShuffleRng(int seed, int day) => RollSeed.Rng(seed, day, ShuffleSalt);

        public static Random YearTwoRng(int seed, int day) => RollSeed.Rng(seed, day, YearTwoSalt);

        /// <summary>The cart's first line when it is an item query (vanilla's RandomObjects).</summary>
        public static ShopItemData MainDrawLine(ShopData shop)
        {
            ShopItemData first = shop?.Items?.FirstOrDefault();
            return first != null && IsQuery(first.ItemId) ? first : null;
        }

        /// <summary>How many leading stock keys came from the main draw: the leading run that no
        /// fixed cart line names, up to the draw's MaxItems. Zero when the cart does not start with
        /// a query (a mod rebuilt it), which leaves vanilla's order alone.</summary>
        public static int MainDrawCount(ShopData shop, IReadOnlyList<string> keys)
        {
            ShopItemData main = MainDrawLine(shop);
            if (main == null) return keys.Count;
            var fixedIds = FixedIds(shop);
            int max = main.MaxItems ?? int.MaxValue;
            int count = 0;
            while (count < keys.Count && count < max && !fixedIds.Contains(keys[count])) count++;
            return count;
        }

        /// <summary>Stock keys of the cart lines whose condition is vanilla's year-1 guarantee.</summary>
        public static IReadOnlySet<string> GuaranteedKeys(ShopData shop)
        {
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (ShopItemData line in shop?.Items ?? new List<ShopItemData>())
                if (line?.Condition?.Contains(GuaranteeField, StringComparison.OrdinalIgnoreCase) == true && !IsQuery(line.ItemId))
                    keys.Add(CartDayStock.KeyFor(ItemRegistry.QualifyItemId(line.ItemId) ?? line.ItemId, line.IsRecipe));
            return keys;
        }

        public static IReadOnlyList<string> Pool(int day)
        {
            if (_poolDay == day) return _pool;
            var rows = new List<ShopRow>();
            foreach (var kv in Game1.content.Load<Dictionary<string, ShopData>>("Data/Shops"))
                foreach (ShopItemData item in kv.Value?.Items ?? new List<ShopItemData>())
                    foreach ((string entry, bool random) in GameObtainabilityParsing.Entries(item?.ItemId, item?.RandomItemId))
                        rows.Add(new ShopRow(kv.Key, ItemRegistry.QualifyItemId(entry) ?? entry, item.Condition, item.IsRecipe, random));
            _pool = YearTwoShopItems.Find(rows, Sellable);
            _poolDay = day;
            PatchLog.Info($"Cart Year 2 pool ({_pool.Count}): {string.Join(", ", _pool)}");
            return _pool;
        }

        /// <summary>Drops the cached pool (Data/Shops changed, e.g. Pierre's Year 2 Seeds bought).</summary>
        public static void Invalidate() => _poolDay = -1;

        /// <summary>The Year 2 item and its stock entry, or null when it is sold out today or cannot
        /// be built.</summary>
        public static (ISalable Item, ItemStockInformation Info)? Build(string key, ShopData shop, Random rng)
        {
            ShopItemData line = MainDrawLine(shop);
            if (line == null || ItemRegistry.Create(key, allowNull: true) is not Item item) return null;
            float price = ShopBuilder.GetBasePrice(new ItemQueryResult(item), shop, line, item, false, line.UseObjectDataPrice);
            if (!line.IgnoreShopPriceModifiers)
                price = Utility.ApplyQuantityModifiers(price, shop.PriceModifiers, shop.PriceModifierMode, targetItem: item, random: rng);
            price = Utility.ApplyQuantityModifiers(price, line.PriceModifiers, line.PriceModifierMode, targetItem: item, random: rng);
            int stock = (int)Utility.ApplyQuantityModifiers(line.AvailableStock, line.AvailableStockModifiers, line.AvailableStockModifierMode, targetItem: item, random: rng);
            if (stock < 0) stock = int.MaxValue;
            var info = new ItemStockInformation((int)price, stock, stockMode: line.AvailableStockLimit, syncedKey: SyncedKey);
            var one = new Dictionary<ISalable, ItemStockInformation> { [item] = info };
            Game1.player.team.synchronizedShopStock.UpdateLocalStockWithSyncedQuanitities(CartSlotLimitPatch.TravelerShopId, one);
            return one.TryGetValue(item, out info) ? (item, info) : null;
        }

        private static bool Sellable(string id)
            => ItemRegistry.Create(id, allowNull: true) is Item item && item.salePrice(ignoreProfitMargins: true) > 0;

        private static HashSet<string> FixedIds(ShopData shop)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (ShopItemData line in shop.Items)
                foreach ((string entry, bool _) in GameObtainabilityParsing.Entries(line?.ItemId, line?.RandomItemId))
                    if (!IsQuery(entry))
                        ids.Add(CartDayStock.KeyFor(ItemRegistry.QualifyItemId(entry) ?? entry, line.IsRecipe));
            return ids;
        }

        private static bool IsQuery(string itemId) => itemId != null && itemId.Contains(' ');
    }
}
