using System;
using System.Collections.Generic;
using HarmonyLib;
using StardewValley;
using StardewValley.Menus;
using TheLongestYear.Core;
using TheLongestYear.Core.Sabotage;

namespace TheLongestYear.Loop
{
    /// <summary>A bundle slot the darkness emptied is marked in the bundle menu (designer,
    /// 2026-10-08): its icon in the page's ingredient list sits in the tainted aura's dark purple
    /// glow until the slot is filled again. The marks live on <see cref="RunState.DarkenedSlots"/>
    /// (rules: <see cref="DarkenedSlots"/>).
    ///
    /// How it draws. The ingredient list is vanilla's own list of item icons, built when a bundle's
    /// page opens (<c>setUpBundleSpecificPage</c>) and rebuilt on a window resize
    /// (<c>gameWindowSizeChanged</c>); each icon's <c>myID</c> is 1000 plus its ingredient index, on
    /// vanilla and TLY boards alike (TLY's boards are BundleData in the vanilla menu, and the
    /// non-object icons <c>IconGatePatch</c> adds keep the same numbering). The postfixes tag a
    /// marked slot's icon item through <see cref="Item.modData"/>, and
    /// <see cref="TaintedAuraPatch"/>'s menu prefix draws the glow under any tagged Object it sees,
    /// unless that ingredient has been completed on the open page since (a donation in the menu).
    /// Only Object icons glow: a darkness reversion only ever empties a donated Object slot.</summary>
    [HarmonyPatch(typeof(JunimoNoteMenu))]
    internal static class DarkenedSlotMarkPatch
    {
        /// <summary>modData key on a marked icon's item; the value is the ingredient index.</summary>
        internal const string ModDataKey = "TheLongestYear/DarkenedSlot";
        private const int IngredientListFirstMyId = 1000;

        /// <summary>Set by ModEntry: the run's marks, or null with no run loaded.</summary>
        internal static Func<List<DonatedSlot>> Marks;

        // ReSharper disable once UnusedMember.Local - discovered by the manual PatchClassProcessor scan.
        [HarmonyPostfix]
        [HarmonyPatch("setUpBundleSpecificPage")]
        // ReSharper disable once InconsistentNaming - Harmony convention.
        private static void AfterPageSetUp(JunimoNoteMenu __instance) => Tag(__instance);

        // ReSharper disable once UnusedMember.Local - discovered by the manual PatchClassProcessor scan.
        [HarmonyPostfix]
        [HarmonyPatch("gameWindowSizeChanged")]
        // ReSharper disable once InconsistentNaming - Harmony convention.
        private static void AfterResize(JunimoNoteMenu __instance) => Tag(__instance);

        private static void Tag(JunimoNoteMenu menu)
        {
            try
            {
                if (!RunActivation.IsActive) return;
                List<DonatedSlot> marks = Marks?.Invoke();
                Bundle bundle = menu?.currentPageBundle;
                if (marks == null || marks.Count == 0 || bundle?.ingredients == null || menu.ingredientList == null) return;
                foreach (ClickableTextureComponent icon in menu.ingredientList)
                {
                    int index = icon.myID - IngredientListFirstMyId;
                    if (icon.item == null || index < 0 || index >= bundle.ingredients.Count) continue;
                    if (DarkenedSlots.Shows(marks, bundle.bundleIndex, index, bundle.ingredients[index].completed))
                        icon.item.modData[ModDataKey] = index.ToString();
                }
            }
            catch (Exception ex)
            {
                PatchLog.Warn($"Darkness: the darkened-slot mark could not tag the bundle page. {ex}");
            }
        }

        /// <summary>Does this icon item carry the mark, on a slot still empty on the open page?
        /// Called by the aura's menu prefix for every Object drawn in a menu, so it is cheap when
        /// the item has no tag.</summary>
        internal static bool ShowsOn(StardewValley.Object item)
        {
            if (!item.modData.TryGetValue(ModDataKey, out string value)) return false;
            if (Game1.activeClickableMenu is not JunimoNoteMenu menu || menu.currentPageBundle?.ingredients == null) return false;
            if (!int.TryParse(value, out int index) || index < 0 || index >= menu.currentPageBundle.ingredients.Count) return false;
            return !menu.currentPageBundle.ingredients[index].completed;
        }
    }
}
