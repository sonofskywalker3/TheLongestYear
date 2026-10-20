using System;
using System.Collections.Generic;
using HarmonyLib;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.Menus;
using TheLongestYear.Core;
using TheLongestYear.UI;

namespace TheLongestYear.Loop
{
    /// <summary>
    /// The favoured goal on the bundle page (spec 2026-10-19-focus-cards, "The favoured goal (x2)"):
    /// an x2 badge on the ingredient this week's card pays double for, and the Junimos' line under
    /// its hover name. Reads the STORED goal lists (<see cref="FavouredGoal.BadgeAt"/>), never a
    /// fresh sample, and the badge goes once that slot is deposited.
    ///
    /// The hover also says when the theme week discount lowered the line ("Asks 13 (27) until
    /// the week ends", <see cref="LoweredAsk"/>), so the lowered stack reads as temporary.
    ///
    /// The badge is drawn from a postfix on the bundle page's own <c>inventory.draw</c>. Vanilla
    /// calls that after the ingredient icons and before the cursor, held item and tooltip, so the
    /// badge sits over its icon and under everything drawn last, with nothing drawn twice.
    /// </summary>
    [HarmonyPatch]
    internal static class FavouredSlotBadgePatch
    {
        /// <summary>The live run (wired at save load, cleared on return to title).</summary>
        internal static Func<RunState> Run;

        /// <summary>Vanilla gives ingredient-list icon i the component id 1000 + i.</summary>
        private const int IngredientIdBase = 1000;

        /// <summary>Width the hover line wraps to in the bundle page's dialogue font.</summary>
        private const int HoverLineWidth = 640;

        /// <summary>Vanilla's alpha for the other ingredients while one takes a partial donation.</summary>
        private const float PartialDonationDim = 0.25f;

        private const int NoPartialIngredient = -1;

        // ReSharper disable once UnusedMember.Local - discovered by the manual PatchClassProcessor scan.
        [HarmonyPostfix]
        [HarmonyPatch(typeof(JunimoNoteMenu), nameof(JunimoNoteMenu.performHoverAction))]
        // ReSharper disable once InconsistentNaming - Harmony convention.
        private static void AfterHover(JunimoNoteMenu __instance, int x, int y)
        {
            if (!Active(__instance) || __instance.scrambledText) return;
            foreach (ClickableTextureComponent icon in __instance.ingredientList)
            {
                if (!icon.bounds.Contains(x, y)) continue;
                if (LoweredFullAsk(__instance, icon) is int full)
                    JunimoNoteMenu.hoverText += "\n" + Game1.parseText(
                        Strings.Get("goal.lowered-line", new Dictionary<string, string>
                        {
                            ["now"] = __instance.currentPageBundle.ingredients[icon.myID - IngredientIdBase].stack.ToString(),
                            ["full"] = full.ToString(),
                        }), Game1.dialogueFont, HoverLineWidth);
                if (Favoured(__instance, icon))
                    JunimoNoteMenu.hoverText += "\n" + Game1.parseText(
                        Strings.Get("goal.favoured-line"), Game1.dialogueFont, HoverLineWidth);
                return;
            }
        }

        // ReSharper disable once UnusedMember.Local - discovered by the manual PatchClassProcessor scan.
        [HarmonyPostfix]
        [HarmonyPatch(typeof(InventoryMenu), nameof(InventoryMenu.draw), typeof(SpriteBatch))]
        // ReSharper disable once InconsistentNaming - Harmony convention.
        private static void AfterInventoryDraw(InventoryMenu __instance, SpriteBatch b)
        {
            if (Game1.activeClickableMenu is not JunimoNoteMenu menu || menu.inventory != __instance) return;
            if (!Active(menu)) return;
            foreach (ClickableTextureComponent icon in menu.ingredientList)
            {
                if (icon?.item == null || !icon.visible || !Favoured(menu, icon)) continue;
                int partial = menu.currentPartialIngredientDescriptionIndex;
                float alpha = partial != NoPartialIngredient && partial != icon.myID - IngredientIdBase
                    ? PartialDonationDim
                    : 1f;
                FavouredBadge.Draw(b, icon.bounds, FavouredBadge.BundleScale, alpha);
            }
        }

        /// <summary>A TLY run is live and the menu is on one bundle's page.</summary>
        private static bool Active(JunimoNoteMenu menu)
            => RunActivation.IsActive && Run != null && menu?.ingredientList != null
               && menu.specificBundlePage && menu.currentPageBundle != null;

        /// <summary>The full ask of an open line this week's discount lowered, or null.</summary>
        private static int? LoweredFullAsk(JunimoNoteMenu menu, ClickableTextureComponent icon)
        {
            Bundle bundle = menu.currentPageBundle;
            int ingredient = icon.myID - IngredientIdBase;
            if (ingredient < 0 || ingredient >= bundle.ingredients.Count) return null;
            BundleIngredientDescription line = bundle.ingredients[ingredient];
            if (line.completed) return null;
            return LoweredAsk.AtLine(LoweredAsk.WeekSlots(Run()), bundle.bundleIndex, ingredient, line.stack);
        }

        /// <summary>The icon is a committed favoured line still waiting for its deposit.</summary>
        private static bool Favoured(JunimoNoteMenu menu, ClickableTextureComponent icon)
        {
            Bundle bundle = menu.currentPageBundle;
            int ingredient = icon.myID - IngredientIdBase;
            if (ingredient < 0 || ingredient >= bundle.ingredients.Count) return false;
            if (bundle.ingredients[ingredient].completed) return false;
            return FavouredGoal.BadgeAt(Run(), bundle.bundleIndex, ingredient);
        }
    }
}
