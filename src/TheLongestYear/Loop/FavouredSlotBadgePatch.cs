using System;
using System.Collections.Generic;
using HarmonyLib;
using Microsoft.Xna.Framework;
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
    /// Vanilla draws the cursor, the held item and the tooltip at the end of
    /// <c>JunimoNoteMenu.draw</c>, so a badge drawn in the postfix would sit on top of them. When a
    /// badge is drawn, the postfix draws that tail again in vanilla's order.
    /// </summary>
    [HarmonyPatch(typeof(JunimoNoteMenu))]
    internal static class FavouredSlotBadgePatch
    {
        /// <summary>The live run (wired at save load, cleared on return to title).</summary>
        internal static Func<RunState> Run;

        /// <summary>Vanilla gives ingredient-list icon i the component id 1000 + i.</summary>
        private const int IngredientIdBase = 1000;

        /// <summary>Width the hover line wraps to in the bundle page's dialogue font.</summary>
        private const int HoverLineWidth = 640;

        /// <summary>How close the cursor must be to a badged icon before it is redrawn over the badge.</summary>
        private const int CursorReach = 64;

        private static readonly AccessTools.FieldRef<JunimoNoteMenu, bool> SingleBundleMenu =
            AccessTools.FieldRefAccess<JunimoNoteMenu, bool>("singleBundleMenu");

        // ReSharper disable once UnusedMember.Local - discovered by the manual PatchClassProcessor scan.
        [HarmonyPostfix]
        [HarmonyPatch(nameof(JunimoNoteMenu.performHoverAction))]
        // ReSharper disable once InconsistentNaming - Harmony convention.
        private static void AfterHover(JunimoNoteMenu __instance, int x, int y)
        {
            if (!Active(__instance) || __instance.scrambledText) return;
            foreach (ClickableTextureComponent icon in __instance.ingredientList)
            {
                if (!icon.bounds.Contains(x, y)) continue;
                if (Favoured(__instance, icon))
                    JunimoNoteMenu.hoverText += "\n" + Game1.parseText(
                        Strings.Get("goal.favoured-line"), Game1.dialogueFont, HoverLineWidth);
                return;
            }
        }

        // ReSharper disable once UnusedMember.Local - discovered by the manual PatchClassProcessor scan.
        [HarmonyPostfix]
        [HarmonyPatch(nameof(JunimoNoteMenu.draw), typeof(SpriteBatch))]
        // ReSharper disable once InconsistentNaming - Harmony convention.
        private static void AfterDraw(JunimoNoteMenu __instance, SpriteBatch b)
        {
            // The bundle-complete swipe draws last in vanilla; leave that moment alone.
            if (!Active(__instance) || JunimoNoteMenu.screenSwipe != null) return;
            var badged = new List<Rectangle>();
            foreach (ClickableTextureComponent icon in __instance.ingredientList)
            {
                if (icon?.item == null || !icon.visible || !Favoured(__instance, icon)) continue;
                FavouredBadge.Draw(b, icon.bounds, FavouredBadge.BundleScale);
                badged.Add(icon.bounds);
            }
            if (badged.Count > 0)
                RedrawTail(__instance, b, badged);
        }

        /// <summary>A TLY run is live and the menu is on one bundle's page.</summary>
        private static bool Active(JunimoNoteMenu menu)
            => RunActivation.IsActive && Run != null && menu?.ingredientList != null
               && menu.specificBundlePage && menu.currentPageBundle != null;

        /// <summary>The icon is a committed favoured line still waiting for its deposit.</summary>
        private static bool Favoured(JunimoNoteMenu menu, ClickableTextureComponent icon)
        {
            Bundle bundle = menu.currentPageBundle;
            int ingredient = icon.myID - IngredientIdBase;
            if (ingredient < 0 || ingredient >= bundle.ingredients.Count) return false;
            if (bundle.ingredients[ingredient].completed) return false;
            return FavouredGoal.BadgeAt(Run(), bundle.bundleIndex, ingredient);
        }

        /// <summary>Vanilla's last draws (cursor, held item, tooltip), again over the badges.</summary>
        private static void RedrawTail(JunimoNoteMenu menu, SpriteBatch b, List<Rectangle> badged)
        {
            if (JunimoNoteMenu.canClick && CursorNear(badged))
                menu.drawMouse(b);
            menu.heldItem?.drawInMenu(b, new Vector2(Game1.getOldMouseX() + 16, Game1.getOldMouseY() + 16), 1f);
            if (menu.inventory.descriptionText.Length > 0)
            {
                if (menu.hoveredItem != null)
                    IClickableMenu.drawToolTip(b, menu.hoveredItem.getDescription(), menu.hoveredItem.DisplayName, menu.hoveredItem);
            }
            else
            {
                string text = JunimoNoteMenu.hoverText;
                bool unreadable = !SingleBundleMenu(menu) && !Game1.player.hasOrWillReceiveMail("canReadJunimoText") && text.Length > 0;
                IClickableMenu.drawHoverText(b, unreadable ? "???" : text, Game1.dialogueFont);
            }
        }

        private static bool CursorNear(List<Rectangle> badged)
        {
            int mx = Game1.getMouseX(), my = Game1.getMouseY();
            foreach (Rectangle r in badged)
            {
                Rectangle reach = r;
                reach.Inflate(CursorReach, CursorReach);
                if (reach.Contains(mx, my)) return true;
            }
            return false;
        }
    }
}
