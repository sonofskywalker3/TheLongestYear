using HarmonyLib;
using StardewValley.Objects;
using TheLongestYear.Core;

namespace TheLongestYear.Loop
{
    /// <summary>Gives the Circle of Warding its own tooltip.
    ///
    /// <c>Data/Furniture</c> has no description field, so <see cref="Furniture.loadDescription"/>
    /// falls back to a line picked purely from the placement restriction: ours is 2 (indoors or
    /// out), which lands on vanilla's "Can be placed as decoration." That says nothing about the
    /// only reason the circle exists (Jeff, 2026-09-10), so the mod supplies its own text for this
    /// one item and leaves every other piece of furniture alone.
    ///
    /// The result is cached in the item's own <c>_description</c> field, so patching
    /// <c>loadDescription</c> costs one call per item rather than one per frame.</summary>
    [HarmonyPatch(typeof(Furniture), "loadDescription")]
    internal static class CircleDescriptionPatch
    {
        // ReSharper disable once InconsistentNaming — Harmony convention.
        // ReSharper disable once UnusedMember.Local — discovered by PatchAll.
        private static void Postfix(Furniture __instance, ref string __result)
        {
            if (__instance?.ItemId == CircleOfWardingService.CircleId)
                __result = Strings.Get("furniture.circle-of-warding.desc");
        }
    }
}
