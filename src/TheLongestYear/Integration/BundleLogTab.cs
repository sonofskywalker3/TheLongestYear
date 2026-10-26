using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;
using TheLongestYear.Core;
using TheLongestYear.Core.Interactables;
using TheLongestYear.UI;

namespace TheLongestYear.Integration
{
    /// <summary>
    /// The Bundle Log as an extra tab in the game menu (Inventory, Skills, Social, Map ...), after the
    /// last tab, so the season's missing items can be checked anywhere. The page is the same
    /// <see cref="SeasonGoalsMenu"/> the Bundle Log book opens. Only added while a run is active.
    ///
    /// The game menu knows its tabs by name: <c>getTabNumberFromName</c> answers -1 for a name it
    /// does not know (a click on the tab would then index tabs[-1]), so the postfix there maps our
    /// name to its place in the list. Its draw switch has no case for our name, so a transpiler
    /// draws the tab icon at the point the vanilla tabs are done and the page is about to draw.
    /// </summary>
    internal static class BundleLogTab
    {
        // The empty tab in Cursors: the skills tab's frame, which vanilla fills with the farmer's portrait.
        private static readonly Rectangle EmptyTabSource = new Rectangle(16, 368, 16, 16);
        // The Bundle Log's cover in assets/books.png (third 16x16 cover).
        private static readonly Rectangle BookCoverSource = new Rectangle(32, 0, 16, 16);
        private const float TabScale = 4f;
        private const float BookScale = 3f;
        private const int BookOffsetX = 8;
        private const int BookOffsetY = 12;
        // Vanilla drops the open tab by this much.
        private const int SelectedTabDrop = 8;

        private static System.Func<MenuLauncher> _launcher;

        internal static IMonitor Monitor { get; set; }

        /// <summary>Lazy launcher accessor (the launcher is built at save load).</summary>
        public static void AttachLauncher(System.Func<MenuLauncher> launcher) => _launcher = launcher;

        /// <summary>Index of the Bundle Log tab in <paramref name="menu"/>, or -1.</summary>
        internal static int IndexIn(GameMenu menu)
            => menu?.tabs == null ? -1 : menu.tabs.FindIndex(t => t?.name == BundleLogTabRules.TabName);

        /// <summary>Debug and screenshot hook: a game menu open on the Bundle Log tab, or null when the
        /// tab is not there (no run).</summary>
        internal static GameMenu OpenOnTab(bool onLogTab)
        {
            var menu = new GameMenu(playOpeningSound: false);
            Game1.activeClickableMenu = menu;
            int index = IndexIn(menu);
            if (index < 0) return null;
            if (onLogTab) menu.changeTab(index, playSound: false);
            return menu;
        }

        private static void AddTo(GameMenu menu)
        {
            if (!BundleLogTabRules.ShouldAdd(RunActivation.IsActive && Context.IsWorldReady, IndexIn(menu) >= 0)) return;
            // Tabs and pages are read by the same index; a menu where they already disagree is left alone.
            if (menu.tabs.Count != menu.pages.Count)
            {
                Monitor?.Log($"Bundle Log tab skipped: the game menu has {menu.tabs.Count} tabs but {menu.pages.Count} pages.", LogLevel.Trace);
                return;
            }
            MenuLauncher launcher = _launcher?.Invoke();
            SeasonGoalsMenu page = launcher?.CreateBundleLogPage(
                new Rectangle(menu.xPositionOnScreen, menu.yPositionOnScreen, menu.width, menu.height));
            if (page == null) return;

            ClickableComponent last = menu.tabs.Where(t => t != null).OrderByDescending(t => t.bounds.X).FirstOrDefault();
            int x = BundleLogTabRules.NextTabX(menu.tabs.Where(t => t != null).Select(t => t.bounds.X).ToList(),
                menu.xPositionOnScreen + BundleLogTabRules.TabSize);
            int y = last?.bounds.Y ?? menu.yPositionOnScreen + IClickableMenu.tabYPositionRelativeToMenuY + BundleLogTabRules.TabSize;
            var tab = new ClickableComponent(
                new Rectangle(x, y, BundleLogTabRules.TabSize, BundleLogTabRules.TabSize),
                BundleLogTabRules.TabName, Strings.Get("menu.goals.tab-label"))
            {
                myID = BundleLogTabRules.TabComponentId,
                leftNeighborID = last?.myID ?? -1,
                downNeighborID = ClickableComponent.SNAP_TO_DEFAULT,
                tryDefaultIfNoDownNeighborExists = true,
                fullyImmutable = true,
            };
            if (last != null) last.rightNeighborID = BundleLogTabRules.TabComponentId;
            menu.tabs.Add(tab);
            menu.pages.Add(page);
            // The constructor already gave the open page the tab list; add ours to it too.
            menu.GetCurrentPage()?.allClickableComponents?.Add(tab);
        }

        internal static void DrawTab(GameMenu menu, SpriteBatch b)
        {
            int index = IndexIn(menu);
            if (index < 0) return;
            ClickableComponent tab = menu.tabs[index];
            int drop = menu.currentTab == index ? SelectedTabDrop : 0;
            b.Draw(Game1.mouseCursors, new Vector2(tab.bounds.X, tab.bounds.Y + drop), EmptyTabSource,
                Color.White, 0f, Vector2.Zero, TabScale, SpriteEffects.None, 0.0001f);
            Texture2D books = Game1.content.Load<Texture2D>(BookFurniture.BookTextureAsset);
            b.Draw(books, new Vector2(tab.bounds.X + BookOffsetX, tab.bounds.Y + BookOffsetY + drop), BookCoverSource,
                Color.White, 0f, Vector2.Zero, BookScale, SpriteEffects.None, 0.00011f);
        }

        // ReSharper disable InconsistentNaming, UnusedMember.Local: Harmony.

        /// <summary>Adds the tab when the menu is built. Every other constructor chains through this
        /// one, so the tab exists before <c>new GameMenu(tab)</c> switches to it (the game rebuilds
        /// the menu on the open tab when the window is resized).</summary>
        [HarmonyPatch(typeof(GameMenu), MethodType.Constructor, new[] { typeof(bool) })]
        internal static class ConstructorPatch
        {
            private static void Postfix(GameMenu __instance)
            {
                try { AddTo(__instance); }
                catch (System.Exception ex)
                {
                    Monitor?.Log($"Bundle Log tab could not be added: {ex.GetType().Name}: {ex.Message}", LogLevel.Warn);
                }
            }
        }

        [HarmonyPatch(typeof(GameMenu), nameof(GameMenu.getTabNumberFromName))]
        internal static class TabNumberPatch
        {
            private static void Postfix(GameMenu __instance, string name, ref int __result)
            {
                if (__result == -1 && name == BundleLogTabRules.TabName)
                    __result = IndexIn(__instance);
            }
        }

        /// <summary>Refreshes the log each time its tab is shown.</summary>
        [HarmonyPatch(typeof(GameMenu), nameof(GameMenu.changeTab))]
        internal static class ChangeTabPatch
        {
            private static void Postfix(GameMenu __instance)
            {
                int index = IndexIn(__instance);
                if (index >= 0 && __instance.currentTab == index && __instance.pages[index] is SeasonGoalsMenu page)
                    page.Refresh();
            }
        }

        /// <summary>The right trigger stops at vanilla's last tab; one more press reaches the log.</summary>
        [HarmonyPatch(typeof(GameMenu), nameof(GameMenu.receiveGamePadButton))]
        internal static class RightTriggerPatch
        {
            private static bool Prefix(GameMenu __instance, Buttons b)
            {
                if (b != Buttons.RightTrigger) return true;
                int? target = BundleLogTabRules.RightTriggerTarget(__instance.currentTab, IndexIn(__instance), GameMenu.numberOfTabs);
                if (target == null || !__instance.pages[__instance.currentTab].readyToClose()) return true;
                __instance.changeTab(target.Value);
                return false;
            }
        }

        /// <summary>Draws the tab right after the vanilla tabs: after the SpriteBatch.Begin that
        /// precedes the first page draw call in <c>GameMenu.draw</c>.</summary>
        [HarmonyPatch(typeof(GameMenu), nameof(GameMenu.draw), new[] { typeof(SpriteBatch) })]
        internal static class DrawPatch
        {
            private static readonly MethodInfo DrawTabMethod = AccessTools.Method(typeof(BundleLogTab), nameof(DrawTab));

            private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                var codes = new List<CodeInstruction>(instructions);
                int pageDraw = codes.FindIndex(c => c.opcode == OpCodes.Callvirt && c.operand is MethodInfo m
                    && m.Name == nameof(IClickableMenu.draw) && typeof(IClickableMenu).IsAssignableFrom(m.DeclaringType)
                    && m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType == typeof(SpriteBatch));
                int begin = pageDraw < 0 ? -1 : codes.FindLastIndex(pageDraw, c => (c.opcode == OpCodes.Callvirt || c.opcode == OpCodes.Call)
                    && c.operand is MethodInfo m && m.Name == nameof(SpriteBatch.Begin) && m.DeclaringType == typeof(SpriteBatch));
                if (begin < 0)
                {
                    Monitor?.Log("Bundle Log tab: could not find where GameMenu.draw draws its tabs; the tab works but shows no icon.", LogLevel.Warn);
                    return codes;
                }
                codes.InsertRange(begin + 1, new[]
                {
                    new CodeInstruction(OpCodes.Ldarg_0),
                    new CodeInstruction(OpCodes.Ldarg_1),
                    new CodeInstruction(OpCodes.Call, DrawTabMethod),
                });
                return codes;
            }
        }

        // ReSharper restore InconsistentNaming, UnusedMember.Local
    }
}
