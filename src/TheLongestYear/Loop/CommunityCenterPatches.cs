using HarmonyLib;
using StardewValley.Locations;

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
            if (__result || area < 0 || area > 5)
                return;
            // Don't re-reveal a completed area's note — vanilla replaces the note tile with the
            // restored room art and the note position becomes a no-op.
            if (area < __instance.areasComplete.Count && __instance.areasComplete[area])
                return;
            __result = true;
        }
    }
}
