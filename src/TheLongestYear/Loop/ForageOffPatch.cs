using HarmonyLib;
using StardewValley;
using StardewValley.Locations;
using TheLongestYear.Core;

namespace TheLongestYear.Loop
{
    /// <summary>
    /// Liability: forage_off (Mining theme only — JC-1 resolved 2026-05-27).
    /// Suppresses wild forage SPAWN by skipping <see cref="GameLocation.spawnObjects"/> for
    /// non-mine outdoor locations. Mine mushrooms are suppressed separately (see below).
    ///
    /// Side-effect (JC-4, accepted for v1): weed/stone debris spawns live inside
    /// spawnObjects -> spawnWeedsAndStones, so they are also suppressed. Flagged for
    /// playtest review — if too punishing, switch to a more surgical bump of
    /// numberOfSpawnedObjectsOnMap before the forage loop.
    /// </summary>
    [HarmonyPatch(typeof(GameLocation), "spawnObjects")]
    internal static class ForageOffPatch
    {
        // ReSharper disable once InconsistentNaming — Harmony convention.
        private static bool Prefix(GameLocation __instance)
        {
            if (!ActiveEffectsProvider.ActiveLiability("forage_off"))
                return true; // run original

            // Only suppress forage-capable outdoor locations.
            // MineShaft is excluded here — its mushroom path is handled below.
            if (__instance is MineShaft)
                return true;

            if (!__instance.IsOutdoors)
                return true;

            // Skip spawnObjects for this location this day.
            return false;
        }
    }

    /// <summary>
    /// Mine mushroom suppression for forage_off: the mushroom rainbow-light spawn
    /// in <see cref="MineShaft"/> (getMineArea() == 80 path) adds the level to
    /// <see cref="MineShaft.mushroomLevelsGeneratedToday"/>. We can't easily prefix the
    /// anonymous lambda inside loadLevel, so we postfix <see cref="MineShaft.loadLevel"/>
    /// and clear the rainbow-light objects that were just placed.
    ///
    /// Implementation: after loadLevel, if forage_off is active and the mine is in
    /// area 80, iterate objects and remove IsSpawnedObject items whose category is forage.
    /// </summary>
    [HarmonyPatch(typeof(MineShaft), "loadLevel")]
    internal static class MineMushroomForageOffPatch
    {
        private static void Postfix(MineShaft __instance)
        {
            if (!ActiveEffectsProvider.ActiveLiability("forage_off"))
                return;

            if (__instance.getMineArea() != 80)
                return;

            // Remove spawned mushroom objects (IsSpawnedObject + forage category).
            var toRemove = new System.Collections.Generic.List<Microsoft.Xna.Framework.Vector2>();
            foreach (var pair in __instance.objects.Pairs)
            {
                if (pair.Value.IsSpawnedObject && pair.Value.isForage())
                    toRemove.Add(pair.Key);
            }
            foreach (var tile in toRemove)
                __instance.objects.Remove(tile);
        }
    }
}
