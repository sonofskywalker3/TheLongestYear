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
    internal sealed class ShrinePreviewMenu : IClickableMenu
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

        public enum ShrineTab { Active, Boosts, Plan }

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
            Func<BoostId, int, BoostPurchase.Result> buyBoost = null)
            : base(0, 0, 0, 0, showUpperRightCloseButton: true)
        {
            _state = state;
            _priceFactor = priceFactor;
            _run = run;
            _buyBoost = buyBoost;
            BuildForesight();
            BuildRows();
            RecomputeBoundsAndLayout();
        }

        private bool BoostsWired => _run != null && _buyBoost != null;
        private bool ShowForesight => _tab == ShrineTab.Plan;
        private int Today => _run == null ? 1 : Calendar.DayOfYear((int)_run.Season, _run.DayOfMonth);

        // ------------------------------------------------------------------ foresight data

        private void BuildForesight()
        {
            int weatherTier = _state.HighestKeptTier("weather_sage_", 6);
            _weatherDays = weatherTier > 0
                ? WeatherForecast.Build(
                    (int)Game1.uniqueIDForThisGame, (int)Game1.stats.DaysPlayed,
                    Game1.dayOfMonth, (int)Game1.season, weatherTier,
                    GreenRainDay.VanillaSummerDay())
                : Array.Empty<ForecastDay>();
            // Rain Dance / Storm Call: slot 0 is tomorrow; show the bought weather, not the schedule.
            if (_run != null && _weatherDays.Length > 0 && _run.WeatherOverride != null
                && _run.WeatherOverrideDay == Today + 1)
                _weatherDays[0] = _weatherDays[0] with { Weather = _run.WeatherOverride };

            _cartItems.Clear();
            _cartHeader = null;
            _cartEmptyNote = null;
            _showCartBlock = _state.HasUpgrade("cart_whisper_1")
                && CartCatalogIntegration.Available(Game1.player);
            if (!_showCartBlock)
                return;

            bool catalogAnyDay = CartCatalogIntegration.Available(Game1.player);
            bool cartInTown = TravelingCartVisitsToday(Game1.dayOfMonth);
            if (!cartInTown && !catalogAnyDay)
            {
                _cartHeader = Strings.Get("menu.shrine-preview.cart-away", new Dictionary<string, string>
                {
                    ["day"] = ShortDayName(NextCartVisitDay(Game1.dayOfMonth)),
                });
                _cartEmptyNote = "";
                return;
            }

            try
            {
                var stock = StardewValley.Internal.ShopBuilder.GetShopStock("Traveler");
                foreach (var pair in stock)
                {
                    if (pair.Key is not Item item) continue;
                    if (!BundleRelevanceIndex.IsRelevant(item)) continue;
                    _cartItems.Add((pair.Key, pair.Value.Price, pair.Key.DisplayName));
                }
            }
            catch (Exception)
            {
                _cartItems.Clear();
            }

            _cartHeader = (catalogAnyDay && !cartInTown)
                ? Strings.Get("menu.shrine-preview.cart-catalog-header")
                : Strings.Get("menu.shrine-preview.cart-traveling-header");
            if (_cartItems.Count == 0)
                _cartEmptyNote = Strings.Get("menu.shrine-preview.cart-nothing");
        }

        /// <summary>The Traveling Cart is in town on days where <c>dayOfMonth % 7 % 5 == 0</c>.</summary>
        private static bool TravelingCartVisitsToday(int dayOfMonth) => dayOfMonth % 7 % 5 == 0;

        private static string ShortDayName(int dayOfMonth) => Game1.shortDayDisplayNameFromDayOfSeason(dayOfMonth);

        private static int NextCartVisitDay(int today)
        {
            for (int off = 1; off <= WeatherScheduler.DaysPerMonth; off++)
            {
                int dom = ((today - 1 + off) % WeatherScheduler.DaysPerMonth) + 1;
                if (dom % 7 % 5 == 0)
                    return dom;
            }
            return today;
        }

        private int ForesightPanelHeight()
        {
            if (!ShowForesight) return 0;
            int h = 0;
            if (_weatherDays.Length > 0)
                h += WeatherHeaderH + WeatherNumberRowH + WeatherIconRowH + ForesightBlockGap;
            if (_showCartBlock)
                h += CartHeaderH + CartIconRowH + ForesightBlockGap;
            return h;
        }

        // ------------------------------------------------------------------ rows per tab

        private static Row Header(string text) => new() { Kind = RowKind.Header, Text = text };
        private static Row Note(string text) => new() { Kind = RowKind.Note, Text = text };

        private void BuildRows()
        {
            _rows.Clear();
            switch (_tab)
            {
                case ShrineTab.Active: BuildActiveRows(); break;
                case ShrineTab.Boosts: BuildBoostRows(); break;
                default: BuildPlanRows(); break;
            }
        }

        private void BuildActiveRows()
        {
            _rows.Add(Header(Strings.Get("shrine.active.running")));
            int today = Today;
            List<ActiveBoost> running = _run == null
                ? new List<ActiveBoost>()
                : BoostPurchase.ActiveEntries(_run, today).ToList();
            if (running.Count == 0)
                _rows.Add(Note(Strings.Get("shrine.active.none")));
            foreach (ActiveBoost b in running)
            {
                if (!Enum.TryParse(b.Id, out BoostId id)) continue;
                BoostDefinition def = BoostCatalog.Get(id);
                string name = Strings.Get(def.NameKey);
                if (id == BoostId.CrashCourse && b.Skill >= 0)
                    name += " (" + SkillName(b.Skill) + ")";
                _rows.Add(new Row
                {
                    Kind = RowKind.Running, Text = name, Note = ExpiryLabel(b, def, today),
                    Tooltip = Strings.Get(def.DescKey),
                });
            }

            _rows.Add(Header(Strings.Get("shrine.active.this-week")));
            string bonus = ActiveEffectsProvider.BonusId;
            string liability = ActiveEffectsProvider.LiabilityId;
            if (bonus == null)
                _rows.Add(Note(Strings.Get("shrine.active.no-theme")));
            else
                _rows.Add(Note(Strings.Get("shrine.active.theme", new Dictionary<string, string>
                {
                    ["bonus"] = ThemeModifiers.DisplayNameFor(bonus),
                    ["liability"] = ThemeModifiers.DisplayNameFor(liability)
                        + (ActiveEffectsProvider.LiabilitySuppressed ? " " + Strings.Get("shrine.active.lifted") : ""),
                })));

            foreach (UpgradeCategory cat in Enum.GetValues(typeof(UpgradeCategory)))
            {
                IReadOnlyList<UpgradeDefinition> owned =
                    KeepShopFilter.OwnedLeavesInCategory(cat, _state, RunReachEvaluator.Meets);
                if (owned.Count == 0) continue;
                _rows.Add(Header(ThemeDisplay.CategoryName(cat)));
                foreach (UpgradeDefinition def in owned)
                    _rows.Add(new Row
                    {
                        Kind = RowKind.Upgrade, Def = def, IsOwned = true,
                        Tooltip = Strings.Get("menu.shrine-preview.tooltip-owned",
                            new Dictionary<string, string> { ["description"] = def.Description }),
                    });
            }
        }

        private string ExpiryLabel(ActiveBoost b, BoostDefinition def, int today)
        {
            if (b.ExpiresAfterDay >= Calendar.DaysPerYear)
                return Strings.Get("shrine.active.this-loop");
            if (b.ExpiresAfterDay == today)
                return Strings.Get("shrine.active.tonight");
            if (def.Duration == BoostDuration.Instant)
                return Strings.Get("shrine.active.tomorrow");
            return Strings.Get("shrine.active.through", new Dictionary<string, string>
            {
                ["season"] = Utility.getSeasonNameFromNumber((int)Calendar.SeasonOfDay(b.ExpiresAfterDay)),
                ["day"] = ((b.ExpiresAfterDay - 1) % Calendar.DaysPerMonth + 1).ToString(),
            });
        }

        /// <summary>Vanilla skill index order (Farming 0, Fishing 1, Foraging 2, Mining 3, Combat 4).</summary>
        private static string SkillName(int skill) => skill switch
        {
            0 => Strings.Get("skill.farming"),
            1 => Strings.Get("skill.fishing"),
            2 => Strings.Get("skill.foraging"),
            3 => Strings.Get("skill.mining"),
            4 => Strings.Get("skill.combat"),
            _ => "",
        };

        private static string GroupHeader(BoostDuration d) => d switch
        {
            BoostDuration.Instant => Strings.Get("shrine.boosts.group.instant"),
            BoostDuration.Week => Strings.Get("shrine.boosts.group.week"),
            BoostDuration.Season => Strings.Get("shrine.boosts.group.season"),
            _ => Strings.Get("shrine.boosts.group.loop"),
        };

        private void BuildBoostRows()
        {
            if (!BoostsWired)
            {
                _rows.Add(Note(Strings.Get("shrine.active.none")));
                return;
            }
            if (!Context.IsMainPlayer)
            {
                _rows.Add(Note(Strings.Get("shrine.boosts.host-only")));
                return;
            }
            foreach (BoostDuration d in new[] { BoostDuration.Instant, BoostDuration.Week, BoostDuration.Season, BoostDuration.Loop })
            {
                _rows.Add(Header(GroupHeader(d)));
                foreach (BoostDefinition boost in BoostCatalog.All.Where(b => b.Duration == d))
                {
                    _rows.Add(new Row { Kind = RowKind.Boost, Boost = boost, Tooltip = Strings.Get(boost.DescKey) });
                    if (boost.Id == BoostId.CrashCourse)
                    {
                        // A skill at 9 is never offered (10 must be earned): no row rather than "Not now".
                        IReadOnlyList<int> levels = BoostContextBuilder.Build(_run).SkillLevels;
                        for (int skill = 0; skill < SkillCount; skill++)
                        {
                            if (levels[skill] + 1 >= BoostPricing.MaxSkillLevel) continue;
                            _rows.Add(new Row { Kind = RowKind.Boost, Boost = boost, Skill = skill, Tooltip = Strings.Get(boost.DescKey) });
                        }
                    }
                }
            }
        }

        private void BuildPlanRows()
        {
            foreach (UpgradeCategory cat in Enum.GetValues(typeof(UpgradeCategory)))
            {
                IReadOnlyList<UpgradeDefinition> buyable =
                    KeepShopFilter.BuyableInCategory(cat, _state, RunReachEvaluator.Meets);
                List<UpgradeDefinition> locked = UpgradeCatalog.ByCategory(cat)
                    .Where(d => !_state.HasUpgrade(d.Id)
                                && (d.PrerequisiteId == null || _state.HasUpgrade(d.PrerequisiteId))
                                && _state.MeetsMetaRequirement(d.MetaRequirement)
                                && d.RunReachRequirement != null
                                && !RunReachEvaluator.Meets(d.RunReachRequirement))
                    .ToList();
                if (buyable.Count == 0 && locked.Count == 0)
                    continue;

                _rows.Add(Header(ThemeDisplay.CategoryName(cat)));
                foreach (UpgradeDefinition def in buyable)
                    _rows.Add(new Row
                    {
                        Kind = RowKind.Upgrade, Def = def,
                        Tooltip = Strings.Get("menu.shrine-preview.tooltip-buyable", new Dictionary<string, string>
                        {
                            ["description"] = def.Description,
                            ["owned"] = OwnedLabel(def),
                        }),
                    });
                if (locked.Count == 0) continue;
                _rows.Add(new Row
                {
                    Kind = RowKind.LockedToggle, Category = cat,
                    Text = Strings.Get("shrine.plan.locked", new Dictionary<string, string> { ["count"] = locked.Count.ToString() }),
                });
                if (!_expandedLocked.Contains(cat)) continue;
                foreach (UpgradeDefinition def in locked)
                    _rows.Add(new Row
                    {
                        Kind = RowKind.Locked, Def = def,
                        Requirement = ReachText.Describe(def.RunReachRequirement),
                        Tooltip = def.Description,
                    });
            }
            if (_rows.Count == 0)
                _rows.Add(Note(Strings.Get("menu.shrine-preview.nothing-new")));
        }

        private static string OwnedLabel(UpgradeDefinition def)
        {
            if (def.PrerequisiteId == null)
                return Strings.Get("menu.shrine-preview.owned-none");
            return UpgradeCatalog.TryGet(def.PrerequisiteId)?.DisplayName ?? def.PrerequisiteId;
        }

        // ------------------------------------------------------------------ boost row state

        private enum BoostRowState { Buy, Active, NotAvailable }

        private BoostContext ContextFor(Row row) => BoostContextBuilder.Build(_run, row.Skill);

        /// <summary>Rendered straight from <see cref="BoostPurchase.StateOf"/>, the same check
        /// TryBuy runs, so the control a player sees can never disagree with what a click does.
        /// NotEnoughJp still draws a Buy button: the shrine reports the shortfall on click.</summary>
        private BoostRowState StateOf(Row row)
            => BoostPurchase.StateOf(_state, _run, row.Boost.Id, ContextFor(row)) switch
            {
                BoostPurchase.Result.NotAvailable => BoostRowState.NotAvailable,
                BoostPurchase.Result.AlreadyActive => BoostRowState.Active,
                _ => BoostRowState.Buy,
            };

        /// <summary>The Crash Course parent row is a label; its five sub-rows carry the buttons.</summary>
        private static bool HasButton(Row row) => row.Kind == RowKind.Boost
            && (row.Boost.Id != BoostId.CrashCourse || row.Skill >= 0);

        private Rectangle BoostButtonBounds(int rowY)
            => new(_listX + _listWidth - 64 - BoostButtonWidth, rowY + 4, BoostButtonWidth, BoostButtonHeight);

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
            ShrineTab[] tabs = { ShrineTab.Active, ShrineTab.Boosts, ShrineTab.Plan };
            for (int i = 0; i < tabs.Length; i++)
            {
                string label = TabLabel(tabs[i]);
                _tabs.Add(new ClickableTextureComponent(
                    name: label,
                    bounds: new Rectangle(_listX + i * (TabWidth + TabGap), yPositionOnScreen + TabsTop, TabWidth, TabHeight),
                    label: null, hoverText: label,
                    texture: Game1.mouseCursors, sourceRect: new Rectangle(16, 368, 16, 16), scale: 1f)
                {
                    myID = TabIdBase + i,
                    leftNeighborID = i == 0 ? -1 : TabIdBase + i - 1,
                    rightNeighborID = i == tabs.Length - 1 ? -1 : TabIdBase + i + 1,
                    downNeighborID = RowIdBase,
                });
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

            allClickableComponents = new List<ClickableComponent>(_tabs) { _scrollUp, _scrollDown };
            allClickableComponents.AddRange(_rowSlots);
            if (upperRightCloseButton != null)
                allClickableComponents.Add(upperRightCloseButton);

            ClampScroll();
        }

        private static string TabLabel(ShrineTab tab) => tab switch
        {
            ShrineTab.Active => Strings.Get("shrine.tab.active"),
            ShrineTab.Boosts => Strings.Get("shrine.tab.boosts"),
            _ => Strings.Get("shrine.tab.plan"),
        };

        private void LayoutForesight()
        {
            _weatherCells.Clear();
            _cartCells.Clear();
            _weatherHeaderY = -1;
            _cartHeaderY = -1;
            if (!ShowForesight) return;

            int fy = yPositionOnScreen + TabsTop + TabStripH;

            if (_weatherDays.Length > 0)
            {
                _weatherHeaderY = fy;
                int numY = fy + WeatherHeaderH;
                for (int i = 0; i < _weatherDays.Length; i++)
                {
                    int cellX = _listX + i * WeatherCellWidth;
                    var bounds = new Rectangle(cellX, numY, WeatherCellWidth, WeatherNumberRowH + WeatherIconRowH);
                    _weatherCells.Add((bounds, _weatherDays[i]));
                }
                fy += WeatherHeaderH + WeatherNumberRowH + WeatherIconRowH + ForesightBlockGap;
            }

            if (_showCartBlock)
            {
                _cartHeaderY = fy;
                int iconY = fy + CartHeaderH;
                for (int i = 0; i < _cartItems.Count; i++)
                {
                    int cellX = _listX + i * CartIconCell;
                    var bounds = new Rectangle(cellX, iconY, CartIconPx, CartIconPx);
                    _cartCells.Add((bounds, _cartItems[i].Item, _cartItems[i].Price, _cartItems[i].Name));
                }
            }
        }

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
        /// every tab's row builder and draw path without a mouse.</summary>
        public void ShowTab(ShrineTab tab) => SetTab(tab);

        private void SetTab(ShrineTab tab)
        {
            if (_tab == tab) return;
            _tab = tab;
            _scrollIndex = 0;
            _hoverText = "";
            BuildRows();
            RecomputeBoundsAndLayout();
            Game1.playSound("smallSelect");
        }

        // ------------------------------------------------------------------ input

        public override void snapToDefaultClickableComponent()
        {
            currentlySnappedComponent = _tabs.Count > 0 ? _tabs[(int)_tab] : getComponentWithID(RowIdBase);
            snapCursorToCurrentSnappedComponent();
        }

        public override void receiveGamePadButton(Microsoft.Xna.Framework.Input.Buttons b)
        {
            if (b == Microsoft.Xna.Framework.Input.Buttons.A && currentlySnappedComponent != null)
            {
                int id = currentlySnappedComponent.myID;
                if (id >= TabIdBase && id < TabIdBase + _tabs.Count) { SetTab((ShrineTab)(id - TabIdBase)); return; }
                if (id == ScrollUpId) { Scroll(-1); return; }
                if (id == ScrollDownId) { Scroll(+1); return; }
                if (id >= RowIdBase && id < RowIdBase + _rowsPerPage)
                {
                    int idx = _scrollIndex + (id - RowIdBase);
                    if (idx < _rows.Count) ActivateRow(_rows[idx]);
                    return;
                }
            }
            base.receiveGamePadButton(b);
        }

        public override void receiveScrollWheelAction(int direction)
        {
            base.receiveScrollWheelAction(direction);
            Scroll(direction > 0 ? -1 : +1);
        }

        public override void receiveLeftClick(int x, int y, bool playSound = true)
        {
            base.receiveLeftClick(x, y, playSound);   // handles the close button
            for (int i = 0; i < _tabs.Count; i++)
            {
                if (_tabs[i].containsPoint(x, y)) { SetTab((ShrineTab)i); return; }
            }
            if (_scrollUp.containsPoint(x, y)) { Scroll(-1); return; }
            if (_scrollDown.containsPoint(x, y)) { Scroll(+1); return; }

            for (int i = 0; i < _rowsPerPage; i++)
            {
                int idx = _scrollIndex + i;
                if (idx >= _rows.Count) break;
                Row row = _rows[idx];
                int rowY = _listY + i * RowHeight;
                if (row.Kind == RowKind.LockedToggle && _rowSlots[i].containsPoint(x, y))
                {
                    ActivateRow(row);
                    return;
                }
                if (HasButton(row) && BoostButtonBounds(rowY).Contains(x, y))
                {
                    ActivateRow(row);
                    return;
                }
            }
        }

        /// <summary>Gamepad A / click on a row: toggle a Locked section, or buy a boost.</summary>
        private void ActivateRow(Row row)
        {
            if (row.Kind == RowKind.LockedToggle)
            {
                if (!_expandedLocked.Remove(row.Category)) _expandedLocked.Add(row.Category);
                Game1.playSound("shwip");
                BuildRows();
                ClampScroll();
                return;
            }
            if (!HasButton(row) || StateOf(row) != BoostRowState.Buy) return;
            _buyBoost(row.Boost.Id, row.Skill);   // sound, HUD and logging all live in the callback
            BuildRows();                          // the row's control flips to Active on success
            ClampScroll();
        }

        public override void performHoverAction(int x, int y)
        {
            base.performHoverAction(x, y);
            _hoverText = "";

            foreach (var (bounds, item, price, name) in _cartCells)
            {
                if (bounds.Contains(x, y))
                {
                    _hoverText = Strings.Get("menu.shrine-preview.cart-item-hover", new Dictionary<string, string>
                    {
                        ["name"] = name,
                        ["price"] = price.ToString(),
                    });
                    return;
                }
            }
            foreach (var (bounds, day) in _weatherCells)
            {
                if (bounds.Contains(x, y))
                {
                    _hoverText = Strings.Get("menu.shrine-preview.weather-day-hover", new Dictionary<string, string>
                    {
                        ["day"] = day.DayOfMonth.ToString(),
                        ["weather"] = WeatherIcons.Label(day.Weather),
                    });
                    return;
                }
            }

            for (int i = 0; i < _rowsPerPage; i++)
            {
                int idx = _scrollIndex + i;
                if (idx >= _rows.Count) break;
                if (_rows[idx].Tooltip != null && _rowSlots[i].containsPoint(x, y))
                {
                    _hoverText = _rows[idx].Tooltip;
                    return;
                }
            }
        }

        // ------------------------------------------------------------------ draw

        public override void draw(SpriteBatch b)
        {
            b.Draw(Game1.fadeToBlackRect, new Rectangle(0, 0, Game1.uiViewport.Width, Game1.uiViewport.Height),
                Color.Black * 0.5f);
            IClickableMenu.drawTextureBox(b, xPositionOnScreen, yPositionOnScreen, width, height, Color.White);

            SpriteText.drawStringHorizontallyCenteredAt(b, Strings.Get("menu.shrine-preview.title"),
                xPositionOnScreen + width / 2, yPositionOnScreen + 24);
            Utility.drawTextWithShadow(b,
                Strings.Get("menu.shrine-preview.banked", new Dictionary<string, string> { ["jp"] = _state.JunimoPoints.ToString() }),
                Game1.smallFont,
                new Vector2(xPositionOnScreen + 40, yPositionOnScreen + 80), Game1.textColor);

            // Keeps are bought at the loop boundary; this board sells boosts only. Say so on the
            // JP line so nobody tries to buy a keep from the Plan tab (several testers did).
            string planningNote = Strings.Get("menu.shrine-preview.planning-note");
            Vector2 noteSize = Game1.smallFont.MeasureString(planningNote);
            string bankedLine = Strings.Get("menu.shrine-preview.banked", new Dictionary<string, string> { ["jp"] = _state.JunimoPoints.ToString() });
            float bankedRight = xPositionOnScreen + 40 + Game1.smallFont.MeasureString(bankedLine).X + 32;
            float noteX = xPositionOnScreen + width - 40 - noteSize.X;
            if (noteX > bankedRight)   // only when it fits beside the JP line; never overlap it
                Utility.drawTextWithShadow(b, planningNote, Game1.smallFont,
                    new Vector2(noteX, yPositionOnScreen + 80), NoteBrown);

            for (int i = 0; i < _tabs.Count; i++)
            {
                ClickableTextureComponent tab = _tabs[i];
                Color tint = (int)_tab == i ? Color.White : Color.White * 0.7f;
                IClickableMenu.drawTextureBox(b, Game1.menuTexture, new Rectangle(0, 256, 60, 60),
                    tab.bounds.X, tab.bounds.Y, tab.bounds.Width, tab.bounds.Height, tint, 1f, false);
                string label = TabLabel((ShrineTab)i);
                Vector2 labelSize = Game1.smallFont.MeasureString(label);
                Utility.drawTextWithShadow(b, label, Game1.smallFont,
                    new Vector2(tab.bounds.X + (tab.bounds.Width - labelSize.X) / 2f,
                        tab.bounds.Y + (tab.bounds.Height - labelSize.Y) / 2f),
                    Game1.textColor);
            }

            DrawForesight(b);

            for (int i = 0; i < _rowsPerPage; i++)
            {
                int idx = _scrollIndex + i;
                if (idx >= _rows.Count) break;
                DrawRow(b, _rows[idx], _listY + i * RowHeight);
            }

            if (MaxScroll() > 0)
            {
                _scrollUp.draw(b, _scrollIndex > 0 ? Color.White : Color.Gray, 1f);
                _scrollDown.draw(b, _scrollIndex < MaxScroll() ? Color.White : Color.Gray, 1f);
            }

            base.draw(b);
            if (!string.IsNullOrEmpty(_hoverText))
                IClickableMenu.drawHoverText(b, _hoverText, Game1.smallFont);
            Game1.mouseCursorTransparency = 1f;
            this.drawMouse(b);
        }

        private void DrawRow(SpriteBatch b, Row row, int rowY)
        {
            switch (row.Kind)
            {
                case RowKind.Header:
                    Utility.drawTextWithShadow(b, row.Text, Game1.dialogueFont,
                        new Vector2(_listX, rowY + 6), Game1.textColor);
                    break;
                case RowKind.Note:
                    Utility.drawTextWithShadow(b, row.Text, Game1.smallFont,
                        new Vector2(_listX + 24, rowY + 6), Game1.textColor * 0.85f);
                    break;
                case RowKind.Running:
                {
                    Utility.drawTextWithShadow(b, row.Text, Game1.smallFont,
                        new Vector2(_listX + 24, rowY + 6), OwnedGreen);
                    Vector2 noteSize = Game1.smallFont.MeasureString(row.Note);
                    Utility.drawTextWithShadow(b, row.Note, Game1.smallFont,
                        new Vector2(_listX + _listWidth - 64 - noteSize.X, rowY + 6), OwnedGreen);
                    break;
                }
                case RowKind.Boost:
                    DrawBoostRow(b, row, rowY);
                    break;
                case RowKind.Upgrade when row.IsOwned:
                {
                    Utility.drawTextWithShadow(b, row.Def.DisplayName, Game1.smallFont,
                        new Vector2(_listX + 24, rowY + 6), OwnedGreen);
                    string ownedLabel = Strings.Get("menu.shrine.owned");
                    Vector2 ownedSize = Game1.smallFont.MeasureString(ownedLabel);
                    Utility.drawTextWithShadow(b, ownedLabel, Game1.smallFont,
                        new Vector2(_listX + _listWidth - 64 - ownedSize.X, rowY + 6), OwnedGreen);
                    break;
                }
                case RowKind.Upgrade:
                {
                    long costJp = UpgradePricing.EffectiveCost(row.Def, _priceFactor);
                    bool affordable = _state.JunimoPoints >= costJp;
                    Utility.drawTextWithShadow(b, row.Def.DisplayName, Game1.smallFont,
                        new Vector2(_listX + 24, rowY + 6), Game1.textColor);
                    string cost = Strings.Get("menu.shrine-preview.cost",
                        new Dictionary<string, string> { ["cost"] = costJp.ToString() });
                    Vector2 costSize = Game1.smallFont.MeasureString(cost);
                    Utility.drawTextWithShadow(b, cost, Game1.smallFont,
                        new Vector2(_listX + _listWidth - 64 - costSize.X, rowY + 6),
                        affordable ? Game1.textColor : Color.Brown);
                    break;
                }
                case RowKind.LockedToggle:
                {
                    string arrow = _expandedLocked.Contains(row.Category) ? "v " : "> ";
                    Utility.drawTextWithShadow(b, arrow + row.Text, Game1.smallFont,
                        new Vector2(_listX + 24, rowY + 6), LockedGray);
                    break;
                }
                case RowKind.Locked:
                {
                    // "Keep Big Coop - unlocked once it's built", cost right-aligned like a buyable row.
                    string title = row.Def.DisplayName
                        + (string.IsNullOrEmpty(row.Requirement) ? "" : " - " + row.Requirement);
                    Utility.drawTextWithShadow(b, title, Game1.smallFont,
                        new Vector2(_listX + SubRowIndent, rowY + 6), LockedGray);
                    long lockedCost = UpgradePricing.EffectiveCost(row.Def, _priceFactor);
                    string lockedCostText = Strings.Get("menu.shrine-preview.cost",
                        new Dictionary<string, string> { ["cost"] = lockedCost.ToString() });
                    Vector2 lockedCostSize = Game1.smallFont.MeasureString(lockedCostText);
                    Utility.drawTextWithShadow(b, lockedCostText, Game1.smallFont,
                        new Vector2(_listX + _listWidth - 64 - lockedCostSize.X, rowY + 6), LockedGray);
                    break;
                }
            }
        }

        /// <summary>One Boosts row: name and cost on the left, and on the right either a Buy button
        /// (greyed when the JP is not there, but still clickable so the HUD can say why), an Active
        /// label, or a "Not now" label. Crash Course draws a label row plus five skill sub-rows;
        /// Elevator Pass shows the floors.</summary>
        private void DrawBoostRow(SpriteBatch b, Row row, int rowY)
        {
            BoostDefinition boost = row.Boost;
            if (boost.Id == BoostId.CrashCourse && row.Skill < 0)
            {
                Utility.drawTextWithShadow(b, Strings.Get(boost.NameKey), Game1.smallFont,
                    new Vector2(_listX + 24, rowY + 6), Game1.textColor);
                // The price multiplier so far this loop (3^n), right-aligned above the Buy buttons.
                string multiplier = Strings.Get("shrine.boosts.multiplier", new Dictionary<string, string>
                {
                    ["factor"] = ((long)Math.Pow(3, _run.SkillLevelsBoughtTotal)).ToString(),
                });
                Vector2 multSize = Game1.smallFont.MeasureString(multiplier);
                Utility.drawTextWithShadow(b, multiplier, Game1.smallFont,
                    new Vector2(_listX + _listWidth - 64 - multSize.X, rowY + 6), NoteBrown);
                return;
            }

            BoostContext ctx = ContextFor(row);
            string name;
            int x = _listX + 24;
            if (boost.Id == BoostId.CrashCourse)
            {
                int from = ctx.SkillLevels[row.Skill];
                name = Strings.Get("shrine.boosts.crash-course-row", new Dictionary<string, string>
                {
                    ["skill"] = SkillName(row.Skill), ["from"] = from.ToString(), ["to"] = (from + 1).ToString(),
                });
                x = _listX + SubRowIndent;
            }
            else if (boost.Id == BoostId.ElevatorPass)
            {
                name = Strings.Get(boost.NameKey) + ": " + Strings.Get("shrine.boosts.elevator-row", new Dictionary<string, string>
                {
                    ["from"] = ctx.MineFloor.ToString(),
                    ["to"] = BoostPricing.ElevatorLanding(ctx.MineFloor).ToString(),
                });
            }
            else
            {
                name = Strings.Get(boost.NameKey);
            }

            BoostRowState rowState = StateOf(row);
            long costJp = BoostPricing.CostOf(boost, _run, ctx);
            bool affordable = _state.JunimoPoints >= costJp;

            Utility.drawTextWithShadow(b, name, Game1.smallFont, new Vector2(x, rowY + 6), Game1.textColor);

            Rectangle button = BoostButtonBounds(rowY);
            if (rowState != BoostRowState.NotAvailable)
            {
                string cost = Strings.Get("menu.shrine-preview.cost",
                    new Dictionary<string, string> { ["cost"] = costJp.ToString() });
                Vector2 costSize = Game1.smallFont.MeasureString(cost);
                Utility.drawTextWithShadow(b, cost, Game1.smallFont,
                    new Vector2(button.X - 16 - costSize.X, rowY + 6),
                    affordable ? Game1.textColor : Color.Brown);
            }

            if (rowState == BoostRowState.Buy)
            {
                IClickableMenu.drawTextureBox(b, Game1.mouseCursors, new Rectangle(432, 439, 9, 9),
                    button.X, button.Y, button.Width, button.Height,
                    affordable ? Color.White : Color.Gray, 4f, drawShadow: false);
                string buy = Strings.Get("shrine.boosts.buy");
                Vector2 buySize = Game1.smallFont.MeasureString(buy);
                Utility.drawTextWithShadow(b, buy, Game1.smallFont,
                    new Vector2(button.X + (button.Width - buySize.X) / 2f,
                        button.Y + (button.Height - buySize.Y) / 2f),
                    Game1.textColor);
                return;
            }

            string label = rowState == BoostRowState.Active
                ? Strings.Get("shrine.boosts.active")
                : boost.Id == BoostId.ElevatorPass && ctx.MineFloor <= 0
                    ? Strings.Get("shrine.boosts.enter-mine")
                    : boost.Id == BoostId.ElevatorPass
                        ? Strings.Get("shrine.boosts.bottom-reached")
                        : Strings.Get("shrine.boosts.not-available");
            Color labelColor = rowState == BoostRowState.Active ? OwnedGreen : Color.Brown;
            Vector2 labelSize = Game1.smallFont.MeasureString(label);
            Utility.drawTextWithShadow(b, label, Game1.smallFont,
                new Vector2(button.X + (button.Width - labelSize.X) / 2f, rowY + 6), labelColor);
        }

        private void DrawForesight(SpriteBatch b)
        {
            if (_weatherCells.Count > 0)
            {
                Utility.drawTextWithShadow(b, Strings.Get("menu.shrine-preview.weather-header"), Game1.dialogueFont,
                    new Vector2(_listX, _weatherHeaderY), Game1.textColor);
                int numY = _weatherHeaderY + WeatherHeaderH;
                int iconY = numY + WeatherNumberRowH;
                foreach (var (bounds, day) in _weatherCells)
                {
                    DrawCell(b, new Rectangle(bounds.X + 2, bounds.Y, bounds.Width - 4, bounds.Height));

                    string num = day.DayOfMonth.ToString();
                    Vector2 ns = Game1.smallFont.MeasureString(num);
                    Utility.drawTextWithShadow(b, num, Game1.smallFont,
                        new Vector2(bounds.X + (WeatherCellWidth - ns.X) / 2f, numY), Game1.textColor);

                    var (tex, src) = WeatherIcons.Source(day.Weather);
                    float iconX = bounds.X + (WeatherCellWidth - WeatherIconPx) / 2f;
                    b.Draw(tex, new Vector2(iconX, iconY), src, Color.White, 0f,
                        Vector2.Zero, WeatherIconScale, SpriteEffects.None, 0.9f);
                }
            }

            if (ShowForesight && _showCartBlock)
            {
                Utility.drawTextWithShadow(b, _cartHeader, Game1.dialogueFont,
                    new Vector2(_listX, _cartHeaderY), Game1.textColor);
                if (_cartCells.Count > 0)
                {
                    foreach (var (bounds, item, price, name) in _cartCells)
                        item.drawInMenu(b, new Vector2(bounds.X, bounds.Y), CartIconScale, 1f, 0.9f,
                            StackDrawType.Hide, Color.White, drawShadow: true);
                }
                else if (!string.IsNullOrEmpty(_cartEmptyNote))
                {
                    Utility.drawTextWithShadow(b, _cartEmptyNote, Game1.smallFont,
                        new Vector2(_listX + 24, _cartHeaderY + CartHeaderH + 8), Game1.textColor * 0.8f);
                }
            }
        }

        /// <summary>A faint filled cell with a thin border, drawn from the 1x1 white pixel
        /// (<c>Game1.staminaRect</c>): the calendar-grid backing for a weather column.</summary>
        private static void DrawCell(SpriteBatch b, Rectangle r)
        {
            Color fill = Color.SaddleBrown * 0.10f;
            Color border = Color.SaddleBrown * 0.40f;
            b.Draw(Game1.staminaRect, r, fill);
            b.Draw(Game1.staminaRect, new Rectangle(r.X, r.Y, r.Width, 2), border);
            b.Draw(Game1.staminaRect, new Rectangle(r.X, r.Bottom - 2, r.Width, 2), border);
            b.Draw(Game1.staminaRect, new Rectangle(r.X, r.Y, 2, r.Height), border);
            b.Draw(Game1.staminaRect, new Rectangle(r.Right - 2, r.Y, 2, r.Height), border);
        }
    }
}
