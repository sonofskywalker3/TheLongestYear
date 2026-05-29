using HarmonyLib;
using StardewValley;
using TheLongestYear.Core;

namespace TheLongestYear.Loop
{
    /// <summary>
    /// Mixed bonus (all_drops_up): 10% chance — when ANY tool-destroyed Object yields drops
    /// (weeds → fiber/seeds/moss, twigs → wood, machines → the machine, crops → harvest) — to
    /// DOUBLE every item the destruction produced. Stone/ore destruction routes through
    /// <see cref="GameLocation.OnStoneDestroyed"/> instead and is handled by
    /// <see cref="MineOreDropBonus"/>.
    ///
    /// 2026-05-29 playtest evolution:
    ///   1. v1 dropped <c>__instance.QualifiedItemId</c> on hit — but for a weed that gave a
    ///      WEED OBJECT, not the cut-weed product. User: "drop the product, not the item itself."
    ///   2. v2 mapped Weed→Fiber, Twig→Wood. Better, but couldn't capture vanilla's randomised
    ///      drop (fiber 50% / mixed seeds 5% / mixed flower seeds 5% summer / moss 10% green-
    ///      rain), and would have undercounted on cases where vanilla drops multiple items.
    ///   3. v3 (current): snapshot-diff approach mirroring <see cref="MineOreDropBonus"/> —
    ///      prefix captures <c>Location.debris.Count</c>, postfix iterates new debris added by
    ///      vanilla and clones each, so we always double whatever vanilla actually rolled.
    ///
    /// Mixed liability (all_sell_prices_down): every Object.sellToStorePrice multiplied by 0.5,
    /// floored at 1g. Applies across all item types.
    /// </summary>
    [HarmonyPatch(typeof(Object), nameof(Object.performToolAction))]
    internal static class AllDropsBonusPatch
    {
        // ReSharper disable InconsistentNaming — Harmony convention.

        private static void Prefix(Object __instance, out int __state)
        {
            __state = __instance?.Location?.debris?.Count ?? -1;
        }

        private static void Postfix(Object __instance, bool __result, int __state)
        {
            if (!__result) return;
            if (!ActiveEffectsProvider.ActiveBonus("all_drops_up")) return;
            if (__instance == null || __state < 0) return;
            GameLocation loc = __instance.Location;
            if (loc?.debris == null) return;
            if (Game1.random.NextDouble() >= 0.10) return;

            // Round-13 spec: +1 from the rolled set, not full set doubled. See
            // MineOreDropBonus for the +1 rationale and the round-12 Debris.itemId.Value
            // read-path explanation.
            int tx = (int)__instance.TileLocation.X;
            int ty = (int)__instance.TileLocation.Y;
            var candidates = new System.Collections.Generic.List<string>();
            int total = loc.debris.Count;
            for (int i = __state; i < total; i++)
            {
                var d = loc.debris[i];
                string id = d?.item?.QualifiedItemId;
                if (string.IsNullOrEmpty(id)) id = d?.itemId?.Value;
                if (string.IsNullOrEmpty(id)) continue;
                candidates.Add(id);
            }
            if (candidates.Count == 0) return;

            string pickedId = candidates[Game1.random.Next(candidates.Count)];
            Game1.createObjectDebris(pickedId, tx, ty, loc);
            BonusDropEffects.Play(loc, tx, ty);
            PatchLog.Info(
                $"all_drops_up: source '{__instance.QualifiedItemId}' → +1 '{pickedId}' " +
                $"(picked from {candidates.Count} vanilla drop(s)) at ({tx}, {ty}) on " +
                $"{loc.NameOrUniqueName}.");
        }
    }

    [HarmonyPatch(typeof(Object), nameof(Object.sellToStorePrice))]
    internal static class SellPricePatch
    {
        // ReSharper disable once InconsistentNaming — Harmony convention.
        private static void Postfix(ref int __result)
        {
            if (!ActiveEffectsProvider.ActiveLiability("all_sell_prices_down"))
                return;
            __result = System.Math.Max(1, __result / 2);
        }
    }
}
