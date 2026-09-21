using System.Collections.Generic;
using HarmonyLib;
using StardewValley.Menus;
using TheLongestYear.Core;

namespace TheLongestYear.Patches
{
    /// <summary>
    /// Sets the flavor on every flavored bundle slot, so a Dried Fruit slot names one fruit and
    /// demands that fruit. Plan 2026-09-21-flavored-bundle-slots, step 5.
    ///
    /// <c>BundleIngredientDescription.preservesId</c> is the field that does everything: with it
    /// set, vanilla's menu builds a flavored item and shows "Dried Apples"
    /// (JunimoNoteMenu.cs:1617-1620), and <c>Bundle.IsValidItemForThisIngredientDescription</c>
    /// accepts only that fruit (Bundle.cs:226-232). Without it the name falls back to the bare
    /// word "Dried" and any flavor is accepted, which is the bug this fixes.
    ///
    /// A bundle's ingredients come out of the data string as (id, stack, quality) triples
    /// (Bundle.cs:133), so the flavor cannot ride in the board data. It is generated with the
    /// board and persisted beside it in <c>MetaState.WrittenBoardFlavors</c>. Mr. Raccoon's
    /// bundles re-derive theirs from a seed instead (Raccoon.cs:136); we store ours because the
    /// board itself is stored for the same reason, so a data mod that shifts the pools cannot
    /// change which fruit a slot names in the middle of a run.
    ///
    /// Boards written before 0.18.33 have no map and get no flavors. That is deliberate: their
    /// stacks were rolled against the machine's throughput for "any dried fruit", and pinning a
    /// fruit onto one would turn 18 Dried Fruit into 90 apples. Those slots keep the "Any Dried
    /// Fruit" label instead (<see cref="FlavorlessSlotLabelPatch"/>).
    ///
    /// Postfixes the <c>Bundle</c> constructor rather than the menu, because the ingredient list
    /// is what both the display and the match read.
    /// </summary>
    [HarmonyPatch(typeof(Bundle))]
    internal static class FlavoredSlotPatch
    {
        /// <summary>Set by ModEntry at save load: the live board's flavor map, or null in Vanilla
        /// board mode, on a pre-0.18.33 board, and on the title screen.</summary>
        public static System.Func<IReadOnlyDictionary<string, string>> FlavorsProvider;

        // The data-string constructor: (bundleIndex, rawBundleValue, completedIngredients, position,
        // rewardText, menu). Verified by reflection against the shipped Stardew Valley.dll, where
        // Bundle has exactly two constructors. The other one takes a ready-made ingredient list and
        // belongs to Mr. Raccoon, who sets his own flavors, so it is deliberately not patched.
        // ReSharper disable once UnusedMember.Local - discovered by the manual PatchClassProcessor scan.
        [HarmonyPostfix]
        [HarmonyPatch(MethodType.Constructor, typeof(int), typeof(string), typeof(bool[]),
            typeof(Microsoft.Xna.Framework.Point), typeof(string), typeof(JunimoNoteMenu))]
        // ReSharper disable once InconsistentNaming - Harmony convention.
        private static void AfterConstruct(Bundle __instance)
        {
            IReadOnlyDictionary<string, string> flavors = FlavorsProvider?.Invoke();
            if (flavors == null || flavors.Count == 0) return;
            if (__instance?.ingredients == null) return;

            // BundleIngredientDescription is a STRUCT, so the list hands out a copy: set the
            // flavor on the copy and put it back, or nothing changes.
            for (int i = 0; i < __instance.ingredients.Count; i++)
            {
                BundleIngredientDescription ingredient = __instance.ingredients[i];
                if (ingredient.preservesId != null) continue;
                if (!FlavoredSlotRules.IsFlavored(ingredient.id)) continue;
                if (!flavors.TryGetValue(FlavoredSlotPass.KeyFor(__instance.bundleIndex, i), out string input)) continue;

                // preservesId is the UNQUALIFIED id: vanilla resolves it as "(O)" + value when it
                // builds the flavored item (Object.loadDisplayName) and compares it against
                // preservedParentSheetIndex, which holds the bare number.
                ingredient.preservesId = BundleParsing.StripQualifier(input);
                __instance.ingredients[i] = ingredient;
            }
        }
    }
}
