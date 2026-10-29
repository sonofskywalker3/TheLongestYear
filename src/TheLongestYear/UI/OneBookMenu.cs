using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;
using TheLongestYear.Core;
using TheLongestYear.Core.Interactables;

namespace TheLongestYear.UI
{
    /// <summary>
    /// The one book (tanky24u, Nexus, 2026-10-28): the Cookbook, the Craftbook and the Herd Book as
    /// tabs of a single item, with a tab strip on the left in the Junimo Shrine's style
    /// (<see cref="TabStrip"/>). Only owned sections get a tab (<see cref="OneBook.TabsFor"/>).
    ///
    /// Each tab is the unchanged page menu (<see cref="CookbookMenu"/>, <see cref="CraftbookMenu"/>,
    /// <see cref="HerdBookMenu"/>), built with room on its left for the strip and hosted here: this
    /// menu stays <c>Game1.activeClickableMenu</c> for the whole visit and hands every input and
    /// draw to the current page. So a watcher that holds this menu (the rewind night,
    /// RunController.WatchRewindMenu) sees one menu from open to close whichever tabs are visited.
    /// When the page closes itself (close button, menu key, B) the whole book closes.
    ///
    /// Controller: the tabs join the page's snap graph (left from a row), and the shoulder
    /// buttons step through the tabs, as on the game's own menu.
    /// </summary>
    internal sealed class OneBookMenu : IClickableMenu, IRecipeBookMenu
    {
        private const int TabWidth = 160;
        private const int TabHeight = 48;
        private const int TabSpacing = 4;
        private const int TabGap = 8;
        private const int TabIdBase = 8700;
        /// <summary>One control id per possible tab, from <see cref="TabIdBase"/>.</summary>
        private static readonly int MaxTabs = Enum.GetValues(typeof(BookTab)).Length;
        private const int NoNeighbor = -1;

        /// <summary>Screen width each page leaves free on its left for the strip.</summary>
        private const int Inset = TabWidth + TabGap;

        private readonly IMonitor _monitor;
        private readonly MetaState _meta;
        private readonly IReadOnlyList<BookTab> _tabs;
        private readonly Func<BookTab, string> _subtitleFor;
        private readonly HashSet<BookTab> _visited = new HashSet<BookTab>();
        private readonly List<ClickableComponent> _tabButtons = new List<ClickableComponent>();

        private IClickableMenu _page;
        private bool _pageClosed;

        /// <summary>Tick of the last tab switch. With a controller one A press reaches the menu
        /// twice in the same tick (receiveGamePadButton, then a synthesized left click), and the
        /// second must not land on the new page's first row.</summary>
        private int _switchTick = -1;

        public BookTab ActiveTab { get; private set; }

        /// <summary>The tabs shown during this visit (the rewind night skips their offers).</summary>
        public IReadOnlyCollection<BookTab> Visited => _visited;

        /// <param name="tabs">The owned tabs; must not be empty.</param>
        /// <param name="subtitleFor">The line under each tab's title (the rewind night's "bank
        /// before the reset"), or null when opened from the book.</param>
        public OneBookMenu(IMonitor monitor, MetaState meta, IReadOnlyList<BookTab> tabs, BookTab open,
            Func<BookTab, string> subtitleFor = null)
            : base(0, 0, 0, 0, showUpperRightCloseButton: false)
        {
            _monitor = monitor;
            _meta = meta;
            _tabs = tabs;
            _subtitleFor = subtitleFor;
            ShowTab(OneBook.Resolve(tabs, open) ?? open, snapToTab: false);
        }

        private void ShowTab(BookTab tab, bool snapToTab)
        {
            if (_page != null)
            {
                // Leaving a page is the same as closing it for the page's own bookkeeping
                // (RecipeBookMenu marks its intro quest seen); the book itself stays open.
                _page.exitFunction = null;
                _page.emergencyShutDown();
            }
            ActiveTab = tab;
            _visited.Add(tab);
            _pageClosed = false;
            string subtitle = _subtitleFor?.Invoke(tab);
            switch (tab)
            {
                case BookTab.Cookbook:
                    _page = new CookbookMenu(_monitor, _meta, subtitle, Inset) { Host = this };
                    break;
                case BookTab.Craftbook:
                    _page = new CraftbookMenu(_monitor, _meta, subtitle, Inset) { Host = this };
                    break;
                default:
                    _page = new HerdBookMenu(_monitor, _meta, subtitle, Inset) { Host = this };
                    break;
            }
            _page.exitFunction = OnPageClosed;
            SyncLayout();
            if (snapToTab && Game1.options.snappyMenus && Game1.options.gamepadControls)
            {
                _page.currentlySnappedComponent = _tabButtons[IndexOf(tab)];
                _page.snapCursorToCurrentSnappedComponent();
            }
            _monitor.Log($"Book: showing the {tab} tab (tabs: {string.Join(", ", _tabs)}).", LogLevel.Trace);
        }

        /// <summary>The tab's place in the strip (0 when it is not shown).</summary>
        private int IndexOf(BookTab tab) => Math.Max(0, OneBook.IndexOf(_tabs, tab));

        private void SwitchTo(BookTab tab, bool snapToTab)
        {
            if (tab == ActiveTab) return;
            _switchTick = Game1.ticks;
            Game1.playSound("smallSelect");
            ShowTab(tab, snapToTab);
        }

        /// <summary>The page closed itself: close the book (the page already played its sound).</summary>
        private void OnPageClosed()
        {
            _pageClosed = true;
            if (Game1.activeClickableMenu == this)
                exitThisMenu(playSound: false);
        }

        /// <summary>Place the tab strip beside the page and splice the tabs into the page's own
        /// snap graph. Run again whenever the page rebuilds its components (window resize, a
        /// recipe removed).</summary>
        private void SyncLayout()
        {
            xPositionOnScreen = _page.xPositionOnScreen - Inset;
            yPositionOnScreen = _page.yPositionOnScreen;
            width = _page.width + Inset;
            height = _page.height;
            upperRightCloseButton = _page.upperRightCloseButton;

            // The first tab lines up with the page's first row, below a taller header when the
            // page shows a subtitle (the rewind night) or the over-cap notice.
            int tabTop = ((IBookPage)_page).HeaderHeight;
            _tabButtons.Clear();
            for (int i = 0; i < _tabs.Count; i++)
            {
                var bounds = new Rectangle(xPositionOnScreen, _page.yPositionOnScreen + tabTop + i * (TabHeight + TabSpacing),
                    TabWidth, TabHeight);
                _tabButtons.Add(new ClickableComponent(bounds, _tabs[i].ToString())
                {
                    myID = TabIdBase + i,
                    upNeighborID = i == 0 ? NoNeighbor : TabIdBase + i - 1,
                    downNeighborID = i == _tabs.Count - 1 ? NoNeighbor : TabIdBase + i + 1,
                });
            }

            List<ClickableComponent> components = _page.allClickableComponents ??= new List<ClickableComponent>();
            components.RemoveAll(IsTabButton);
            int activeTabId = TabIdBase + IndexOf(ActiveTab);
            int firstPageId = components.FirstOrDefault(c => c != _page.upperRightCloseButton)?.myID ?? NoNeighbor;
            foreach (ClickableComponent c in components)
                if (c != _page.upperRightCloseButton && c.leftNeighborID == NoNeighbor)
                    c.leftNeighborID = activeTabId;
            foreach (ClickableComponent tab in _tabButtons)
                tab.rightNeighborID = firstPageId;
            components.AddRange(_tabButtons);
            allClickableComponents = components;
        }

        private static bool IsTabButton(ClickableComponent c) => c.myID >= TabIdBase && c.myID < TabIdBase + MaxTabs;

        private BookTab? TabAt(int x, int y)
        {
            for (int i = 0; i < _tabButtons.Count; i++)
                if (_tabButtons[i].containsPoint(x, y)) return _tabs[i];
            return null;
        }

        // ---- IRecipeBookMenu (tly_booktest drives the book through these) ----

        public bool PickerOpen => (_page as IRecipeBookMenu)?.PickerOpen ?? false;

        public Rectangle? FirstEmptySlotBounds() => (_page as IRecipeBookMenu)?.FirstEmptySlotBounds();

        // ---- input: tabs first, everything else to the page ----

        public override void receiveLeftClick(int x, int y, bool playSound = true)
        {
            if (Game1.ticks == _switchTick) return;
            if (TabAt(x, y) is BookTab tab) { SwitchTo(tab, snapToTab: false); return; }
            _page.receiveLeftClick(x, y, playSound);
        }

        public override void receiveGamePadButton(Buttons b)
        {
            switch (b)
            {
                case Buttons.LeftShoulder:
                    SwitchTo(OneBook.Step(_tabs, ActiveTab, -1), snapToTab: false);
                    return;
                case Buttons.RightShoulder:
                    SwitchTo(OneBook.Step(_tabs, ActiveTab, +1), snapToTab: false);
                    return;
                case Buttons.A:
                    if (Game1.ticks == _switchTick) return;
                    int index = _tabButtons.IndexOf(_page.currentlySnappedComponent);
                    if (index >= 0) { SwitchTo(_tabs[index], snapToTab: true); return; }
                    break;
            }
            _page.receiveGamePadButton(b);
        }

        public override void receiveKeyPress(Keys key) => _page.receiveKeyPress(key);
        public override void receiveRightClick(int x, int y, bool playSound = true) => _page.receiveRightClick(x, y, playSound);
        public override void leftClickHeld(int x, int y) => _page.leftClickHeld(x, y);
        public override void releaseLeftClick(int x, int y) => _page.releaseLeftClick(x, y);
        public override void receiveScrollWheelAction(int direction) => _page.receiveScrollWheelAction(direction);
        public override void gamePadButtonHeld(Buttons b) => _page.gamePadButtonHeld(b);
        public override void performHoverAction(int x, int y) => _page.performHoverAction(x, y);
        public override void applyMovementKey(int direction) => _page.applyMovementKey(direction);
        public override bool areGamePadControlsImplemented() => _page.areGamePadControlsImplemented();
        public override bool shouldClampGamePadCursor() => _page.shouldClampGamePadCursor();
        public override bool overrideSnappyMenuCursorMovementBan() => _page.overrideSnappyMenuCursorMovementBan();
        public override bool readyToClose() => _page.readyToClose();
        public override bool isWithinBounds(int x, int y) => _page.isWithinBounds(x, y) || TabAt(x, y) != null;
        public override void setUpForGamePadMode() => _page.setUpForGamePadMode();
        public override void snapToDefaultClickableComponent() => _page.snapToDefaultClickableComponent();
        public override void snapCursorToCurrentSnappedComponent() => _page.snapCursorToCurrentSnappedComponent();
        public override void setCurrentlySnappedComponentTo(int id) => _page.setCurrentlySnappedComponentTo(id);
        public override ClickableComponent getCurrentlySnappedComponent() => _page.currentlySnappedComponent;

        public override void gameWindowSizeChanged(Rectangle oldBounds, Rectangle newBounds)
        {
            _page.gameWindowSizeChanged(oldBounds, newBounds);
            SyncLayout();
        }

        public override void update(GameTime time)
        {
            _page.update(time);
            // The page rebuilt its components (a recipe removed) or its header changed height (the
            // over-cap notice cleared): put the tabs back in line.
            if (_tabButtons.Count > 0
                && (_page.allClickableComponents?.Contains(_tabButtons[0]) != true
                    || _tabButtons[0].bounds.Y != _page.yPositionOnScreen + ((IBookPage)_page).HeaderHeight))
                SyncLayout();
            currentlySnappedComponent = _page.currentlySnappedComponent;
        }

        public override void emergencyShutDown()
        {
            base.emergencyShutDown();
            if (!_pageClosed) _page.emergencyShutDown();
        }

        protected override void cleanupBeforeExit()
        {
            base.cleanupBeforeExit();
            // Closed from outside (not by the page itself): give the page its close bookkeeping.
            if (!_pageClosed)
            {
                _pageClosed = true;
                _page.exitFunction = null;
                _page.emergencyShutDown();
            }
        }

        /// <summary>Dim the screen, then the tabs, then the page: a hosted page skips its own
        /// dimming (<see cref="IBookPage.Host"/>), so its cursor and any hover text are drawn
        /// once, above the tabs.</summary>
        public override void draw(SpriteBatch b)
        {
            b.Draw(Game1.fadeToBlackRect, new Rectangle(0, 0, Game1.uiViewport.Width, Game1.uiViewport.Height),
                Color.Black * 0.5f);
            for (int i = 0; i < _tabButtons.Count; i++)
                TabStrip.DrawTab(b, _tabButtons[i].bounds, TabLabel(_tabs[i]), _tabs[i] == ActiveTab);
            _page.draw(b);
        }

        private static string TabLabel(BookTab tab) => tab switch
        {
            BookTab.Cookbook => Strings.Get("menu.book.tab.cookbook"),
            BookTab.Craftbook => Strings.Get("menu.book.tab.craftbook"),
            _ => Strings.Get("menu.book.tab.herdbook"),
        };
    }
}
