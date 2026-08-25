using System;
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
    /// the player's cart_slot_N upgrades (1 by default). Postfixes ShopBuilder.GetShopStock. The
    /// cap is per day: the first build of the day picks the visible ids and remembers them on the
    /// live RunState; every later build that day (e.g. reopening the cart after a purchase) is
    /// filtered to those same ids, so buying an item leaves a gap for the rest of the day instead
    /// of the next item sliding into view. No-op when no TLY run is loaded
    /// (UpgradeChecker.HasUpgrade == null) so it never touches non-TLY saves.
    /// </summary>
    [HarmonyPatch(typeof(ShopBuilder), nameof(ShopBuilder.GetShopStock), new[] { typeof(string), typeof(ShopData) })]
    internal static class CartSlotLimitPatch
    {
        internal const string TravelerShopId = "Traveler";

        /// <summary>Mirrors <c>GameplayConfig.LimitTravelingCartStock</c>; set by ModEntry at
        /// config load and whenever GMCM changes it. False = postfix is a no-op.</summary>
        internal static bool Enabled = true;

        /// <summary>Set by ModEntry.OnSaveLoaded (null when no TLY run is loaded): the live RunState
        /// that remembers today's cart selection.</summary>
        internal static Func<RunState> RunProvider;

        // ReSharper disable once InconsistentNaming — Harmony convention.
        // ReSharper disable once UnusedMember.Local — discovered by PatchAll.
        private static void Postfix(string shopId, ref Dictionary<ISalable, ItemStockInformation> __result)
        {
            if (!Enabled) return;                                // config kill-switch
            if (UpgradeChecker.HasUpgrade == null) return;       // dormant on non-TLY saves
            if (shopId != TravelerShopId) return;
            if (__result == null || __result.Count == 0) return;
            int tier = UpgradeChecker.GetTier("cart_slot", CartSlotRules.MaxSlots);
            int allowed = CartSlotRules.VisibleSlots(tier);

            RunState run = RunProvider?.Invoke();
            if (run == null)
            {
                // No run state (should not happen once HasUpgrade is set): fall back to the old per-view cap.
                if (__result.Count <= allowed) return;
                var firstN = __result.Take(allowed).ToDictionary(kv => kv.Key, kv => kv.Value);
                __result.Clear();
                foreach (var kv in firstN) __result.Add(kv.Key, kv.Value);
                return;
            }

            var entries = __result.ToList();
            var ids = entries
                .Select(kv => CartDayStock.KeyFor(kv.Key.QualifiedItemId, (kv.Key as StardewValley.Object)?.IsRecipe ?? false))
                .ToList();
            var keep = new HashSet<string>(CartDayStock.Select(run, Game1.Date.TotalDays, ids, allowed), StringComparer.Ordinal);
            __result.Clear();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var kv in entries)
            {
                string key = CartDayStock.KeyFor(kv.Key.QualifiedItemId, (kv.Key as StardewValley.Object)?.IsRecipe ?? false);
                if (keep.Contains(key) && seen.Add(key))
                    __result.Add(kv.Key, kv.Value);
            }
        }
    }
}
