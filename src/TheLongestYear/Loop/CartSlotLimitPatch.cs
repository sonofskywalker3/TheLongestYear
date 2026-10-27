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
    /// of the next item sliding into view. Before the cap, <see cref="CartSlotArrangement"/> puts
    /// vanilla's fixed extras in random slots and may give one slot to a Year 2 item. No-op when no TLY run is loaded
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

        /// <summary>Set by ModEntry.OnSaveLoaded: how many items the cart shows before any Cart
        /// Stall upgrade, from the run's stamped difficulty profile (spec 2026-08-26). Null on a
        /// non-TLY save, which falls back to the shipping value of one.</summary>
        internal static Func<int> StartingSlotsProvider;

        // ReSharper disable once InconsistentNaming — Harmony convention.
        // ReSharper disable once UnusedMember.Local — discovered by PatchAll.
        private static void Postfix(string shopId, ShopData shop, ref Dictionary<ISalable, ItemStockInformation> __result)
        {
            if (!Enabled) return;                                // config kill-switch
            if (UpgradeChecker.HasUpgrade == null) return;       // dormant on non-TLY saves
            if (shopId != TravelerShopId) return;
            if (__result == null || __result.Count == 0) return;
            int tier = UpgradeChecker.GetTier("cart_slot", CartSlotRules.MaxSlots);
            int allowed = CartSlotRules.VisibleSlots(tier, StartingSlotsProvider?.Invoke() ?? CartSlotRules.MinSlots);

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

            int day = Game1.Date.TotalDays;
            var entries = __result.ToList();
            var ids = entries
                .Select(kv => CartDayStock.KeyFor(kv.Key.QualifiedItemId, kv.Key.IsRecipe))
                .ToList();
            if (RunActivation.IsActive)
                Arrange(shop, run, day, allowed, entries, ref ids);
            var keep = new HashSet<string>(CartDayStock.Select(run, day, ids, allowed), StringComparer.Ordinal);
            __result.Clear();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var byKey = entries
                .GroupBy(kv => CartDayStock.KeyFor(kv.Key.QualifiedItemId, kv.Key.IsRecipe), StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
            foreach (string key in ids)
            {
                if (keep.Contains(key) && seen.Add(key) && byKey.TryGetValue(key, out var kv))
                    __result.Add(kv.Key, kv.Value);
            }
        }

        /// <summary>Reorders <paramref name="ids"/> by <see cref="CartSlotArrangement"/> (extras in
        /// random slots, the Year 2 slot) and adds the Year 2 item to <paramref name="entries"/>
        /// when vanilla did not stock it.</summary>
        private static void Arrange(ShopData shop, RunState run, int day, int allowed,
            List<KeyValuePair<ISalable, ItemStockInformation>> entries, ref List<string> ids)
        {
            try
            {
                CartArrangement arranged = CartSlotArrangement.Arrange(ids,
                    CartYearTwoSlot.MainDrawCount(shop, ids), CartYearTwoSlot.GuaranteedKeys(shop),
                    CartYearTwoSlot.Pool(day), allowed,
                    CartYearTwoSlot.ShuffleRng(run.Seed, day), CartYearTwoSlot.YearTwoRng(run.Seed, day));
                var order = arranged.Order.ToList();
                if (arranged.Added != null)
                {
                    var built = CartYearTwoSlot.Build(arranged.Added, shop, CartYearTwoSlot.YearTwoRng(run.Seed, day));
                    if (built is { } b)
                        entries.Add(new KeyValuePair<ISalable, ItemStockInformation>(b.Item, b.Info));
                    else
                        order.Remove(arranged.Added);   // sold out today
                }
                ids = order;
            }
            catch (Exception ex)
            {
                PatchLog.WarnOnce("cart-arrange",
                    $"Traveling Cart: could not arrange today's slots ({ex.GetType().Name}: {ex.Message}); showing vanilla's order.");
            }
        }
    }
}
