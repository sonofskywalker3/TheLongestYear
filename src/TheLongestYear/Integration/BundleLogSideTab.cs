using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;
using TheLongestYear.Core;
using TheLongestYear.Core.Interactables;
using TheLongestYear.UI;

namespace TheLongestYear.Integration
{
    /// <summary>
    /// The Bundle Log as one more side tab on the game menu's Collections page, at the bottom of the
    /// column of side tabs down its left edge (Items Shipped, Fish, Artifacts, Minerals, Cooking,
    /// Achievements, Letters, Secret Notes). Selecting it shows the log in place of the collection grid;
    /// the vanilla side tabs stay where they are, so a click on any of them goes back to that
    /// collection. Only added while a run is active.
    ///
    /// The tab is a real entry in the page's <c>sideTabs</c> (with an empty collection beside it), so
    /// vanilla selects it, shifts it out when selected and shows its hover label like its own tabs;
    /// this class only shows the log while it is the current tab and passes it the page's input.
    /// When all nine tabs show and would not fit the box, the column closes up (see FitColumn).
    /// </summary>
    internal static class BundleLogSideTab
    {
        // The Bundle Log's cover in assets/books.png (third 16x16 cover, the green one).
        private static readonly Rectangle BookCoverSource = new Rectangle(32, 0, 16, 16);
        // Vanilla's side-tab sprites in Cursors (see CollectionsPage's constructor). They share one
        // frame, so comparing them finds the frame without any one tab's picture.
        private static readonly Rectangle[] VanillaTabSources =
        {
            new Rectangle(640, 80, 16, 16), new Rectangle(640, 64, 16, 16), new Rectangle(656, 64, 16, 16),
            new Rectangle(672, 64, 16, 16), new Rectangle(688, 64, 16, 16), new Rectangle(656, 80, 16, 16),
            new Rectangle(688, 80, 16, 16), new Rectangle(672, 80, 16, 16),
        };
        // First tab's slot when the column is empty (vanilla's formula for tab 0, as not selected).
        private const int FirstSlotTiles = 2;

        private sealed class State
        {
            public ClickableTextureComponent Tab;
            public SeasonGoalsMenu Log;
        }

        private static readonly ConditionalWeakTable<CollectionsPage, State> States = new();
        private static Texture2D _icon;
        private static System.Func<MenuLauncher> _launcher;

        internal static IMonitor Monitor { get; set; }

        /// <summary>Lazy launcher accessor (the launcher is built at save load).</summary>
        public static void AttachLauncher(System.Func<MenuLauncher> launcher) => _launcher = launcher;

        /// <summary>The Bundle Log side tab on <paramref name="page"/>, or null.</summary>
        internal static ClickableTextureComponent TabOn(CollectionsPage page)
            => page != null && States.TryGetValue(page, out State s) ? s.Tab : null;

        /// <summary>The log <paramref name="page"/> shows, or null while another collection is shown.</summary>
        internal static SeasonGoalsMenu LogOn(CollectionsPage page)
            => page != null && States.TryGetValue(page, out State s) ? s.Log : null;

        /// <summary>The Collections page of <paramref name="menu"/>, or null.</summary>
        internal static CollectionsPage CollectionsOf(GameMenu menu)
            => menu?.pages?.OfType<CollectionsPage>().FirstOrDefault();

        private static bool IsSelected(CollectionsPage page) => page.currentTab == BundleLogSideTabRules.TabKey;

        private static void AddTo(CollectionsPage page)
        {
            if (!BundleLogSideTabRules.ShouldAdd(RunActivation.IsActive && Context.IsWorldReady, TabOn(page) != null)) return;
            if (page.sideTabs.ContainsKey(BundleLogSideTabRules.TabKey)) return;

            List<ClickableTextureComponent> column = page.sideTabs.Values.ToList();
            int y = BundleLogSideTabRules.TabY(column.Select(t => t.bounds.Y).ToList(),
                page.yPositionOnScreen + BundleLogSideTabRules.TabSize * FirstSlotTiles);
            // The tab above in the column: vanilla ends its column there and points nowhere further down.
            ClickableTextureComponent above = column.OrderByDescending(t => t.bounds.Y).FirstOrDefault();
            Texture2D icon = Icon();
            var tab = new ClickableTextureComponent(BundleLogSideTabRules.TabName,
                new Rectangle(page.xPositionOnScreen - 48, y, BundleLogSideTabRules.TabSize, BundleLogSideTabRules.TabSize),
                "", Strings.Get("menu.goals.tab-label"), icon, new Rectangle(0, 0, icon.Width, icon.Height),
                (float)BundleLogSideTabRules.TabSize / icon.Width)
            {
                myID = BundleLogSideTabRules.TabComponentId,
                upNeighborID = above?.myID ?? -1,
                downNeighborID = -1,
                downNeighborImmutable = true,
                rightNeighborID = 0,
            };
            if (above != null) above.downNeighborID = tab.myID;

            page.sideTabs.Add(BundleLogSideTabRules.TabKey, tab);
            // Vanilla reads the current tab's collection on every draw, hover and click: an empty one.
            page.collections[BundleLogSideTabRules.TabKey] = new List<List<ClickableTextureComponent>> { new() };
            States.AddOrUpdate(page, new State { Tab = tab });
            page.allClickableComponents?.Add(tab);
            FitColumn(page);
        }

        /// <summary>With every vanilla tab showing (Secret Notes too), one more tab would hang below the
        /// menu box; the whole stack then moves up, keeping vanilla's spacing, until its last tab ends on
        /// the box's bottom edge (closing up only if it would rise past the box's top). Only the tops
        /// move: sizes, the selected tab's shift and the hover label stay vanilla's. With room to spare
        /// the column stays where vanilla put it.</summary>
        private static void FitColumn(CollectionsPage page)
        {
            List<ClickableTextureComponent> column = page.sideTabs.Values.OrderBy(t => t.bounds.Y).ToList();
            if (column.Count == 0) return;
            int bottom = System.Math.Min(page.yPositionOnScreen + page.height - BundleLogSideTabRules.BoxBottomInset,
                Game1.uiViewport.Height);
            int top = page.yPositionOnScreen + BundleLogSideTabRules.BoxTopInset;
            int[] tops = BundleLogSideTabRules.ColumnTops(column.Count, column[0].bounds.Y, bottom, top);
            for (int i = 0; i < column.Count; i++) column[i].bounds.Y = tops[i];
            int end = tops[^1] + BundleLogSideTabRules.TabSize;
            if (end > bottom)
                Monitor?.Log($"Bundle Log side tab: {column.Count} tabs still end at {end}, past the menu box or screen ({bottom}).", LogLevel.Trace);
        }

        /// <summary>The tab's picture: a vanilla side-tab frame with the Bundle Log's cover on it. Built
        /// once from the loaded sprites; falls back to the bare cover if they cannot be read.</summary>
        private static Texture2D Icon()
        {
            if (_icon != null && !_icon.IsDisposed) return _icon;
            Texture2D books = Game1.content.Load<Texture2D>(BookFurniture.BookTextureAsset);
            uint[] cover = Read(books, BookCoverSource);
            uint[] frame = BundleLogSideTabRules.BlankFrame(VanillaTabSources.Select(r => Read(Game1.mouseCursors, r)).ToList());
            if (frame == null)
            {
                Monitor?.Log("Bundle Log side tab: the vanilla tab frame could not be read; the tab shows the bare cover.", LogLevel.Trace);
                frame = new uint[BundleLogSideTabRules.SpriteSize * BundleLogSideTabRules.SpriteSize];
            }
            uint[] pixels = BundleLogSideTabRules.Compose(frame, cover);
            int size = BundleLogSideTabRules.SpriteSize * BundleLogSideTabRules.IconScale;
            var colors = pixels.Select(p => new Color(p)).ToArray();
            _icon = new Texture2D(Game1.graphics.GraphicsDevice, size, size);
            _icon.SetData(colors);
            return _icon;
        }

        private static uint[] Read(Texture2D texture, Rectangle source)
        {
            if (texture == null || source.Right > texture.Width || source.Bottom > texture.Height) return null;
            var data = new Color[source.Width * source.Height];
            texture.GetData(0, source, data, 0, data.Length);
            return data.Select(c => c.PackedValue).ToArray();
        }

        /// <summary>Builds the log for <paramref name="page"/> and hooks it into the page's controller
        /// list: the tabs lead right into its rows, its rows lead left back to the tab.</summary>
        private static void ShowLog(CollectionsPage page)
        {
            if (!States.TryGetValue(page, out State state)) return;
            SeasonGoalsMenu log = _launcher?.Invoke()?.CreateBundleLogPage(
                new Rectangle(page.xPositionOnScreen, page.yPositionOnScreen, page.width, page.height));
            if (log == null) return;
            log.Refresh();
            state.Log = log;
            foreach (ClickableTextureComponent tab in page.sideTabs.Values)
                tab.rightNeighborID = SeasonGoalsMenu.FirstRowId;
            page.populateClickableComponentList();
        }

        private static void HideLog(CollectionsPage page)
        {
            if (!States.TryGetValue(page, out State state) || state.Log == null) return;
            state.Log = null;
            foreach (ClickableTextureComponent tab in page.sideTabs.Values)
                tab.rightNeighborID = 0;
            page.populateClickableComponentList();
        }

        // ReSharper disable InconsistentNaming, UnusedMember.Local: Harmony.

        [HarmonyPatch(typeof(CollectionsPage), MethodType.Constructor, new[] { typeof(int), typeof(int), typeof(int), typeof(int) })]
        internal static class ConstructorPatch
        {
            private static void Postfix(CollectionsPage __instance)
            {
                try { AddTo(__instance); }
                catch (System.Exception ex)
                {
                    Monitor?.Log($"Bundle Log side tab could not be added: {ex.GetType().Name}: {ex.Message}", LogLevel.Warn);
                }
            }
        }

        /// <summary>Adds the log's rows and scroll arrows to the page's controller list while it shows
        /// (the side tab itself is found in <c>sideTabs</c> by the base version).</summary>
        [HarmonyPatch(typeof(IClickableMenu), nameof(IClickableMenu.populateClickableComponentList))]
        internal static class ComponentListPatch
        {
            private static void Postfix(IClickableMenu __instance)
            {
                if (__instance is not CollectionsPage page || LogOn(page) is not { } log || page.allClickableComponents == null) return;
                if (log.allClickableComponents == null) log.populateClickableComponentList();
                foreach (ClickableComponent c in log.allClickableComponents)
                    if (!page.allClickableComponents.Contains(c)) page.allClickableComponents.Add(c);
            }
        }

        /// <summary>Vanilla switches tabs on the click; this shows or drops the log to match, and passes
        /// any other click on the log (its scroll arrows) to it.</summary>
        [HarmonyPatch(typeof(CollectionsPage), nameof(CollectionsPage.receiveLeftClick))]
        internal static class ClickPatch
        {
            private static void Prefix(CollectionsPage __instance, out int __state) => __state = __instance.currentTab;

            private static void Postfix(CollectionsPage __instance, int __0, int __1, int __state)
            {
                if (TabOn(__instance) == null || __instance.letterviewerSubMenu != null) return;
                bool was = __state == BundleLogSideTabRules.TabKey, now = IsSelected(__instance);
                if (now && !was) ShowLog(__instance);
                else if (was && !now) HideLog(__instance);
                else if (now) LogOn(__instance)?.receiveLeftClick(__0, __1);
            }
        }

        [HarmonyPatch(typeof(CollectionsPage), nameof(CollectionsPage.performHoverAction))]
        internal static class HoverPatch
        {
            private static void Postfix(CollectionsPage __instance, int __0, int __1)
            {
                if (!IsSelected(__instance) || LogOn(__instance) is not { } log) return;
                // Over a side tab vanilla has already set the tab's label; the log's own hover clears.
                if (__instance.sideTabs.Values.Any(t => t.containsPoint(__0, __1))) log.performHoverAction(-1, -1);
                else log.performHoverAction(__0, __1);
            }
        }

        /// <summary>Draws the log first, so vanilla's side tabs and their labels draw over it.</summary>
        [HarmonyPatch(typeof(CollectionsPage), nameof(CollectionsPage.draw), new[] { typeof(SpriteBatch) })]
        internal static class DrawPatch
        {
            private static void Prefix(CollectionsPage __instance, SpriteBatch __0)
            {
                if (IsSelected(__instance)) LogOn(__instance)?.draw(__0);
            }
        }

        [HarmonyPatch(typeof(IClickableMenu), nameof(IClickableMenu.receiveScrollWheelAction))]
        internal static class ScrollPatch
        {
            private static void Postfix(IClickableMenu __instance, int __0)
            {
                if (__instance is CollectionsPage page && IsSelected(page)) LogOn(page)?.receiveScrollWheelAction(__0);
            }
        }

        /// <summary>Coming back to the Collections tab with the log selected, the cursor starts on the
        /// log's first row, as vanilla starts on a collection's first item.</summary>
        [HarmonyPatch(typeof(CollectionsPage), nameof(CollectionsPage.snapToDefaultClickableComponent))]
        internal static class DefaultSnapPatch
        {
            private static void Postfix(CollectionsPage __instance)
            {
                if (!IsSelected(__instance) || LogOn(__instance) == null) return;
                if (__instance.getComponentWithID(SeasonGoalsMenu.FirstRowId) is not { } row) return;
                __instance.currentlySnappedComponent = row;
                __instance.snapCursorToCurrentSnappedComponent();
            }
        }

        // ReSharper restore InconsistentNaming, UnusedMember.Local
    }
}
