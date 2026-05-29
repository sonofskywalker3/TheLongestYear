using HarmonyLib;
using StardewValley;
using TheLongestYear.Core;

namespace TheLongestYear.Loop
{
    /// <summary>
    /// Foraging bonus: 25% chance for +1 on any newly-spawned forage item (excludes stone/wood).
    /// Runs as a postfix on <see cref="GameLocation.spawnObjects"/> so the base spawn logic
    /// runs first and we iterate the resulting IsSpawnedObject pool.
    /// Stone (390) and wood (388) are excluded by BonusDropResolver — the forage_yield_up id
    /// only fires on category-forage items (categories -79, -81, -80, -75, -23 or forage_item tag).
    /// </summary>
    [HarmonyPatch(typeof(GameLocation), "spawnObjects")]
    internal static class ForageYieldPatch
    {
        private static void Postfix(GameLocation __instance)
        {
            if (!ActiveEffectsProvider.ActiveBonus("forage_yield_up"))
                return;

            // Collect tiles to avoid mutating the dictionary while iterating.
            var toBonus = new System.Collections.Generic.List<(Microsoft.Xna.Framework.Vector2 tile, Object obj)>();
            foreach (var pair in __instance.objects.Pairs)
            {
                Object obj = pair.Value;
                if (obj.IsSpawnedObject && obj.isForage())
                    toBonus.Add((pair.Key, obj));
            }

            foreach (var (tile, obj) in toBonus)
            {
                if (!BonusDropResolver.ShouldGrantExtraDrop("forage_yield_up", obj.QualifiedItemId, Game1.random))
                    continue;

                // Place a clone adjacent — try up to 8 neighbouring tiles.
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
                            $"forage_yield_up: +1 '{obj.QualifiedItemId}' at ({(int)candidate.X}, " +
                            $"{(int)candidate.Y}) on {__instance.NameOrUniqueName}.");
                        break;
                    }
                }
            }
        }
    }
}
