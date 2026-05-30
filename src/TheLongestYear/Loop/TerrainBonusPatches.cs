using HarmonyLib;
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.TerrainFeatures;
using TheLongestYear.Core;

namespace TheLongestYear.Loop
{
    /// <summary>
    /// Mixed bonus (all_drops_up): doubles drops from chopping trees and breaking large
    /// resource clumps (big stumps / boulders / quartz clusters / meteorites). Mining bonus
    /// (mine_drops_up) is intentionally NOT applied here — user spec is "only rocks and
    /// nodes" for the mining bonus, which means just <see cref="GameLocation.OnStoneDestroyed"/>
    /// (handled by <see cref="MineOreDropBonus"/>).
    ///
    /// Same snapshot-diff pattern as <see cref="MineOreDropBonus"/> and
    /// <see cref="AllDropsBonusPatch"/>: prefix records <c>Location.debris.Count</c>, postfix
    /// iterates the new debris added during this tool-action and clones each item on a
    /// successful Mixed all_drops_up roll. That captures whatever vanilla actually dropped — wood + sap +
    /// seeds for a tree, hardwood + coal for a big stump, gold/iridium for a meteorite, etc.
    /// — without us mirroring each branch.
    /// </summary>
    internal static class TerrainBonusPatches
    {
        // Vanilla object qualified ids that BonusDropResolver excludes from forage_yield_up.
        // Stone and wood drop in bulk from resource clumps already, so adding a 20% extra
        // would feel runaway. Tree-shake usually drops neither (seeds), so this exclusion is
        // a no-op for the shake path but kept consistent with ForageYieldPatch + MineDropsPatch.
        private const string Stone = "(O)390";
        private const string Wood  = "(O)388";

        /// <summary>Shared roll + double helper. Routes by <see cref="ActiveEffectsProvider.BonusId"/>:
        /// <list type="bullet">
        ///   <item><c>all_drops_up</c> (Mixed): <see cref="BonusDropResolver.MixedAllDropsChance"/>
        ///     chance to clone one of the new drops, no filter.</item>
        ///   <item><c>forage_yield_up</c> (Foraging): 20% chance to clone one of the new drops,
        ///     stone/wood excluded — applied ONLY when the caller passes
        ///     <paramref name="applyForageYieldUp"/>=true. 2026-05-29 user report: tree-shake
        ///     drops felt invisible on Foraging week, since shake debris bypasses the
        ///     spawnObjects path that <see cref="ForageYieldPatch"/> covers. TreeShake passes
        ///     true so its seed drops also fire on Foraging weeks; ResourceClump and
        ///     MonsterDrop don't (their drops aren't forage-adjacent — wood/stone/coal/slime).</item>
        /// </list>
        /// Returns true when the bonus fired (caller may then trigger the audio/visual effect).
        /// Returns false if the active bonus doesn't apply to this caller, the roll missed,
        /// no new debris were added, or any required reference was null.
        /// </summary>
        public static bool TryDoubleNewDrops(GameLocation loc, int startDebrisCount,
            int tileX, int tileY, bool applyForageYieldUp = false)
        {
            if (loc?.debris == null || startDebrisCount < 0) return false;

            string bonusId = ActiveEffectsProvider.BonusId;
            bool mixedActive = bonusId == "all_drops_up";
            bool forageActive = applyForageYieldUp && bonusId == "forage_yield_up";
            if (!mixedActive && !forageActive) return false;

            // Round-13 spec: +1 from the rolled set, not full set doubled. See
            // MineOreDropBonus for rationale + the Debris.itemId.Value read path.
            var candidates = new System.Collections.Generic.List<string>();
            int total = loc.debris.Count;
            for (int i = startDebrisCount; i < total; i++)
            {
                var d = loc.debris[i];
                string id = d?.item?.QualifiedItemId;
                if (string.IsNullOrEmpty(id)) id = d?.itemId?.Value;
                if (string.IsNullOrEmpty(id)) continue;
                candidates.Add(id);
            }
            if (candidates.Count == 0) return false;

            double rate;
            string label;
            if (mixedActive)
            {
                rate = BonusDropResolver.MixedAllDropsChance;
                label = "all_drops_up";
            }
            else
            {
                // Foraging path: filter stone/wood out of the candidate pool (BonusDropResolver
                // policy — shared with ForageYieldPatch + MineDropsPatch).
                candidates.RemoveAll(id => id == Stone || id == Wood);
                if (candidates.Count == 0) return false;
                rate = 0.20;
                label = "forage_yield_up";
            }

            if (Game1.random.NextDouble() >= rate) return false;

            string pickedId = candidates[Game1.random.Next(candidates.Count)];
            Game1.createObjectDebris(pickedId, tileX, tileY, loc);
            BonusDropEffects.Play(loc, tileX, tileY);
            PatchLog.Info(
                $"{label} (terrain): +1 '{pickedId}' " +
                $"(picked from {candidates.Count} vanilla drop(s)) at ({tileX}, {tileY}) on " +
                $"{loc.NameOrUniqueName}.");
            return true;
        }
    }

    /// <summary>Tree chopping — doubles wood/sap/seeds/whatever drops on the Mixed all_drops_up roll.</summary>
    [HarmonyPatch(typeof(Tree), nameof(Tree.performToolAction))]
    internal static class TreeAllDropsBonusPatch
    {
        // ReSharper disable InconsistentNaming — Harmony convention.
        private static void Prefix(Tree __instance, out int __state)
        {
            __state = __instance?.Location?.debris?.Count ?? -1;
        }

        private static void Postfix(Tree __instance, bool __result, Vector2 tileLocation, int __state)
        {
            if (!__result) return;  // tree wasn't fully chopped this swing
            if (__instance == null) return;
            TerrainBonusPatches.TryDoubleNewDrops(
                __instance.Location, __state, (int)tileLocation.X, (int)tileLocation.Y);
        }
    }

    /// <summary>Large resource clumps — big stumps, big logs, boulders, quartz/topaz/jade/
    /// amethyst clusters, meteorites. Same snapshot-diff doubling on the Mixed all_drops_up roll.</summary>
    [HarmonyPatch(typeof(ResourceClump), nameof(ResourceClump.performToolAction))]
    internal static class ResourceClumpAllDropsBonusPatch
    {
        // ReSharper disable InconsistentNaming — Harmony convention.
        private static void Prefix(ResourceClump __instance, out int __state)
        {
            __state = __instance?.Location?.debris?.Count ?? -1;
        }

        private static void Postfix(ResourceClump __instance, bool __result, Vector2 tileLocation, int __state)
        {
            if (!__result) return;
            if (__instance == null) return;
            TerrainBonusPatches.TryDoubleNewDrops(
                __instance.Location, __state, (int)tileLocation.X, (int)tileLocation.Y);
        }
    }

    /// <summary>Shaken trees — walking into a tree may drop a seed (and on rare rolls a Mystery
    /// Box / Qi Bean / rare-object-table item). Vanilla creates these via Game1.createItemDebris
    /// inside <see cref="Tree.shake"/>. User asked 2026-05-29 whether shake seeds were getting
    /// the all_drops_up bonus — they weren't (the existing TreeAllDropsBonusPatch covers
    /// performToolAction only, i.e. chopping). Same snapshot-diff +1 on a Mixed all_drops_up roll.</summary>
    [HarmonyPatch(typeof(Tree), nameof(Tree.shake))]
    internal static class TreeShakeAllDropsBonusPatch
    {
        // ReSharper disable InconsistentNaming — Harmony convention.
        private static void Prefix(Tree __instance, out int __state)
        {
            __state = __instance?.Location?.debris?.Count ?? -1;
        }

        private static void Postfix(Tree __instance, Vector2 tileLocation, int __state)
        {
            if (__instance == null) return;
            // Tree shake is the ONE caller that opts into the forage_yield_up extension —
            // shake debris is conceptually forage-adjacent (acorns/maple/pine cones/mahogany
            // seeds + the rare mystery box) and bypasses the spawnObjects path that
            // ForageYieldPatch covers. 2026-05-29 user report: "I shook a bunch of trees and
            // never hit" on Foraging week.
            TerrainBonusPatches.TryDoubleNewDrops(
                __instance.Location, __state, (int)tileLocation.X, (int)tileLocation.Y,
                applyForageYieldUp: true);
        }
    }

    /// <summary>Monster drops — slimes/dust spirits/bats/etc dropping their loot on death.
    /// Vanilla resolves the drop list in <see cref="GameLocation.monsterDrop"/> and adds each
    /// to <c>location.debris</c>. User asked 2026-05-29 whether monster drops were covered by
    /// all_drops_up — they weren't. Same snapshot-diff +1 on a Mixed all_drops_up roll. Mining bonus
    /// (mine_drops_up) intentionally NOT applied here — "mined resources" reads as
    /// rocks/nodes, not creatures.</summary>
    [HarmonyPatch(typeof(GameLocation), nameof(GameLocation.monsterDrop))]
    internal static class MonsterDropAllDropsBonusPatch
    {
        // ReSharper disable InconsistentNaming — Harmony convention.
        private static void Prefix(GameLocation __instance, out int __state)
        {
            __state = __instance?.debris?.Count ?? -1;
        }

        private static void Postfix(GameLocation __instance, int x, int y, int __state)
        {
            if (__instance == null) return;
            // The (x, y) here are pixel coords from monsterDrop — convert back to tile coords
            // for TryDoubleNewDrops' BonusDropEffects.Play (which itself converts tile → pixel).
            TerrainBonusPatches.TryDoubleNewDrops(__instance, __state, x / 64, y / 64);
        }
    }

    /// <summary>Fish caught — fishing-rod catch on a Mixed all_drops_up roll spawns +1 of the same fish at the
    /// same quality directly into the player's inventory. Pond auto-produce skipped (those
    /// aren't player-effort catches). Treasure chest contents NOT covered — treasure is itself
    /// a rare-roll bonus already (treasureCaught flag, openChestEndFunction delivery), and
    /// doubling it would feel like double-dipping on its rarity premium.
    ///
    /// Mine bonus (mine_drops_up) intentionally NOT applied — even when fishing inside the
    /// mines, the catch is a fishing reward, not a mining drop.</summary>
    [HarmonyPatch(typeof(StardewValley.Tools.FishingRod), nameof(StardewValley.Tools.FishingRod.pullFishFromWater))]
    internal static class FishAllDropsBonusPatch
    {
        // ReSharper disable InconsistentNaming — Harmony convention.
        private static void Postfix(StardewValley.Tools.FishingRod __instance, string fishId,
            int fishQuality, bool fromFishPond)
        {
            if (fromFishPond) return;
            if (string.IsNullOrEmpty(fishId)) return;
            if (!ActiveEffectsProvider.ActiveBonus("all_drops_up")) return;
            if (Game1.random.NextDouble() >= BonusDropResolver.MixedAllDropsChance) return;

            var player = Game1.player;
            if (player == null) return;

            try
            {
                Item extra = StardewValley.ItemRegistry.Create(fishId, 1, fishQuality, allowNull: true);
                if (extra == null) return;
                player.addItemToInventoryBool(extra);

                GameLocation loc = player.currentLocation;
                int tx = (int)player.Tile.X;
                int ty = (int)player.Tile.Y;
                if (loc != null) BonusDropEffects.Play(loc, tx, ty);
                PatchLog.Info(
                    $"all_drops_up (fish): +1 '{fishId}' (Q{fishQuality}) into inventory at " +
                    $"({tx}, {ty}) on {loc?.NameOrUniqueName}.");
            }
            catch (System.Exception ex)
            {
                PatchLog.Trace($"all_drops_up (fish): threw {ex.GetType().Name}: {ex.Message}");
            }
        }
    }
}
