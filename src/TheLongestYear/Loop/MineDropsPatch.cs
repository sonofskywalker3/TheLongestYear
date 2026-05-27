using HarmonyLib;
using StardewValley;
using StardewValley.Locations;
using TheLongestYear.Core;

namespace TheLongestYear.Loop
{
    /// <summary>
    /// Mining bonus (mine_drops_up): 30% extra drop on ore/coal hits inside MineShaft.
    /// Applied as a postfix on Object.performToolAction — fires only when the object was
    /// destroyed (returns true) and the location is a MineShaft.
    ///
    /// Foraging liability (mines_closed — HARD): blocks mine elevator + descent ladder.
    /// Applied as a prefix on MineShaft.checkAction — swallows the action for tile 112
    /// (elevator) and tile 173 (descend ladder) and shows an info dialogue.
    /// This is a hard block: no mine progress is possible this week for Foraging theme.
    /// </summary>
    [HarmonyPatch(typeof(Object), nameof(Object.performToolAction))]
    internal static class MineOreDropBonus
    {
        // ReSharper disable once InconsistentNaming — Harmony convention.
        private static void Postfix(Object __instance, bool __result)
        {
            if (!__result) return; // object not destroyed
            if (!ActiveEffectsProvider.ActiveBonus("mine_drops_up")) return;
            if (!(Game1.currentLocation is MineShaft)) return;

            string qid = __instance.QualifiedItemId;
            // Apply to ore and coal; stone (390) and wood (388) excluded by resolver.
            if (!BonusDropResolver.ShouldGrantExtraDrop("mine_drops_up", qid, Game1.random))
                return;

            Game1.createObjectDebris(qid,
                (int)__instance.TileLocation.X,
                (int)__instance.TileLocation.Y,
                Game1.player.UniqueMultiplayerID);
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
}
