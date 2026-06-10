using HarmonyLib;
using StardewValley;
using StardewValley.Locations;
using TheLongestYear.Core;

namespace TheLongestYear.Loop
{
    /// <summary>
    /// Mining bonus (mine_drops_up): 20% chance — when a rock is destroyed in a MineShaft —
    /// to DOUBLE every item the destruction dropped (ore, coal, geode, gems, all of it).
    /// Mixed bonus (all_drops_up): <see cref="BonusDropResolver.MixedAllDropsChance"/>, same
    /// doubling, but anywhere a stone breaks (Mines, Farm, Quarry, …).
    ///
    /// 2026-05-29 playtest evolution: the first cut returned a fixed +1 ore keyed by stoneId,
    /// but stones in the mines drop a *variable* set (a single plain stone can yield stone +
    /// coal + ore + a random gem on the same destruction). User: "I want everything it drops
    /// to be doubled if you roll right." Implementation now patches
    /// <see cref="GameLocation.OnStoneDestroyed"/> with a prefix that snapshots
    /// <c>__instance.debris.Count</c> and a postfix that, on a successful roll, iterates the
    /// newly-added debris and clones each item back into the location. That captures the
    /// vanilla drop set exactly, so the doubling tracks every special-case branch
    /// (DROP_QI_BEANS, frozen geodes on 343/450, mystic-stone gems, ladder fragments) without
    /// us having to mirror each one.
    ///
    /// Foraging liability (mines_closed — HARD): blocks mine elevator + descent ladder.
    /// Applied as a prefix on MineShaft.checkAction — swallows the action for tile 112
    /// (elevator) and tile 173 (descend ladder) and shows an info dialogue.
    /// This is a hard block: no mine progress is possible this week for Foraging theme.
    /// </summary>
    [HarmonyPatch(typeof(GameLocation), nameof(GameLocation.OnStoneDestroyed),
        new System.Type[] { typeof(string), typeof(int), typeof(int), typeof(Farmer) })]
    internal static class MineOreDropBonus
    {
        // ReSharper disable InconsistentNaming — Harmony convention.

        /// <summary>Snapshot the debris count before vanilla runs so the postfix can diff for
        /// items added by this destruction. Stored in Harmony's per-call __state.</summary>
        private static void Prefix(GameLocation __instance, out int __state)
        {
            __state = __instance?.debris?.Count ?? -1;
        }

        private static void Postfix(GameLocation __instance, string stoneId, int x, int y,
            Farmer who, int __state)
        {
            // 2026-05-29 round 9: unconditional trace so we can confirm the patch is wired.
            // The prior round confirmed Object.performToolAction was firing but never saw
            // ore destruction; we then moved to OnStoneDestroyed but a full mining run
            // produced zero bonus log lines. Trace will tell us whether the postfix is
            // entering at all and what the debris-diff looks like.
            int newCount = (__instance?.debris?.Count ?? 0) - System.Math.Max(0, __state);
            PatchLog.Trace(
                $"OnStoneDestroyed postfix: stoneId='{stoneId}', loc='{__instance?.NameOrUniqueName}', " +
                $"newDebrisAdded={newCount}, mineBonus={ActiveEffectsProvider.ActiveBonus("mine_drops_up")}, " +
                $"allBonus={ActiveEffectsProvider.ActiveBonus("all_drops_up")}.");

            if (string.IsNullOrEmpty(stoneId)) return;
            if (__state < 0 || __instance?.debris == null) return;

            // 2026-05-29 round 7: mine_drops_up fires on rocks/nodes ANYWHERE — overworld
            // (Farm, Quarry, Backwoods boulder) AND inside MineShaft. User: "only rocks and
            // nodes, whether in the overworld or the mine." Dropping the prior MineShaft gate.
            bool mineBonus = ActiveEffectsProvider.ActiveBonus("mine_drops_up");
            bool allBonus  = ActiveEffectsProvider.ActiveBonus("all_drops_up");
            if (!mineBonus && !allBonus) return;

            // Tier the roll: mine_drops_up takes priority at 20%; falls back to all_drops_up
            // when only Mixed is picked. Single roll covers ALL drops added by this
            // destruction — user spec is "everything doubled" on a successful roll, not
            // per-item independent rolls.
            // 2026-05-29 user spec rebalance: mine_drops_up 30% → 20%.
            // all_drops_up (Mixed) uses BonusDropResolver.MixedAllDropsChance (10% baseline).
            string firingBonus;
            double threshold;
            if (mineBonus) { firingBonus = "mine_drops_up"; threshold = 0.20; }
            else           { firingBonus = "all_drops_up";  threshold = BonusDropResolver.MixedAllDropsChance; }

            // 2026-05-29 round 12: log the actual rolled value on every event so we can
            // disambiguate "bad luck", "RNG always >= threshold" (broken random), and
            // "rolled in but bonus path threw" (exception swallowed by Harmony).
            double roll = Game1.random.NextDouble();
            if (roll >= threshold)
            {
                PatchLog.Trace($"{firingBonus}: roll={roll:F3} >= {threshold:F2} → no bonus.");
                return;
            }
            PatchLog.Trace($"{firingBonus}: roll={roll:F3} < {threshold:F2} → bonus rolling in.");

            try
            {
                // Round-13 spec correction: user playtest confirmed +1 per successful roll,
                // NOT the full drop set doubled. Doubling a loaded node (stone + coal +
                // copper + gem on one swing) into 8 items reads as OP; +1 picked at random
                // from what vanilla rolled keeps the bonus felt without snowballing.
                //
                // Round-12 root cause for the read path (still applies): vanilla creates
                // Object-bearing debris via Game1.createObjectDebris(string id, …) which
                // leaves Debris.item null and stores the id in Debris.itemId.Value. Read
                // the string id; clone via Game1.createObjectDebris which uses the same
                // constructor vanilla used so the visual + collection behaviour matches.
                // Quality rides along (khauser13 2026-06-10) — see TerrainBonusPatches for the
                // item.Quality / Debris.itemQuality read split.
                var candidates = new System.Collections.Generic.List<(string Id, int Quality)>();
                int total = __instance.debris.Count;
                for (int i = __state; i < total; i++)
                {
                    var d = __instance.debris[i];
                    string id = d?.item?.QualifiedItemId;
                    int quality = !string.IsNullOrEmpty(id) ? d.item.Quality : (d?.itemQuality ?? 0);
                    if (string.IsNullOrEmpty(id)) id = d?.itemId?.Value;
                    if (string.IsNullOrEmpty(id)) continue;
                    candidates.Add((id, quality));
                }
                if (candidates.Count == 0)
                {
                    PatchLog.Trace($"{firingBonus}: rolled in but no item-bearing debris in diff (snapshot={__state}, total={total}).");
                    return;
                }

                var picked = candidates[Game1.random.Next(candidates.Count)];
                Game1.createObjectDebris(picked.Id, x, y, -1, picked.Quality, 1f, __instance);
                BonusDropEffects.Play(__instance, x, y);

                PatchLog.Info(
                    $"{firingBonus}: stone '{stoneId}' destroyed → +1 '{picked.Id}' (Q{picked.Quality}) " +
                    $"(picked from {candidates.Count} vanilla drop(s)) at ({x}, {y}) on " +
                    $"{__instance.NameOrUniqueName}.");
            }
            catch (System.Exception ex)
            {
                PatchLog.Trace($"{firingBonus}: bonus path THREW after successful roll: {ex.GetType().Name}: {ex.Message}");
            }
        }
    }

    [HarmonyPatch(typeof(MineShaft), nameof(MineShaft.checkAction))]
    internal static class MinesClosedPatch
    {
        // ReSharper disable once InconsistentNaming — Harmony convention.
        private static bool Prefix(MineShaft __instance, xTile.Dimensions.Location tileLocation, ref bool __result)
        {
            if (!ActiveEffectsProvider.ActiveLiability("mines_closed"))
                return true;

            // Vanilla only activates the elevator on floors <= 120; tile 112 is inert otherwise.
            // Don't show our spurious dialogue when vanilla would have ignored the interaction.
            if (__instance.mineLevel > 120)
                return true;

            int tileIndex = __instance.getTileIndexAt(tileLocation, "Buildings", "mine");

            // Tile 112 = elevator, tile 173 = descend ladder.
            if (tileIndex == 112 || tileIndex == 173)
            {
                Game1.drawObjectDialogue("The mines feel uneasy this week. The elevator and lower ladders will not respond.");
                __result = true;
                return false; // skip original
            }

            return true;
        }
    }

    /// <summary>
    /// Foraging liability (mines_closed) — entrance block.
    ///
    /// 2026-05-28 playtest correction: the prior implementation blocked elevator + descent
    /// ladder INSIDE the mine, but let the player enter floor 1 freely. User: "floor 1 needs
    /// to be blocked, floor 0 is accessible" — i.e. the player should be able to walk up to
    /// the Mountain mine entrance but NOT enter the mineshaft at all.
    ///
    /// Intercepts <see cref="GameLocation.performAction(string[], Farmer, Location)"/> for the
    /// action verbs that warp a player INTO the mineshaft from outside: "Mine" and
    /// "NextMineLevel" (the Mountain mine-entrance tile and any vanilla NextMineLevel
    /// staircase), and "MineElevator" (the elevator tile in Mountain that would skip floors).
    /// Blocking all three keeps the player in Mountain regardless of which Mountain tile they
    /// click. The internal-descent patch <see cref="MinesClosedPatch"/> above remains the
    /// fallback for any path that's already inside the shaft.
    /// </summary>
    [HarmonyPatch(typeof(GameLocation), nameof(GameLocation.performAction), new System.Type[] { typeof(string[]), typeof(Farmer), typeof(xTile.Dimensions.Location) })]
    internal static class MinesEntranceClosedPatch
    {
        // ReSharper disable once InconsistentNaming — Harmony convention.
        private static bool Prefix(string[] action, Farmer who, ref bool __result)
        {
            if (!ActiveEffectsProvider.ActiveLiability("mines_closed"))
                return true;
            if (action == null || action.Length == 0)
                return true;
            if (!who.IsLocalPlayer)
                return true;

            string verb = action[0];
            if (verb != "Mine" && verb != "NextMineLevel" && verb != "MineElevator")
                return true;

            Game1.drawObjectDialogue("The mines feel uneasy this week. The entrance has been closed off.");
            __result = true;
            return false; // skip original — no warp into the shaft
        }
    }
}
