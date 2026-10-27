using System;
using System.Collections.Generic;
using System.Linq;
using StardewValley;
using StardewValley.GameData.Shops;
using StardewValley.Internal;
using TheLongestYear.Core;
using StardewValley.ItemTypeDefinitions;
using TheLongestYear.Core.Obtainability;

namespace TheLongestYear.Loop
{
    /// <summary>
    /// Game-side half of <see cref="CartSlotArrangement"/>: reads the Traveler shop's own lines to
    /// split its stock into the main random draw and the extras, sorts every registered item into
    /// the Year 2 pool (<see cref="YearTwoCartPool"/>, once per save load), and builds the Year 2
    /// item when vanilla did not stock it, priced and stock-limited by the cart's random-object line.
    /// </summary>
    internal static class CartYearTwoSlot
    {
        private const string GuaranteeField = "VisitsUntilY1Guarantee";
        private const string SyncedKey = "TLY_YearTwoSlot";
        private const int ShuffleSalt = 0x2C47;
        private const int YearTwoSalt = 0x5A1E;

        private static ObtainabilityModel _poolModel;
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

        /// <summary>Set by ModEntry: the obtainability model built at save load (null when none).</summary>
        internal static Func<ObtainabilityModel> ModelProvider;

        /// <summary>Every item the Year 2 slot can offer (<see cref="YearTwoCartPool"/>), sorted once
        /// per obtainability model: the model is rebuilt only when a save loads.</summary>
        public static IReadOnlyList<string> Pool()
        {
            ObtainabilityModel model = ModelProvider?.Invoke();
            if (model == null)
            {
                PatchLog.WarnOnce("cart-year2-no-model", "Traveling Cart: no obtainability model, so no Year 2 slot today.");
                return Array.Empty<string>();
            }
            if (ReferenceEquals(model, _poolModel)) return _pool;
            var candidates = new List<CartCandidate>();
            foreach (IItemDataDefinition type in ItemRegistry.ItemTypes)
                foreach (string id in type.GetAllIds())
                {
                    string qualified = type.Identifier + id;
                    if (ItemRegistry.Create(qualified, allowNull: true) is not Item item) continue;
                    var obj = item as StardewValley.Object;
                    candidates.Add(new CartCandidate(qualified, type.Identifier, item.salePrice(ignoreProfitMargins: true),
                        obj?.Type, item.Category, YearTwoItems.Classify(model, qualified)));
                }
            _pool = YearTwoCartPool.Select(candidates);
            _poolModel = model;
            PatchLog.Info($"Cart Year 2 pool ({_pool.Count}): {string.Join(", ", _pool)}");
            return _pool;
        }

        /// <summary>Drops the cached pool (a save loaded or Data/Shops changed).</summary>
        public static void Invalidate() => _poolModel = null;

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
