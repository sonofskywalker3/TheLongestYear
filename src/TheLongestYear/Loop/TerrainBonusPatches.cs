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
    /// successful 10% roll. That captures whatever vanilla actually dropped — wood + sap +
    /// seeds for a tree, hardwood + coal for a big stump, gold/iridium for a meteorite, etc.
    /// — without us mirroring each branch.
    /// </summary>
    internal static class TerrainBonusPatches
    {
        /// <summary>Shared roll + double helper. Returns true when bonus fired (caller may
        /// then trigger the audio/visual effect). Returns false if the bonus was inactive,
        /// the roll missed, no new debris were added, or any required reference was null.</summary>
        public static bool TryDoubleNewDrops(GameLocation loc, int startDebrisCount,
            int tileX, int tileY)
        {
            if (loc?.debris == null || startDebrisCount < 0) return false;
            if (!ActiveEffectsProvider.ActiveBonus("all_drops_up")) return false;
            if (Game1.random.NextDouble() >= 0.10) return false;

            // See MineOreDropBonus for the round-12 root cause: vanilla Object debris has
            // .item == null and stores the id in Debris.itemId.Value. Clone via the string id.
            int doubled = 0;
            int total = loc.debris.Count;
            for (int i = startDebrisCount; i < total; i++)
            {
                var d = loc.debris[i];
                string id = d?.item?.QualifiedItemId;
                if (string.IsNullOrEmpty(id)) id = d?.itemId?.Value;
                if (string.IsNullOrEmpty(id)) continue;

                Game1.createObjectDebris(id, tileX, tileY, loc);
                doubled++;
            }
            if (doubled == 0) return false;

            BonusDropEffects.Play(loc, tileX, tileY);
            PatchLog.Info(
                $"all_drops_up (terrain): doubled {doubled} drop(s) at ({tileX}, {tileY}) on " +
                $"{loc.NameOrUniqueName}.");
            return true;
        }
    }

    /// <summary>Tree chopping — doubles wood/sap/seeds/whatever drops on the 10% Mixed roll.</summary>
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
    /// amethyst clusters, meteorites. Same snapshot-diff doubling on the 10% Mixed roll.</summary>
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
}
