using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using StardewValley;
using StardewValley.GameData.Shops;
using StardewValley.Internal;
using TheLongestYear.Core;

namespace TheLongestYear.Loop
{
    /// <summary>
    /// Caps how many items the Traveling Cart ("Traveler" shop) offers, to the count unlocked by
    /// the player's cart_slot_N upgrades (1 by default). Postfixes ShopBuilder.GetShopStock and
    /// keeps the first N entries; the stock dictionary is built in a stable per-day insertion order
    /// (Utility.CreateDaySaveRandom seeds it deterministically) so the trimmed subset is the same
    /// every time the cart is viewed on a given day. No-op when no TLY run is loaded
    /// (UpgradeChecker.HasUpgrade == null) so it never touches non-TLY saves.
    /// </summary>
    [HarmonyPatch(typeof(ShopBuilder), nameof(ShopBuilder.GetShopStock), new[] { typeof(string), typeof(ShopData) })]
    internal static class CartSlotLimitPatch
    {
        private const string TravelerShopId = "Traveler";

        // ReSharper disable once InconsistentNaming — Harmony convention.
        // ReSharper disable once UnusedMember.Local — discovered by PatchAll.
        private static void Postfix(string shopId, ref Dictionary<ISalable, ItemStockInformation> __result)
        {
            if (UpgradeChecker.HasUpgrade == null) return;       // dormant on non-TLY saves
            if (shopId != TravelerShopId) return;
            if (__result == null || __result.Count == 0) return;

            int tier = UpgradeChecker.GetTier("cart_slot", CartSlotRules.MaxSlots);
            int allowed = CartSlotRules.VisibleSlots(tier);
            if (__result.Count <= allowed) return;

            var trimmed = __result.Take(allowed).ToDictionary(kv => kv.Key, kv => kv.Value);
            __result.Clear();
            foreach (var kv in trimmed) __result.Add(kv.Key, kv.Value);
        }
    }
}
