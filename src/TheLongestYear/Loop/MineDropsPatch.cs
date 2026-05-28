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
