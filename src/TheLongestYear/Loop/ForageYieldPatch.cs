using HarmonyLib;
using Microsoft.Xna.Framework;
using StardewValley;
using TheLongestYear.Core;

namespace TheLongestYear.Loop
{
    /// <summary>
    /// Foraging bonus (forage_yield_up): 20% chance to gain +1 of a foraged item <b>on
    /// pickup</b> — the same mechanic as the vanilla Gatherer profession (GameLocation.cs
    /// :7740), an independent roll that stacks with it.
    ///
    /// 2026-05-30 user feedback: the prior implementation spawned a second forage <i>object</i>
    /// on an adjacent tile during the overnight <c>GameLocation.spawnObjects</c> pass. The
    /// player disliked seeing a duplicate sprite appear on the ground — "same mechanic, just a
    /// 20% chance to gain +1 on pickup." So the bonus now fires when the player actually grabs
    /// the forage, dropping the extra straight into the inventory with no world clutter.
    ///
    /// Hook: a prefix on <see cref="GameLocation.checkAction"/> snapshots the live spawned-
    /// forage objects; the postfix sees which tile was removed (a successful pickup removes the
    /// object) and rolls <see cref="BonusDropResolver.ShouldGrantExtraDrop"/> for it. Cloning
    /// the <i>live</i> source object (not a pre-pickup copy) preserves the harvest quality
    /// vanilla assigns during the pickup. Stone/wood are excluded by the resolver (a no-op
    /// here — forage is never stone/wood — but keeps the policy consistent).
    ///
    /// Inventory-full overflow is ignored, matching the vanilla Gatherer double.
    /// </summary>
    [HarmonyPatch(typeof(GameLocation), nameof(GameLocation.checkAction),
        new[] { typeof(xTile.Dimensions.Location), typeof(xTile.Dimensions.Rectangle), typeof(Farmer) })]
    internal static class ForageYieldPatch
    {
        // ReSharper disable InconsistentNaming — Harmony convention.

        /// <summary>Snapshot the spawned-forage objects present before the action runs, keyed
        /// by tile. Null state when the bonus is inactive or there's no forage to pick.</summary>
        private static void Prefix(GameLocation __instance,
            out System.Collections.Generic.Dictionary<Vector2, Object> __state)
        {
            __state = null;
            if (__instance?.objects == null) return;
            if (!ActiveEffectsProvider.ActiveBonus("forage_yield_up")) return;

            System.Collections.Generic.Dictionary<Vector2, Object> snapshot = null;
            foreach (var pair in __instance.objects.Pairs)
            {
                Object o = pair.Value;
                if (o == null || !o.IsSpawnedObject || !o.isForage()) continue;
                snapshot ??= new System.Collections.Generic.Dictionary<Vector2, Object>();
                snapshot[pair.Key] = o;
            }
            __state = snapshot;
        }

        private static void Postfix(GameLocation __instance, Farmer who,
            System.Collections.Generic.Dictionary<Vector2, Object> __state)
        {
            if (__state == null || who == null || __instance?.objects == null) return;
            if (!ActiveEffectsProvider.ActiveBonus("forage_yield_up")) return;

            // A successful forage pickup removes exactly one object from the tile. Find the
            // tile that disappeared this action — that's what the player just grabbed.
            foreach (var kv in __state)
            {
                if (__instance.objects.ContainsKey(kv.Key)) continue;   // still there — not picked

                Object src = kv.Value;
                if (!BonusDropResolver.ShouldGrantExtraDrop("forage_yield_up", src.QualifiedItemId, Game1.random))
                    return;

                Object extra = (Object)src.getOne();
                extra.Quality = src.Quality;   // carry the harvest quality vanilla just assigned
                who.addItemToInventoryBool(extra);

                BonusDropEffects.Play(__instance, (int)kv.Key.X, (int)kv.Key.Y);
                PatchLog.Info(
                    $"forage_yield_up: +1 '{src.QualifiedItemId}' (Q{src.Quality}) into inventory on " +
                    $"pickup at ({(int)kv.Key.X}, {(int)kv.Key.Y}) on {__instance.NameOrUniqueName}.");
                return;   // at most one pickup per action
            }
        }
    }
}
