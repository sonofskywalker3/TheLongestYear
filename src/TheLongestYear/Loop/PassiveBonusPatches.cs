using HarmonyLib;
using StardewValley;
using TheLongestYear.Core;

namespace TheLongestYear.Loop
{
    /// <summary>
    /// Tiered passive accelerators (added 2026-05-29) — three independent 5-tier chains in
    /// the Obtainability category that grant flat +5%-per-tier (max +25%) chances on the
    /// matching world event. Each is a separate Harmony patch so it fires alongside the
    /// theme-week bonus without coupling to it: a Farming-week + Green Thumb V crop gets
    /// BOTH the deterministic +1 tick on days 2/5 and a 25%/day passive +1 tick.
    ///
    /// Design intent: cheap (75 JP tier 1) early-game upgrades that compound for players
    /// stuck in JP poverty in the first couple of runs. The boost is small enough not to
    /// trivialise theme weeks but compounds across an entire run's worth of crops/stones/
    /// forage tiles.
    ///
    /// Read pattern matches <see cref="MixedSeedsPatch"/>: UpgradeChecker.GetTier walks the
    /// chain "<prefix>_1" .. "<prefix>_5" and returns the highest owned tier (0 if none).
    /// </summary>

    /// <summary>Green Thumb (green_thumb_1..5): on every watered crop, X% chance per day to
    /// advance growth by one extra tick. Layered as a separate postfix on Crop.newDay so it
    /// runs after <see cref="CropGrowthPatch"/> — when both fire on the same crop on the same
    /// day, the crop gains 2 extra ticks (one from theme, one from passive). That's the
    /// intended "everything compounds" behaviour, not a bug.</summary>
    [HarmonyPatch(typeof(Crop), nameof(Crop.newDay))]
    internal static class GreenThumbPatch
    {
        // ReSharper disable once InconsistentNaming — Harmony convention.
        private static void Postfix(Crop __instance, int state)
        {
            if (__instance == null) return;
            if (state != 1) return;  // unwatered — vanilla skipped growth
            if (__instance.dead.Value) return;
            if (__instance.fullyGrown.Value && __instance.dayOfCurrentPhase.Value > 0) return;

            int tier = UpgradeChecker.GetTier("green_thumb", 5);
            if (tier == 0) return;
            double chance = tier * 0.05;
            if (Game1.random.NextDouble() >= chance) return;

            AdvanceOneTick(__instance);
        }

        /// <summary>Bump the crop forward exactly one day, advancing to the next phase if the
        /// current one finishes. Mirrors the in-line logic from CropGrowthPatch.Postfix; kept
        /// separate so the two patches stay independent.</summary>
        private static void AdvanceOneTick(Crop crop)
        {
            if (crop.phaseDays.Count == 0) return;
            int maxForPhase = crop.phaseDays[System.Math.Min(
                crop.phaseDays.Count - 1, crop.currentPhase.Value)];
            crop.dayOfCurrentPhase.Value = System.Math.Min(
                crop.dayOfCurrentPhase.Value + 1, maxForPhase);
            if (crop.dayOfCurrentPhase.Value >= maxForPhase
                && crop.currentPhase.Value < crop.phaseDays.Count - 1)
            {
                crop.currentPhase.Value++;
                crop.dayOfCurrentPhase.Value = 0;
            }
        }
    }

    /// <summary>Coal Vein (coal_vein_1..5): X% chance per destroyed stone to drop +1 coal.
    /// Separate patch from <see cref="MineOreDropBonus"/> so the theme bonus and this passive
    /// roll independently — a stone smashed during a Mining week with Coal Vein V owned can
    /// trigger both (a +1 picked-from-rolled-set bonus AND +1 coal). Coal item id is "(O)382".</summary>
    [HarmonyPatch(typeof(GameLocation), nameof(GameLocation.OnStoneDestroyed),
        new System.Type[] { typeof(string), typeof(int), typeof(int), typeof(Farmer) })]
    internal static class CoalVeinPatch
    {
        // Coal item id in the qualified-id format used by Game1.createObjectDebris.
        private const string CoalItemId = "(O)382";

        // ReSharper disable once InconsistentNaming — Harmony convention.
        private static void Postfix(GameLocation __instance, string stoneId, int x, int y, Farmer who)
        {
            if (__instance == null) return;
            if (string.IsNullOrEmpty(stoneId)) return;

            int tier = UpgradeChecker.GetTier("coal_vein", 5);
            if (tier == 0) return;
            double chance = tier * 0.05;
            if (Game1.random.NextDouble() >= chance) return;

            Game1.createObjectDebris(CoalItemId, x, y, __instance);
            BonusDropEffects.Play(__instance, x, y);
            PatchLog.Info(
                $"coal_vein (tier {tier}): stone '{stoneId}' destroyed → +1 coal at ({x}, {y}) " +
                $"on {__instance.NameOrUniqueName}.");
        }
    }

    /// <summary>Forager's Eye (foragers_eye_1..5): on every spawnObjects pass (overnight forage
    /// spawn), iterate the newly-placed forage tiles and roll X% per tile to drop a clone on
    /// an adjacent tile. Same placement strategy as <see cref="ForageYieldPatch"/> — try 8
    /// neighbours, first valid one wins. Stacks with the Foraging-week themed bonus: a
    /// Forager's Eye V owner on a Foraging week sees a 25% passive roll plus the themed
    /// forage_yield_up roll on the same tile, each evaluated independently.</summary>
    [HarmonyPatch(typeof(GameLocation), "spawnObjects")]
    internal static class ForagersEyePatch
    {
        // ReSharper disable once InconsistentNaming — Harmony convention.
        private static void Postfix(GameLocation __instance)
        {
            int tier = UpgradeChecker.GetTier("foragers_eye", 5);
            if (tier == 0) return;
            if (__instance == null) return;

            double chance = tier * 0.05;

            // Snapshot first to avoid mutating the dictionary while iterating.
            var toBonus = new System.Collections.Generic.List<(Microsoft.Xna.Framework.Vector2 tile, Object obj)>();
            foreach (var pair in __instance.objects.Pairs)
            {
                Object obj = pair.Value;
                if (obj == null) continue;
                if (!obj.IsSpawnedObject) continue;
                if (!obj.isForage()) continue;
                toBonus.Add((pair.Key, obj));
            }

            foreach (var (tile, obj) in toBonus)
            {
                if (Game1.random.NextDouble() >= chance) continue;

                Microsoft.Xna.Framework.Vector2[] offsets = {
                    new Microsoft.Xna.Framework.Vector2(1,0), new Microsoft.Xna.Framework.Vector2(-1,0),
                    new Microsoft.Xna.Framework.Vector2(0,1), new Microsoft.Xna.Framework.Vector2(0,-1),
                    new Microsoft.Xna.Framework.Vector2(1,1), new Microsoft.Xna.Framework.Vector2(-1,1),
                    new Microsoft.Xna.Framework.Vector2(1,-1), new Microsoft.Xna.Framework.Vector2(-1,-1)
                };
                foreach (var offset in offsets)
                {
                    Microsoft.Xna.Framework.Vector2 candidate = tile + offset;
                    if (__instance.objects.ContainsKey(candidate)) continue;
                    if (!__instance.CanItemBePlacedHere(candidate)) continue;
                    Object clone = (Object)obj.getOne();
                    clone.IsSpawnedObject = true;
                    clone.CanBeGrabbed = true;
                    if (__instance.dropObject(clone, candidate * 64f, Game1.viewport, initialPlacement: true))
                    {
                        BonusDropEffects.Play(__instance, (int)candidate.X, (int)candidate.Y);
                        PatchLog.Info(
                            $"foragers_eye (tier {tier}): +1 '{obj.QualifiedItemId}' at " +
                            $"({(int)candidate.X}, {(int)candidate.Y}) on {__instance.NameOrUniqueName}.");
                        break;
                    }
                }
            }
        }
    }
}
