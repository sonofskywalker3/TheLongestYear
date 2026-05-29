using HarmonyLib;
using StardewValley;
using TheLongestYear.Core;

namespace TheLongestYear.Loop
{
    /// <summary>
    /// Mixed bonus (all_drops_up): 10% extra drop on ANY tool-destroyed object.
    /// Includes stone and wood unlike forage_yield_up/mine_drops_up.
    ///
    /// Mixed liability (all_sell_prices_down): every Object.sellToStorePrice multiplied by 0.5,
    /// floored at 1g. Applies across all item types.
    /// </summary>
    [HarmonyPatch(typeof(Object), nameof(Object.performToolAction))]
    internal static class AllDropsBonusPatch
    {
        // ReSharper disable once InconsistentNaming — Harmony convention.
        private static void Postfix(Object __instance, bool __result)
        {
            if (!__result) return;
            if (!ActiveEffectsProvider.ActiveBonus("all_drops_up")) return;
            // 2026-05-29 playtest fix: never bonus on weeds/twigs (same rationale as
            // mine_drops_up — see MineDropsPatch.cs). all_drops_up was also dropping +1 of
            // the source object instead of the actual loot for these.
            if (__instance.IsWeeds() || __instance.IsTwig()) return;
            if (!BonusDropResolver.ShouldGrantExtraDrop("all_drops_up", __instance.QualifiedItemId, Game1.random))
                return;

            int tx = (int)__instance.TileLocation.X;
            int ty = (int)__instance.TileLocation.Y;
            Game1.createObjectDebris(__instance.QualifiedItemId, tx, ty,
                Game1.player.UniqueMultiplayerID);
            BonusDropEffects.Play(Game1.currentLocation, tx, ty);

            PatchLog.Info(
                $"all_drops_up: +1 '{__instance.QualifiedItemId}' at ({tx}, {ty}) on " +
                $"{Game1.currentLocation?.NameOrUniqueName}.");
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
