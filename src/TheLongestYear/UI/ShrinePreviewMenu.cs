using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.BellsAndWhistles;
using StardewValley.Menus;
using TheLongestYear.Core;
using TheLongestYear.Donations;
using TheLongestYear.Integration;
using TheLongestYear.Loop;

namespace TheLongestYear.UI
{
    /// <summary>The planning shrine (the Junimo statue on the farm), three tabs
    /// (spec docs/superpowers/specs/2026-08-29-shrine-tabs-jp-boosts-design.md, section 3):
    /// <list type="bullet">
    /// <item><b>Active</b> (default, read-only): running boosts with their expiry, this week's
    /// theme bonus and liability, and every owned permanent leaf by category.</item>
    /// <item><b>Boosts</b>: the JP Boost roster grouped by duration class, each row with a Buy
    /// button, an Active label or a "Not now" label straight from <see cref="BoostPurchase.StateOf"/>.
    /// Host-only in multiplayer (JP is one pool per save).</item>
    /// <item><b>Plan</b>: the foresight calendar (Weather Sage forecast + Traveling Cart stock),
    /// then per category the next purchasable keep of each chain with its cost, then a collapsed
    /// Locked section naming what each reach-gated keep still needs this loop. Keeps are bought
    /// only on the loop-boundary JP perk screen; this tab shows the price and the effect.</item>
    /// </list></summary>
    /// <remarks>A fourth tab, <b>Donate</b>, shows on weeks with random shrine donation goals
    /// (ShrinePreviewMenu.Donate.cs).</remarks>
    internal sealed partial class ShrinePreviewMenu : IClickableMenu
    {
        private const int RowHeight = 56;
        private const int RowIdBase = 7000;
        private const int ScrollUpId = 7900;
        private const int ScrollDownId = 7901;

        // ---- Tab strip (below the title + JP line) ----
        private const int TabIdBase = 6200;
        private const int TabWidth = 220;
        private const int TabHeight = 52;
        private const int TabGap = 8;
        private const int TabsTop = 112;
        private const int TabStripH = TabHeight + 12;
        private const int TabMinWidth = 140;   // four-tab floor: the longest label still fits

        // ---- Restart the year (spec 2026-09-24-voluntary-restart): right end of the tab strip ----
        private const int RestartButtonId = 6300;
        private const int RestartButtonMinWidth = 220;
        private const int RestartButtonPadding = 32;

        // ---- Foresight calendar panel (Plan tab, drawn above the scrolling list) ----
        private const int ForesightBlockGap = 14;       // vertical gap after weather / cart blocks
        private const int WeatherCellWidth = 64;
        private const float WeatherIconScale = 3f;       // 13px source -> 39px
        private const int WeatherIconPx = 39;
        private const int WeatherHeaderH = 40;
        private const int WeatherNumberRowH = 30;
        private const int WeatherIconRowH = 52;
        private const int CartHeaderH = 40;
        private const int CartIconCell = 72;
        private const float CartIconScale = 1f;          // drawInMenu 1f -> 64px
        private const int CartIconPx = 64;
        private const int CartIconRowH = 72;

        // ---- Boost rows (the one part of this board that DOES spend JP) ----
        private const int BoostButtonWidth = 132;
        private const int BoostButtonHeight = 44;
        private const int SubRowIndent = 48;
        private const int SkillCount = 5;

        private static readonly Color OwnedGreen = new(30, 130, 30);
        private static readonly Color LockedGray = new(110, 100, 90);
        private static readonly Color NoteBrown = new(120, 90, 40);

        /// <summary>The value is the tab's index in the strip. Donate is last and only present on
        /// weeks with shrine goals.</summary>
        public enum ShrineTab { Active, Boosts, Plan, Donate }

        private enum RowKind { Header, Note, Running, Boost, Upgrade, LockedToggle, Locked }

        private sealed class Row
        {
            public RowKind Kind;
            public string Text;                  // header / note / running-boost name / locked-toggle label
            public string Note;                  // right-hand text (running boosts: expiry)
            public UpgradeDefinition Def;        // Upgrade + Locked rows
            public BoostDefinition Boost;        // Boost rows
            public int Skill = -1;               // Crash Course sub-rows: the skill; -1 = the parent row
            public string Tooltip;               // hover text (null = none)
            public bool IsOwned;                 // Upgrade rows: owned leaf (green) vs buyable (cost)
            public string Requirement;           // Locked rows: ReachText
            public UpgradeCategory Category;     // LockedToggle rows
        }

        private readonly MetaState _state;
        private readonly List<Row> _rows = new();
        private readonly RunState _run;
        private readonly Func<BoostId, int, BoostPurchase.Result> _buyBoost;
        private readonly double _priceFactor;
        private readonly bool _showRestart;
        private readonly Action _requestRestart;
        private ClickableComponent _restartButton;

        private ShrineTab _tab = ShrineTab.Active;
        private readonly List<ClickableTextureComponent> _tabs = new();
        private readonly HashSet<UpgradeCategory> _expandedLocked = new();

        // Foresight data, fetched once at open (rolling: reads the live Game1 date).
        private ForecastDay[] _weatherDays = Array.Empty<ForecastDay>();
        private readonly List<(ISalable Item, int Price, string Name)> _cartItems = new();
        private string _cartHeader;
        private bool _showCartBlock;
        private string _cartEmptyNote;
        private readonly List<(Rectangle Bounds, ForecastDay Day)> _weatherCells = new();
        private readonly List<(Rectangle Bounds, ISalable Item, int Price, string Name)> _cartCells = new();
        private int _weatherHeaderY = -1;
        private int _cartHeaderY = -1;

        private int _scrollIndex;
        private int _rowsPerPage;
        private int _listX, _listY, _listWidth;
        private readonly List<ClickableComponent> _rowSlots = new();
        private ClickableTextureComponent _scrollUp;
        private ClickableTextureComponent _scrollDown;
        private string _hoverText = "";

        public ShrinePreviewMenu(MetaState state, double priceFactor = 1.0, RunState run = null,
            Func<BoostId, int, BoostPurchase.Result> buyBoost = null,
            Func<bool> restartOffered = null, Action requestRestart = null,
            ShrineDonationService donations = null)
            : base(0, 0, 0, 0, showUpperRightCloseButton: true)
        {
            _state = state;
            // Read once at open, like the restart button: the tab shows only on weeks with goals,
            // and only for the host (shrine goals live in the host's save data, like Boosts).
            _donations = donations;
            _showDonate = Context.IsMainPlayer && donations != null && donations.Goals.Count > 0;
            _priceFactor = priceFactor;
            _run = run;
            _buyBoost = buyBoost;
            // Read once at open: single-player time is paused while a menu is up, so nothing the
            // rule reads can change before the menu closes.
            _requestRestart = requestRestart;
            _showRestart = requestRestart != null && (restartOffered?.Invoke() ?? false);
            BuildForesight();
            BuildRows();
            RecomputeBoundsAndLayout();
        }

        private bool BoostsWired => _run != null && _buyBoost != null;
        private bool ShowForesight => _tab == ShrineTab.Plan;
        private int Today => _run == null ? 1 : Calendar.DayOfYear((int)_run.Season, _run.DayOfMonth);

        // ------------------------------------------------------------------ layout

        public override void gameWindowSizeChanged(Rectangle oldBounds, Rectangle newBounds)
        {
            base.gameWindowSizeChanged(oldBounds, newBounds);
            RecomputeBoundsAndLayout();
        }

        private void RecomputeBoundsAndLayout()
        {
            width = Math.Min(1260, Game1.uiViewport.Width - 64);
            height = Math.Min(1020, Game1.uiViewport.Height - 64);
            xPositionOnScreen = (Game1.uiViewport.Width - width) / 2;
            yPositionOnScreen = (Game1.uiViewport.Height - height) / 2;

            _listX = xPositionOnScreen + 40;
            _listWidth = width - 80;

            _tabs.Clear();
            List<ShrineTab> tabs = new() { ShrineTab.Active, ShrineTab.Boosts, ShrineTab.Plan };
            if (_showDonate)
                tabs.Add(ShrineTab.Donate);
            int contentTopId = _tab == ShrineTab.Donate ? DonateFirstTargetId() : RowIdBase;

            int restartWidth = 0;
            if (_showRestart)
            {
                string restartLabel = Strings.Get("shrine.restart.button");
                restartWidth = Math.Max(RestartButtonMinWidth, (int)Game1.smallFont.MeasureString(restartLabel).X + RestartButtonPadding);
            }
            // Three tabs keep TabWidth exactly; the Donate tab shrinks all four to clear the button.
            int stripRoom = _showRestart ? _listWidth - restartWidth - TabGap : _listWidth;
            int tabWidth = ShrineTabLayout.TabWidth(tabs.Count, TabWidth, TabGap, stripRoom, TabMinWidth);

            for (int i = 0; i < tabs.Count; i++)
            {
                string label = TabLabel(tabs[i]);
                _tabs.Add(new ClickableTextureComponent(
                    name: label,
                    bounds: new Rectangle(_listX + i * (tabWidth + TabGap), yPositionOnScreen + TabsTop, tabWidth, TabHeight),
                    label: null, hoverText: label,
                    texture: Game1.mouseCursors, sourceRect: new Rectangle(16, 368, 16, 16), scale: 1f)
                {
                    myID = TabIdBase + i,
                    leftNeighborID = i == 0 ? -1 : TabIdBase + i - 1,
                    rightNeighborID = i == tabs.Count - 1 ? -1 : TabIdBase + i + 1,
                    downNeighborID = contentTopId,
                });
            }

            _restartButton = null;
            if (_showRestart)
            {
                _restartButton = new ClickableComponent(
                    new Rectangle(_listX + _listWidth - restartWidth, yPositionOnScreen + TabsTop, restartWidth, TabHeight), "restart")
                {
                    myID = RestartButtonId,
                    leftNeighborID = TabIdBase + tabs.Count - 1,
                    downNeighborID = contentTopId,
                };
                _tabs[tabs.Count - 1].rightNeighborID = RestartButtonId;
            }

            LayoutForesight();

            _listY = yPositionOnScreen + TabsTop + TabStripH + ForesightPanelHeight();
            int listHeight = height - (_listY - yPositionOnScreen) - 40;
            _rowsPerPage = Math.Max(1, listHeight / RowHeight);

            _rowSlots.Clear();
            for (int i = 0; i < _rowsPerPage; i++)
                _rowSlots.Add(new ClickableComponent(
                    new Rectangle(_listX, _listY + i * RowHeight, _listWidth - 56, RowHeight),
                    "row-" + i) { myID = RowIdBase + i, upNeighborID = i == 0 ? TabIdBase : RowIdBase + i - 1 });

            int arrowX = _listX + _listWidth - 48;
            _scrollUp = new ClickableTextureComponent("scroll-up",
                new Rectangle(arrowX, _listY, 44, 48), null, null,
                Game1.mouseCursors, new Rectangle(421, 459, 11, 12), 4f) { myID = ScrollUpId };
            _scrollDown = new ClickableTextureComponent("scroll-down",
                new Rectangle(arrowX, _listY + listHeight - 48, 44, 48), null, null,
                Game1.mouseCursors, new Rectangle(421, 472, 11, 12), 4f) { myID = ScrollDownId };

            this.initializeUpperRightCloseButton();

            LayoutDonate();

            allClickableComponents = new List<ClickableComponent>(_tabs);
            if (_tab != ShrineTab.Donate)
                allClickableComponents.AddRange(new[] { _scrollUp, _scrollDown });
            if (_restartButton != null)
                allClickableComponents.Add(_restartButton);
            if (_tab == ShrineTab.Donate)
                AddDonateTargets(allClickableComponents);
            else
                allClickableComponents.AddRange(_rowSlots);
            if (upperRightCloseButton != null)
                allClickableComponents.Add(upperRightCloseButton);

            ClampScroll();
        }

        private static string TabLabel(ShrineTab tab) => tab switch
        {
            ShrineTab.Active => Strings.Get("shrine.tab.active"),
            ShrineTab.Boosts => Strings.Get("shrine.tab.boosts"),
            ShrineTab.Donate => Strings.Get("shrine.tab.donate"),
            _ => Strings.Get("shrine.tab.plan"),
        };

        private int MaxScroll() => Math.Max(0, _rows.Count - _rowsPerPage);

        private void ClampScroll()
        {
            if (_scrollIndex < 0) _scrollIndex = 0;
            if (_scrollIndex > MaxScroll()) _scrollIndex = MaxScroll();
        }

        private void Scroll(int delta)
        {
            int before = _scrollIndex;
            _scrollIndex += delta;
            ClampScroll();
            if (_scrollIndex != before)
                Game1.playSound("shwip");
        }

        /// <summary>Debug entry (tly_openshrine): open on a given tab so the bridge can exercise
        /// every tab's row builder and draw path without a mouse.
        /// Returns false (and stays put) for the Donate tab on a week without shrine goals.</summary>
        public bool ShowTab(ShrineTab tab)
        {
            if (tab == ShrineTab.Donate && !_showDonate) return false;
            SetTab(tab);
            return true;
        }

        private void SetTab(ShrineTab tab)
        {
            if (_tab == tab) return;
            if (tab == ShrineTab.Donate && !_showDonate) return;
            _tab = tab;
            _scrollIndex = 0;
            _hoverText = "";
            BuildRows();
            RecomputeBoundsAndLayout();
            Game1.playSound("smallSelect");
        }
    }
}
