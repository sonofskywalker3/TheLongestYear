using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
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
    /// The Bundle Log as a side button on the game menu's inventory page, in the column right of the
    /// inventory grid, directly under the organize button (the bag). Clicking it (or A on it) shows the
    /// log in place of the inventory, inside the same game menu; Escape or B, or a click on the
    /// inventory tab, puts the inventory back, and the menu's close button still closes everything.
    /// The log is the same <see cref="SeasonGoalsMenu"/> the Bundle Log book opens. Only added while a
    /// run is active; the game menu's tab row is left as vanilla built it.
    ///
    /// The log is shown by swapping it into the inventory tab's slot in <c>GameMenu.pages</c>, so the
    /// game menu draws it and sends it input like any page; the inventory page is kept aside and put
    /// back before any tab change.
    /// </summary>
    internal static class BundleLogButton
    {
        // The Bundle Log's cover in assets/books.png (third 16x16 cover, the green one).
        private static readonly Rectangle BookCoverSource = new Rectangle(32, 0, 16, 16);
        private const float ButtonScale = 4f;

        private static readonly ConditionalWeakTable<InventoryPage, ClickableTextureComponent> Buttons = new();
        // Inventory pages put aside while their game menu shows the log.
        private static readonly ConditionalWeakTable<GameMenu, InventoryPage> HiddenInventories = new();

        private static System.Func<MenuLauncher> _launcher;

        internal static IMonitor Monitor { get; set; }

        /// <summary>Lazy launcher accessor (the launcher is built at save load).</summary>
        public static void AttachLauncher(System.Func<MenuLauncher> launcher) => _launcher = launcher;

        /// <summary>The Bundle Log button on <paramref name="page"/>, or null.</summary>
        internal static ClickableTextureComponent ButtonOn(InventoryPage page)
            => page != null && Buttons.TryGetValue(page, out ClickableTextureComponent button) ? button : null;

        /// <summary>The inventory page of <paramref name="menu"/>, whether shown or put aside.</summary>
        internal static InventoryPage InventoryOf(GameMenu menu)
        {
            if (menu == null) return null;
            if (HiddenInventories.TryGetValue(menu, out InventoryPage hidden)) return hidden;
            return menu.pages?.Count > GameMenu.inventoryTab ? menu.pages[GameMenu.inventoryTab] as InventoryPage : null;
        }

        /// <summary>True while <paramref name="menu"/> shows the log in place of the inventory.</summary>
        internal static bool IsLogOpen(GameMenu menu)
            => menu != null && HiddenInventories.TryGetValue(menu, out _);

        /// <summary>Debug and screenshot hook: a game menu open on the inventory page, showing the log
        /// when <paramref name="showLog"/>. Null when the button is not there (no run).</summary>
        internal static GameMenu OpenForDebug(bool showLog)
        {
            var menu = new GameMenu(playOpeningSound: false);
            Game1.activeClickableMenu = menu;
            if (ButtonOn(InventoryOf(menu)) == null) return null;
            if (showLog) OpenLog(menu, playSound: false);
            return menu;
        }

        private static void AddTo(GameMenu menu)
        {
            InventoryPage page = InventoryOf(menu);
            if (page == null) return;
            if (!BundleLogButtonRules.ShouldAdd(RunActivation.IsActive && Context.IsWorldReady, ButtonOn(page) != null)) return;
            ClickableTextureComponent bag = page.organizeButton;
            if (bag == null)
            {
                Monitor?.Log("Bundle Log button skipped: the inventory page has no organize button to sit under.", LogLevel.Trace);
                return;
            }

            int columnX = bag.bounds.X;
            List<ClickableComponent> column = ColumnComponents(page, columnX).Where(c => c != bag).ToList();
            int y = BundleLogButtonRules.ButtonY(bag.bounds.Bottom,
                column.Select(c => (c.bounds.Top, c.bounds.Bottom)).ToList());
            // Whatever ends right above the button: the bag, unless another button already sat under it.
            ClickableComponent above = column.Where(c => c.bounds.Bottom <= y && c.bounds.Top >= bag.bounds.Top)
                .OrderByDescending(c => c.bounds.Bottom).FirstOrDefault() ?? bag;

            var rightColumn = new List<(int, int)>();
            List<ClickableComponent> slots = page.inventory?.inventory;
            if (slots != null && slots.Count > 0)
            {
                int rightX = slots.Max(s => s.bounds.X);
                rightColumn.AddRange(slots.Where(s => s.bounds.X == rightX).Select(s => (s.myID, s.bounds.Center.Y)));
            }

            Texture2D books = Game1.content.Load<Texture2D>(BookFurniture.BookTextureAsset);
            var button = new ClickableTextureComponent(BundleLogButtonRules.ButtonName,
                new Rectangle(columnX, y, BundleLogButtonRules.ButtonSize, BundleLogButtonRules.ButtonSize),
                "", Strings.Get("menu.goals.tab-label"), books, BookCoverSource, ButtonScale)
            {
                myID = BundleLogButtonRules.ButtonComponentId,
                upNeighborID = above.myID,
                downNeighborID = page.trashCan?.myID ?? -1,
                leftNeighborID = BundleLogButtonRules.NearestSlot(rightColumn, y + BundleLogButtonRules.ButtonSize / 2),
            };
            above.downNeighborID = button.myID;
            if (page.trashCan != null) page.trashCan.upNeighborID = button.myID;
            Buttons.AddOrUpdate(page, button);
            // The constructor already built the page's component list; add the button to it too.
            if (page.allClickableComponents != null && !page.allClickableComponents.Contains(button))
                page.allClickableComponents.Add(button);
        }

        /// <summary>The side buttons in the column at <paramref name="columnX"/>: the page's own and any
        /// another mod added to its component list.</summary>
        private static IEnumerable<ClickableComponent> ColumnComponents(InventoryPage page, int columnX)
        {
            var all = new List<ClickableComponent>();
            if (page.allClickableComponents != null) all.AddRange(page.allClickableComponents);
            all.Add(page.organizeButton);
            all.Add(page.junimoNoteIcon);
            all.Add(page.trashCan);
            int right = columnX + BundleLogButtonRules.ButtonSize;
            return all.Where(c => c != null && c.bounds.X < right && c.bounds.Right > columnX).Distinct();
        }

        /// <summary>Shows the log in place of the inventory page.</summary>
        internal static bool OpenLog(GameMenu menu, bool playSound = true)
        {
            if (menu == null || IsLogOpen(menu) || menu.currentTab != GameMenu.inventoryTab) return false;
            if (menu.pages[GameMenu.inventoryTab] is not InventoryPage inventory) return false;
            SeasonGoalsMenu log = _launcher?.Invoke()?.CreateBundleLogPage(
                new Rectangle(menu.xPositionOnScreen, menu.yPositionOnScreen, menu.width, menu.height));
            if (log == null) return false;
            log.Refresh();

            inventory.hoverText = "";
            HiddenInventories.AddOrUpdate(menu, inventory);
            menu.pages[GameMenu.inventoryTab] = log;
            menu.hoverText = "";
            log.populateClickableComponentList();
            // The inventory tab's down neighbours point at inventory slots; on the log, take its default.
            foreach (ClickableComponent tab in menu.tabs)
                if (tab != null) tab.downNeighborID = ClickableComponent.SNAP_TO_DEFAULT;
            if (playSound) Game1.playSound("smallSelect");
            if (Game1.options.SnappyMenus) log.snapToDefaultClickableComponent();
            return true;
        }

        /// <summary>Puts the inventory page back. With <paramref name="stayOnInventory"/> the menu stays on
        /// the inventory and the cursor goes back to the button; without it a tab change follows and
        /// sets the page up itself.</summary>
        internal static bool CloseLog(GameMenu menu, bool stayOnInventory)
        {
            if (menu == null || !HiddenInventories.TryGetValue(menu, out InventoryPage inventory)) return false;
            HiddenInventories.Remove(menu);
            menu.pages[GameMenu.inventoryTab] = inventory;
            menu.hoverText = "";
            if (!stayOnInventory || menu.currentTab != GameMenu.inventoryTab) return true;

            inventory.populateClickableComponentList();
            menu.AddTabsToClickableComponents(inventory);
            menu.setTabNeighborsForCurrentPage();
            if (Game1.options.SnappyMenus && ButtonOn(inventory) is { } button)
            {
                inventory.currentlySnappedComponent = button;
                inventory.snapCursorToCurrentSnappedComponent();
            }
            return true;
        }

        private static bool OpensLog(InventoryPage page, int x, int y)
            => ButtonOn(page) is { } button && button.containsPoint(x, y) && Game1.player.CursorSlotItem == null
               && Game1.activeClickableMenu is GameMenu menu && menu.GetCurrentPage() == page;

        // ReSharper disable InconsistentNaming, UnusedMember.Local: Harmony.

        /// <summary>Adds the button when the menu is built. Every other constructor chains through this
        /// one.</summary>
        [HarmonyPatch(typeof(GameMenu), MethodType.Constructor, new[] { typeof(bool) })]
        internal static class ConstructorPatch
        {
            private static void Postfix(GameMenu __instance)
            {
                try { AddTo(__instance); }
                catch (System.Exception ex)
                {
                    Monitor?.Log($"Bundle Log button could not be added: {ex.GetType().Name}: {ex.Message}", LogLevel.Warn);
                }
            }
        }

        /// <summary>Keeps the button in the inventory page's controller list, which the game menu rebuilds
        /// on every tab change (the base version only finds the page's own fields).</summary>
        [HarmonyPatch(typeof(IClickableMenu), nameof(IClickableMenu.populateClickableComponentList))]
        internal static class ComponentListPatch
        {
            private static void Postfix(IClickableMenu __instance)
            {
                if (__instance is InventoryPage page && ButtonOn(page) is { } button
                    && page.allClickableComponents != null && !page.allClickableComponents.Contains(button))
                    page.allClickableComponents.Add(button);
            }
        }

        [HarmonyPatch(typeof(InventoryPage), nameof(InventoryPage.draw), new[] { typeof(SpriteBatch) })]
        internal static class DrawPatch
        {
            private static void Postfix(InventoryPage __instance, SpriteBatch __0)
                => ButtonOn(__instance)?.draw(__0);
        }

        [HarmonyPatch(typeof(InventoryPage), nameof(InventoryPage.performHoverAction))]
        internal static class HoverPatch
        {
            private static void Postfix(InventoryPage __instance, int __0, int __1)
            {
                if (ButtonOn(__instance) is not { } button) return;
                button.tryHover(__0, __1);
                if (!button.containsPoint(__0, __1)) return;
                __instance.hoverText = button.hoverText;
                __instance.hoverTitle = "";
                __instance.hoveredItem = null;
            }
        }

        [HarmonyPatch(typeof(InventoryPage), nameof(InventoryPage.receiveLeftClick))]
        internal static class ClickPatch
        {
            private static bool Prefix(InventoryPage __instance, int __0, int __1)
            {
                if (!OpensLog(__instance, __0, __1)) return true;
                return !OpenLog((GameMenu)Game1.activeClickableMenu);
            }
        }

        /// <summary>Any tab change puts the inventory back first, so the inventory tab never reopens on the log.</summary>
        [HarmonyPatch(typeof(GameMenu), nameof(GameMenu.changeTab))]
        internal static class ChangeTabPatch
        {
            private static void Prefix(GameMenu __instance) => CloseLog(__instance, stayOnInventory: false);
        }

        /// <summary>Escape (and B, which the game sends as the menu key) goes back to the inventory
        /// instead of closing the menu.</summary>
        [HarmonyPatch(typeof(GameMenu), nameof(GameMenu.receiveKeyPress))]
        internal static class BackKeyPatch
        {
            private static bool Prefix(GameMenu __instance, Keys __0)
            {
                if (!IsLogOpen(__instance) || !Game1.options.doesInputListContain(Game1.options.menuButton, __0)) return true;
                CloseLog(__instance, stayOnInventory: true);
                Game1.playSound("bigDeSelect");
                return false;
            }
        }

        /// <summary>The inventory tab is the open tab, so vanilla ignores a click on it; while the log is
        /// shown that click goes back to the inventory.</summary>
        [HarmonyPatch(typeof(GameMenu), nameof(GameMenu.receiveLeftClick))]
        internal static class InventoryTabClickPatch
        {
            private static bool Prefix(GameMenu __instance, int __0, int __1)
            {
                if (!IsLogOpen(__instance) || __instance.tabs.Count <= GameMenu.inventoryTab
                    || !__instance.tabs[GameMenu.inventoryTab].containsPoint(__0, __1)) return true;
                CloseLog(__instance, stayOnInventory: true);
                Game1.playSound("smallSelect");
                return false;
            }
        }

        // ReSharper restore InconsistentNaming, UnusedMember.Local
    }
}
