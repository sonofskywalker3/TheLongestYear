using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.BellsAndWhistles;
using StardewValley.Menus;
using TheLongestYear.Core;
using TheLongestYear.Integration;
using TheLongestYear.Loop;

namespace TheLongestYear.UI
{
    /// <summary>Read-only "what could I buy next reset" planning board. No purchasing — that stays
    /// on the loop-boundary shrine popup. A pinned, calendar-style foresight panel sits on top
    /// (Weather Sage forecast + Traveling Cart stock, both rolling off the live date), and below it
    /// the buyable-upgrade list: for every category, only the next purchasable tier of each chain
    /// (reach-gated, owned tiers hidden). Each upgrade row shows its cost; hovering shows the effect.
    /// Fully scrollable.</summary>
    internal sealed class ShrinePreviewMenu : IClickableMenu
    {
        private const int RowHeight = 56;
        private const int RowIdBase = 7000;
        private const int ScrollUpId = 7900;
        private const int ScrollDownId = 7901;

        // ---- Foresight calendar panel (drawn above the scrolling upgrade list) ----
        private const int ForesightTop = 112;          // below title + JP line
        private const int ForesightBlockGap = 14;       // vertical gap after weather / cart blocks
        // Weather: a "Weather" header, a row of day-of-month numbers, then a row of HUD weather icons.
        private const int WeatherCellWidth = 64;
        private const float WeatherIconScale = 3f;       // 13px source → 39px
        private const int WeatherIconPx = 39;
        private const int WeatherHeaderH = 40;
        private const int WeatherNumberRowH = 30;
        private const int WeatherIconRowH = 52;
        // Cart: a "Traveling Cart - <Weekday>" header, then a row of hoverable item icons.
        private const int CartHeaderH = 40;
        private const int CartIconCell = 72;
        private const float CartIconScale = 1f;          // drawInMenu 1f → 64px
        private const int CartIconPx = 64;
        private const int CartIconRowH = 72;

        private sealed class Row
        {
            public bool IsHeader;
            public string Text;                  // header label (when IsHeader)
            public UpgradeDefinition Def;        // the upgrade (when !IsHeader)
            public string Tooltip;               // precomputed hover text (when !IsHeader)
            public bool IsOwned;                 // owned leaf → green, no cost (vs buyable → cost)
        }

        private readonly MetaState _state;
        private readonly List<Row> _rows = new();

        // Foresight data, fetched once at open (rolling — reads live Game1 date so re-opening on a
        // later day shows a later window). Bounds for hover/draw are computed in the layout pass.
        private ForecastDay[] _weatherDays = System.Array.Empty<ForecastDay>();
        private readonly List<(ISalable Item, int Price, string Name)> _cartItems = new();
        private string _cartHeader;
        private bool _showCartBlock;   // owns Cart Whisperer → show the cart bundle-sense block
        private string _cartEmptyNote; // shown in the icon-row area when no relevant items today

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

        public ShrinePreviewMenu(MetaState state)
            : base(0, 0, 0, 0, showUpperRightCloseButton: true)
        {
            _state = state;
            BuildRows();
            BuildForesight();
            RecomputeBoundsAndLayout();
        }

        /// <summary>Fetch the foresight data (Weather Sage forecast + Cart Whisperer stock) from the
        /// owned tiers and the live game date. Slot 0 is always tomorrow, so the panel rolls forward
        /// each day. Empty blocks when the relevant tier isn't owned.</summary>
        private void BuildForesight()
        {
            int weatherTier = _state.HighestKeptTier("weather_sage_", 6);
            _weatherDays = weatherTier > 0
                ? WeatherForecast.Build(
                    (int)Game1.uniqueIDForThisGame, (int)Game1.stats.DaysPlayed,
                    Game1.dayOfMonth, (int)Game1.season, weatherTier,
                    Loop.GreenRainDay.VanillaSummerDay())
                : System.Array.Empty<ForecastDay>();

            // Cart Whisperer (single upgrade): on a cart day — or any day if the Cart Catalog mod lets
            // you mail-order — flag which of the cart's REAL current stock can feed any CC bundle.
            // Requires BOTH the upgrade AND the Cart Catalog mod installed+enabled (user decision
            // 2026-06-08): without Cart Catalog the preview block is hidden entirely.
            _cartItems.Clear();
            _cartHeader = null;
            _cartEmptyNote = null;
            _showCartBlock = _state.HasUpgrade("cart_whisper_1")
                && CartCatalogIntegration.Available(Game1.player);
            if (_showCartBlock)
            {
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
                catch (System.Exception)
                {
                    _cartItems.Clear();
                }

                _cartHeader = (catalogAnyDay && !cartInTown)
                    ? Strings.Get("menu.shrine-preview.cart-catalog-header")
                    : Strings.Get("menu.shrine-preview.cart-traveling-header");
                if (_cartItems.Count == 0)
                    _cartEmptyNote = Strings.Get("menu.shrine-preview.cart-nothing");
            }
        }

        /// <summary>The Traveling Cart is in town on days where <c>dayOfMonth % 7 % 5 == 0</c>
        /// (Fri 5/12/19/26, Sun 7/14/21/28).</summary>
        private static bool TravelingCartVisitsToday(int dayOfMonth) => dayOfMonth % 7 % 5 == 0;

        // Day-of-month IS day-of-season in vanilla's terms, so the vanilla helper (loaded from
        // the game's own localized Strings\StringsFromCSFiles asset) produces the identical
        // abbreviated weekday names ("Sun".."Sat") this menu previously hardcoded in English.
        private static string ShortDayName(int dayOfMonth) => Game1.shortDayDisplayNameFromDayOfSeason(dayOfMonth);

        /// <summary>Day-of-month of the next Traveling Cart visit strictly after <paramref name="today"/>,
        /// wrapping across the 28-day month. The cart visits on days where <c>dayOfMonth % 7 % 5 == 0</c>.</summary>
        private static int NextCartVisitDay(int today)
        {
            for (int off = 1; off <= WeatherScheduler.DaysPerMonth; off++)
            {
                int dom = ((today - 1 + off) % WeatherScheduler.DaysPerMonth) + 1;
                if (dom % 7 % 5 == 0)
                    return dom;
            }
            return today; // unreachable — a visit day always exists within 28 days.
        }

        // Icon + label lookups live in the shared WeatherIcons helper (one copy for both menus).

        private int ForesightPanelHeight()
        {
            int h = 0;
            if (_weatherDays.Length > 0)
                h += WeatherHeaderH + WeatherNumberRowH + WeatherIconRowH + ForesightBlockGap;
            if (_showCartBlock)
                h += CartHeaderH + CartIconRowH + ForesightBlockGap;
            return h;
        }

        /// <summary>Snapshot the buyable list (reach read live, at open time): for each category,
        /// a header followed by its next-purchasable tiers. Owned lower tiers are never listed.</summary>
        private void BuildRows()
        {
            _rows.Clear();
            foreach (UpgradeCategory cat in System.Enum.GetValues(typeof(UpgradeCategory)))
            {
                IReadOnlyList<UpgradeDefinition> owned =
                    KeepShopFilter.OwnedLeavesInCategory(cat, _state, RunReachEvaluator.Meets);
                IReadOnlyList<UpgradeDefinition> buyable =
                    KeepShopFilter.BuyableInCategory(cat, _state, RunReachEvaluator.Meets);
                if (owned.Count == 0 && buyable.Count == 0)
                    continue;

                _rows.Add(new Row { IsHeader = true, Text = ThemeDisplay.CategoryName(cat) });

                // Owned leaves first (green, no cost) — "you already have this, nothing more to buy".
                foreach (UpgradeDefinition def in owned)
                    _rows.Add(new Row
                    {
                        Def = def,
                        IsOwned = true,
                        Tooltip = Strings.Get("menu.shrine-preview.tooltip-owned",
                            new Dictionary<string, string> { ["description"] = def.Description }),
                    });

                // Then the next-purchasable tiers (with cost).
                foreach (UpgradeDefinition def in buyable)
                    _rows.Add(new Row
                    {
                        Def = def,
                        Tooltip = Strings.Get("menu.shrine-preview.tooltip-buyable", new Dictionary<string, string>
                        {
                            ["description"] = def.Description,
                            ["owned"] = OwnedLabel(def),
                        }),
                    });
            }
        }

        /// <summary>What the player already owns in this chain — the buyable tier's prerequisite is,
        /// by definition, the highest owned tier (KeepShopFilter only offers a tier whose prereq is
        /// owned). "none" when the buyable tier is the chain root.</summary>
        private static string OwnedLabel(UpgradeDefinition def)
        {
            if (def.PrerequisiteId == null)
                return Strings.Get("menu.shrine-preview.owned-none");
            return UpgradeCatalog.TryGet(def.PrerequisiteId)?.DisplayName ?? def.PrerequisiteId;
        }

        public override void gameWindowSizeChanged(Rectangle oldBounds, Rectangle newBounds)
        {
            base.gameWindowSizeChanged(oldBounds, newBounds);
            RecomputeBoundsAndLayout();
        }

        private void RecomputeBoundsAndLayout()
        {
            // 50% larger than the original 840x680 board (capped to the viewport).
            width = System.Math.Min(1260, Game1.uiViewport.Width - 64);
            height = System.Math.Min(1020, Game1.uiViewport.Height - 64);
            xPositionOnScreen = (Game1.uiViewport.Width - width) / 2;
            yPositionOnScreen = (Game1.uiViewport.Height - height) / 2;

            _listX = xPositionOnScreen + 40;
            _listWidth = width - 80;

            LayoutForesight();

            // List sits below the foresight panel (or just below the JP line when no tier is owned).
            _listY = yPositionOnScreen + ForesightTop + ForesightPanelHeight();
            int listHeight = height - (_listY - yPositionOnScreen) - 40;
            _rowsPerPage = System.Math.Max(1, listHeight / RowHeight);

            _rowSlots.Clear();
            for (int i = 0; i < _rowsPerPage; i++)
                _rowSlots.Add(new ClickableComponent(
                    new Rectangle(_listX, _listY + i * RowHeight, _listWidth - 56, RowHeight),
                    "row-" + i) { myID = RowIdBase + i });

            int arrowX = _listX + _listWidth - 48;
            _scrollUp = new ClickableTextureComponent("scroll-up",
                new Rectangle(arrowX, _listY, 44, 48), null, null,
                Game1.mouseCursors, new Rectangle(421, 459, 11, 12), 4f) { myID = ScrollUpId };
            _scrollDown = new ClickableTextureComponent("scroll-down",
                new Rectangle(arrowX, _listY + listHeight - 48, 44, 48), null, null,
                Game1.mouseCursors, new Rectangle(421, 472, 11, 12), 4f) { myID = ScrollDownId };

            this.initializeUpperRightCloseButton();

            allClickableComponents = new List<ClickableComponent> { _scrollUp, _scrollDown };
            allClickableComponents.AddRange(_rowSlots);
            if (upperRightCloseButton != null)
                allClickableComponents.Add(upperRightCloseButton);

            ClampScroll();
        }

        /// <summary>Position the weather + cart cells for the current window. Header Y values and the
        /// per-cell hover/draw bounds are stored for both <see cref="draw"/> and hover hit-testing.</summary>
        private void LayoutForesight()
        {
            _weatherCells.Clear();
            _cartCells.Clear();
            _weatherHeaderY = -1;
            _cartHeaderY = -1;

            int fy = yPositionOnScreen + ForesightTop;

            if (_weatherDays.Length > 0)
            {
                _weatherHeaderY = fy;
                int numY = fy + WeatherHeaderH;
                for (int i = 0; i < _weatherDays.Length; i++)
                {
                    int cellX = _listX + i * WeatherCellWidth;
                    // Hover region spans the number + icon rows of the column.
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

        private int MaxScroll() => System.Math.Max(0, _rows.Count - _rowsPerPage);

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

        public override void receiveScrollWheelAction(int direction)
        {
            base.receiveScrollWheelAction(direction);
            Scroll(direction > 0 ? -1 : +1);
        }

        public override void receiveLeftClick(int x, int y, bool playSound = true)
        {
            base.receiveLeftClick(x, y, playSound);   // handles the close button
            if (_scrollUp.containsPoint(x, y)) { Scroll(-1); return; }
            if (_scrollDown.containsPoint(x, y)) { Scroll(+1); return; }
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
                if (!_rows[idx].IsHeader && _rowSlots[i].containsPoint(x, y))
                {
                    _hoverText = _rows[idx].Tooltip;
                    return;
                }
            }
        }

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

            // Multiple testers tried to buy from this board and were confused when nothing happened
            // (it's a read-only preview — JP is spent only at a loop boundary). Spell that out,
            // right-aligned on the JP line, so the "planning, not a shop" intent is obvious.
            string planningNote = Strings.Get("menu.shrine-preview.planning-note");
            Vector2 noteSize = Game1.smallFont.MeasureString(planningNote);
            Utility.drawTextWithShadow(b, planningNote, Game1.smallFont,
                new Vector2(xPositionOnScreen + width - 40 - noteSize.X, yPositionOnScreen + 80),
                new Color(120, 90, 40));

            DrawForesight(b);

            if (_rows.Count == 0)
            {
                Utility.drawTextWithShadow(b, Strings.Get("menu.shrine-preview.nothing-new"),
                    Game1.smallFont, new Vector2(_listX, _listY), Game1.textColor);
            }

            for (int i = 0; i < _rowsPerPage; i++)
            {
                int idx = _scrollIndex + i;
                if (idx >= _rows.Count) break;
                Row row = _rows[idx];
                int rowY = _listY + i * RowHeight;
                if (row.IsHeader)
                {
                    Utility.drawTextWithShadow(b, row.Text, Game1.dialogueFont,
                        new Vector2(_listX, rowY + 6), Game1.textColor);
                }
                else if (row.IsOwned)
                {
                    // Owned leaf: name + "Owned" on the right, both green, no cost.
                    Color green = new Color(30, 130, 30);
                    Utility.drawTextWithShadow(b, row.Def.DisplayName, Game1.smallFont,
                        new Vector2(_listX + 24, rowY + 6), green);
                    string ownedLabel = Strings.Get("menu.shrine.owned");
                    Vector2 ownedSize = Game1.smallFont.MeasureString(ownedLabel);
                    Utility.drawTextWithShadow(b, ownedLabel, Game1.smallFont,
                        new Vector2(_listX + _listWidth - 64 - ownedSize.X, rowY + 6), green);
                }
                else
                {
                    bool affordable = _state.JunimoPoints >= row.Def.Cost;
                    Utility.drawTextWithShadow(b, row.Def.DisplayName, Game1.smallFont,
                        new Vector2(_listX + 24, rowY + 6), Game1.textColor);
                    string cost = Strings.Get("menu.shrine-preview.cost",
                        new Dictionary<string, string> { ["cost"] = row.Def.Cost.ToString() });
                    Vector2 costSize = Game1.smallFont.MeasureString(cost);
                    Utility.drawTextWithShadow(b, cost, Game1.smallFont,
                        new Vector2(_listX + _listWidth - 64 - costSize.X, rowY + 6),
                        affordable ? Game1.textColor : Color.Brown);
                }
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

        /// <summary>Draw the calendar-style foresight panel: weather number+icon rows and the cart's
        /// hoverable item-icon row.</summary>
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
                    // Faint calendar cell behind the number + icon (2px inset for gaps between days).
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

            if (_showCartBlock)
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

        /// <summary>A faint filled cell with a thin border, drawn from the 1×1 white pixel
        /// (<c>Game1.staminaRect</c>) — the calendar-grid backing for a weather column.</summary>
        private static void DrawCell(SpriteBatch b, Rectangle r)
        {
            Color fill = Color.SaddleBrown * 0.10f;
            Color border = Color.SaddleBrown * 0.40f;
            b.Draw(Game1.staminaRect, r, fill);
            b.Draw(Game1.staminaRect, new Rectangle(r.X, r.Y, r.Width, 2), border);            // top
            b.Draw(Game1.staminaRect, new Rectangle(r.X, r.Bottom - 2, r.Width, 2), border);   // bottom
            b.Draw(Game1.staminaRect, new Rectangle(r.X, r.Y, 2, r.Height), border);           // left
            b.Draw(Game1.staminaRect, new Rectangle(r.Right - 2, r.Y, 2, r.Height), border);   // right
        }
    }
}
