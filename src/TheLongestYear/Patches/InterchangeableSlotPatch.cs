using HarmonyLib;
using StardewValley;
using StardewValley.Menus;
using TheLongestYear.Core;

namespace TheLongestYear.Patches
{
    /// <summary>
    /// A Community Center slot asking for an egg takes either color of that size (Nexus,
    /// flutegir, 2026-10-10: a 13-egg Animal slot refused the brown eggs). See
    /// <see cref="InterchangeableItems"/> for the rule.
    ///
    /// <c>Bundle.IsValidItemForThisIngredientDescription</c> is the one check every part of the
    /// bundle page asks (Bundle.cs:220): the inventory highlight (JunimoNoteMenu.HighlightObjects),
    /// the full deposit (canAcceptThisItem, tryToDepositThisItem), and the partial deposit
    /// (CanBePartiallyOrFullyDonated counts every valid stack in the inventory, HandlePartialDonation
    /// adds each one). So widening this one answer is enough for 7 white and 6 brown eggs to fill a
    /// 13-egg slot through the game's own partial donation, and cancelling it hands back each color
    /// as it went in.
    ///
    /// Only the plain item branch widens: a flavored slot (preservesId) or a category slot keeps
    /// the game's answer, and the slot's quality floor still applies. Inert off a TLY save, so a
    /// vanilla save keeps vanilla's color rule. On a TLY save it covers every board the run writes,
    /// the generated boards and the Vanilla board option alike, because it is the same slot check.
    /// </summary>
    [HarmonyPatch(typeof(Bundle), nameof(Bundle.IsValidItemForThisIngredientDescription))]
    internal static class InterchangeableSlotPatch
    {
        // ReSharper disable once InconsistentNaming - Harmony convention.
        // ReSharper disable once UnusedMember.Local - discovered by the manual PatchClassProcessor scan.
        private static void Postfix(Item item, BundleIngredientDescription ingredient, ref bool __result)
        {
            if (__result || !RunActivation.IsActive)
                return;
            if (item == null || ingredient.completed || ingredient.quality > item.Quality)
                return;
            if (ingredient.preservesId != null || ingredient.category.HasValue)
                return;
            if (InterchangeableItems.SameForDonation(ingredient.id, item.QualifiedItemId))
                __result = true;
        }
    }
}
