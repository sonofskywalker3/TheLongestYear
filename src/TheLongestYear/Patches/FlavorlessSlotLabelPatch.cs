using System.Collections.Generic;
using HarmonyLib;
using StardewValley.Menus;
using TheLongestYear.Core;

namespace TheLongestYear.Patches
{
    /// <summary>
    /// Nexus bug 1137151: an Artisan slot asking for dried fruit read only "Dried", and a smoked
    /// fish slot only "Smoked", so there was no way to tell what the bundle wanted.
    ///
    /// <see cref="FlavorlessBundleSlots"/> holds the why. In short: a bundle's ingredients carry
    /// no flavor, the menu therefore shows the base display name, and for these three objects that
    /// name is the bare word "Dried" or "Smoked". The slot does take any flavor, so this writes
    /// the honest label ("Any Dried Fruit") over the hover text instead of narrowing the ask.
    ///
    /// Both places vanilla fills <c>ingredientList</c> are covered: <c>setUpBundleSpecificPage</c>
    /// when the page opens, and <c>gameWindowSizeChanged</c>, which clears and rebuilds the same
    /// list on a resize and would otherwise put the bare name back.
    ///
    /// Hover text only. The component's item, icon and matching are untouched, so nothing about
    /// what the slot accepts changes — and if a future game version does start giving these slots
    /// a real flavor, <see cref="Relabel"/> leaves any name that is not one of the three alone.
    ///
    /// Since 0.18.33 this is the FALLBACK, not the normal case: a board generated from that
    /// version on names the fruit outright (<see cref="FlavoredSlotPatch"/>), and only a board
    /// written before it still has an "any" slot to label.
    /// </summary>
    [HarmonyPatch(typeof(JunimoNoteMenu))]
    internal static class FlavorlessSlotLabelPatch
    {
        // ReSharper disable once UnusedMember.Local - discovered by the manual PatchClassProcessor scan.
        [HarmonyPostfix]
        [HarmonyPatch("setUpBundleSpecificPage")]
        // ReSharper disable once InconsistentNaming - Harmony convention.
        private static void AfterPageSetUp(JunimoNoteMenu __instance) => Relabel(__instance);

        // ReSharper disable once UnusedMember.Local - discovered by the manual PatchClassProcessor scan.
        [HarmonyPostfix]
        [HarmonyPatch("gameWindowSizeChanged")]
        // ReSharper disable once InconsistentNaming - Harmony convention.
        private static void AfterResize(JunimoNoteMenu __instance) => Relabel(__instance);

        private static void Relabel(JunimoNoteMenu menu)
        {
            List<ClickableTextureComponent> list = menu?.ingredientList;
            if (list == null) return;

            foreach (ClickableTextureComponent slot in list)
            {
                string itemId = slot?.item?.ItemId;
                if (itemId == null) continue;
                if (FlavorlessBundleSlots.LabelKeyFor(itemId) is not string key) continue;
                // A slot that HAS a flavor already reads "Dried Apples" and means it
                // (FlavoredSlotPatch). Its item still carries the base id "DriedFruit", so match
                // on the flavor itself, or this would relabel a specific ask as "Any Dried Fruit".
                if (slot.item is StardewValley.Object flavored && flavored.preservedParentSheetIndex.Value != null)
                    continue;
                slot.hoverText = Strings.Get(key);
            }
        }
    }
}
