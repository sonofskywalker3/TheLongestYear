using HarmonyLib;
using StardewValley;
using StardewValley.Locations;
using TheLongestYear.Core;

namespace TheLongestYear.Loop
{
    /// <summary>
    /// Harmony patches that override vanilla CC progression gating so all six rooms are donatable
    /// from Spring 1 of every run.
    ///
    /// Vanilla rule (from <see cref="CommunityCenter.shouldNoteAppearInArea"/>): only the Crafts
    /// Room (area 1) Junimo Note appears unconditionally; the other five gate on
    /// <c>numberOfCompleteBundles()</c> thresholds (1, 2, 3, 4 — Pantry/FishTank, BoilerRoom,
    /// Bulletin, Vault). With our 16-week parallel-attack design the player must be able to
    /// donate to every bundle on day 1, so we postfix the gate to return true for any incomplete
    /// area in the range 0–5.
    ///
    /// Same patch also fixes <see cref="CommunityCenter.refreshBundlesIngredientsInfo"/>, which
    /// reads <c>shouldNoteAppearInArea</c> at line 140 of the decompile to decide whether to
    /// include each bundle's ingredients in the tooltip lookup — without this, the Junimo Note
    /// would be visible but ingredient hints would be missing for the unrevealed-in-vanilla rooms.
    /// </summary>
    [HarmonyPatch(typeof(CommunityCenter), nameof(CommunityCenter.shouldNoteAppearInArea))]
    internal static class ShouldNoteAppearPatch
    {
        private static void Postfix(CommunityCenter __instance, int area, ref bool __result)
        {
            if (!RunActivation.IsActive)
                return;
            if (__result || area < 0 || area > 5)
                return;
            // Don't re-reveal a completed area's note — vanilla replaces the note tile with the
            // restored room art and the note position becomes a no-op.
            if (area < __instance.areasComplete.Count && __instance.areasComplete[area])
                return;
            __result = true;
        }
    }

    /// <summary>
    /// Make the Community Center bulletin board (Mixed room, area 5) openable from day 1.
    ///
    /// Vanilla <see cref="CommunityCenter.checkAction"/> special-cases the bulletin board — its
    /// Buildings-layer tile index is <c>1799</c> and, unlike the other five Junimo Notes (tile
    /// indices 1824–1833, which open unconditionally), it only calls <c>checkBundle(5)</c> when
    /// <c>numberOfCompleteBundles() &gt; 2</c>. It's the last room in a normal playthrough.
    ///
    /// TLY reveals all six notes on Spring 1 (<see cref="ShouldNoteAppearPatch"/>) and wants every
    /// room donatable immediately, but nothing patched <c>checkAction</c> — so pressing the board
    /// did nothing (no menu, no log, action silently consumed) until three bundles were already
    /// done. An earlier "Season Goals board" feature anchored at this tile masked the issue; once
    /// it was removed the underlying vanilla gate showed through. This prefix opens area 5 directly
    /// when the board tile is acted on and its room isn't yet complete, bypassing the &gt;2 gate.
    /// </summary>
    [HarmonyPatch(typeof(CommunityCenter), nameof(CommunityCenter.checkAction))]
    internal static class BulletinBoardCheckActionPatch
    {
        // Buildings-layer ("indoors" tilesheet) tile index of the bulletin board, and its CC area.
        private const int BulletinBoardTileIndex = 1799;
        private const int BulletinBoardArea = 5;

        private static bool Prefix(CommunityCenter __instance, xTile.Dimensions.Location tileLocation, ref bool __result)
        {
            if (!RunActivation.IsActive)
                return true;

            // Match vanilla's own lookup at the head of checkAction (getTileIndexAt with the
            // "indoors" tilesheet). Anything that isn't the bulletin board tile defers to vanilla.
            if (__instance.getTileIndexAt(tileLocation, "Buildings", "indoors") != BulletinBoardTileIndex)
                return true;

            // Already-restored room: the note tile is gone; let vanilla no-op cleanly.
            if (BulletinBoardArea < __instance.areasComplete.Count && __instance.areasComplete[BulletinBoardArea])
                return true;

            // Open the bundle menu directly, skipping vanilla's numberOfCompleteBundles() > 2 gate.
            __instance.checkBundle(BulletinBoardArea);
            __result = true;
            return false;
        }
    }
}
