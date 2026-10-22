using HarmonyLib;
using StardewValley.Objects;

namespace TheLongestYear.Scenes
{
    /// <summary>Lets an overnight strike scene hold a chest's lid open (spec 2026-09-21, the
    /// thief). <c>Chest.fixLidFrame</c> runs at the top of the chest's own update and snaps an
    /// unlocked player chest shut, and the game runs that update AFTER the farm event's tick, so
    /// this is the last word on the lid before the frame is drawn. See
    /// <see cref="SceneChestLid.ReapplyAfterFix"/> for the whole story.
    ///
    /// It does nothing at all unless a scene is driving that exact chest this instant.</summary>
    [HarmonyPatch(typeof(Chest), nameof(Chest.fixLidFrame))]
    internal static class SceneChestLidPatch
    {
        // ReSharper disable once InconsistentNaming (Harmony's own parameter name)
        private static void Postfix(Chest __instance) => SceneChestLid.ReapplyAfterFix(__instance);
    }
}
