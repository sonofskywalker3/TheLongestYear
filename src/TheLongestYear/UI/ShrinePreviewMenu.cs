using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.BellsAndWhistles;
using StardewValley.Menus;
using TheLongestYear.Core;
using TheLongestYear.Integration;

namespace TheLongestYear.UI
{
    /// <summary>Read-only "what could I buy next reset" planning board. No purchasing — that stays
    /// on the loop-boundary shrine popup. Shows, for every category, only the next purchasable tier
    /// of each chain (reach-gated, owned tiers hidden — seeing "Cookbook II" means you already own
    /// I). Each row shows the cost; hovering shows the effect + what you currently own in that chain.
    /// Fully scrollable so the whole buyable list is visible.</summary>
    internal sealed class ShrinePreviewMenu : IClickableMenu
    {
        private const int RowHeight = 56;
        private const int RowIdBase = 7000;
        private const int ScrollUpId = 7900;
        private const int ScrollDownId = 7901;

        // Pinned foresight panel (Weather Sage + Cart Whisperer), drawn above the scrolling list.
        private const int ForesightLineHeight = 30;
        private const int ForesightBottomGap = 16;

        private sealed class Row
        {
            public bool IsHeader;
            public string Text;                  // header label (when IsHeader)
            public UpgradeDefinition Def;        // the buyable upgrade (when !IsHeader)
            public string Tooltip;               // precomputed hover text (when !IsHeader)
        }

        private readonly MetaState _state;
        private readonly List<Row> _rows = new();

        // Foresight lines computed at open time (rolling): each is a label + whether it's a
        // section header. Reads live Game1 state so the shrine always shows "from tomorrow".
        private readonly List<(string Text, bool IsHeader)> _foresightLines = new();

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

        /// <summary>Compute the pinned foresight panel (Weather Sage + Cart Whisperer) from the
        /// owned tiers and live game date. Rolling: slot 0 is always tomorrow, so re-opening the
        /// shrine on a later day shows a later window. Empty when no foresight tier is owned.</summary>
        private void BuildForesight()
        {
            _foresightLines.Clear();

            int weatherTier = _state.HighestKeptTier("weather_sage_", 6);
            if (weatherTier > 0)
            {
                ForecastDay[] forecast = WeatherForecast.Build(
                    (int)Game1.uniqueIDForThisGame, (int)Game1.stats.DaysPlayed,
                    Game1.dayOfMonth, (int)Game1.season, weatherTier);
                _foresightLines.Add(("Weather", true));
                foreach (ForecastDay fd in forecast)
                    _foresightLines.Add(($"Day {fd.DayOfMonth}: {fd.Weather}", false));
            }

            int cartSlots = CartStockPreview.SlotsToReveal(_state.HighestKeptTier("cart_whisper_", 3));
            if (cartSlots > 0)
            {
                var names = new List<string>();
                try
                {
                    var stock = StardewValley.Internal.ShopBuilder.GetShopStock("Traveler");
                    int taken = 0;
                    foreach (var pair in stock)
                    {
                        if (taken >= cartSlots) break;
                        names.Add(pair.Key?.DisplayName ?? "?");
                        taken++;
                    }
                }
                catch (System.Exception)
                {
                    // Preview only — if the cart stock can't be built, just omit the cart block.
                }

                if (names.Count > 0)
                {
                    _foresightLines.Add(($"Cart (next visit: Day {NextCartVisitDay(Game1.dayOfMonth)})", true));
                    foreach (string n in names)
                        _foresightLines.Add((n, false));
                }
            }
        }

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

        private int ForesightPanelHeight()
            => _foresightLines.Count == 0 ? 0 : _foresightLines.Count * ForesightLineHeight + ForesightBottomGap;

        /// <summary>Snapshot the buyable list (reach read live, at open time): for each category,
        /// a header followed by its next-purchasable tiers. Owned lower tiers are never listed.</summary>
        private void BuildRows()
        {
            _rows.Clear();
            foreach (UpgradeCategory cat in System.Enum.GetValues(typeof(UpgradeCategory)))
            {
                IReadOnlyList<UpgradeDefinition> buyable =
                    KeepShopFilter.BuyableInCategory(cat, _state, RunReachEvaluator.Meets);
                if (buyable.Count == 0)
                    continue;
                _rows.Add(new Row { IsHeader = true, Text = cat.ToString() });
                foreach (UpgradeDefinition def in buyable)
                    _rows.Add(new Row
                    {
                        Def = def,
                        Tooltip = $"{def.Description}\nCurrently owned: {OwnedLabel(def)}",
                    });
            }
        }

        /// <summary>What the player already owns in this chain — the buyable tier's prerequisite is,
        /// by definition, the highest owned tier (KeepShopFilter only offers a tier whose prereq is
        /// owned). "none" when the buyable tier is the chain root.</summary>
        private static string OwnedLabel(UpgradeDefinition def)
        {
            if (def.PrerequisiteId == null)
                return "none";
            return UpgradeCatalog.TryGet(def.PrerequisiteId)?.DisplayName ?? def.PrerequisiteId;
        }

        public override void gameWindowSizeChanged(Rectangle oldBounds, Rectangle newBounds)
        {
            base.gameWindowSizeChanged(oldBounds, newBounds);
            RecomputeBoundsAndLayout();
        }

        private void RecomputeBoundsAndLayout()
        {
            width = System.Math.Min(840, Game1.uiViewport.Width - 64);
            height = System.Math.Min(680, Game1.uiViewport.Height - 64);
            xPositionOnScreen = (Game1.uiViewport.Width - width) / 2;
            yPositionOnScreen = (Game1.uiViewport.Height - height) / 2;

            _listX = xPositionOnScreen + 40;
            // Below title + JP line, then below the pinned foresight panel (0 when no tier owned).
            _listY = yPositionOnScreen + 112 + ForesightPanelHeight();
            _listWidth = width - 80;
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

            SpriteText.drawStringHorizontallyCenteredAt(b, "Junimo Shrine - Planning",
                xPositionOnScreen + width / 2, yPositionOnScreen + 24);
            Utility.drawTextWithShadow(b, $"Junimo Points banked: {_state.JunimoPoints}", Game1.smallFont,
                new Vector2(xPositionOnScreen + 40, yPositionOnScreen + 80), Game1.textColor);

            // Pinned foresight panel (above the scrolling upgrade list).
            int foresightY = yPositionOnScreen + 112;
            foreach (var (text, isHeader) in _foresightLines)
            {
                if (isHeader)
                    Utility.drawTextWithShadow(b, text, Game1.dialogueFont,
                        new Vector2(_listX, foresightY), Game1.textColor);
                else
                    Utility.drawTextWithShadow(b, text, Game1.smallFont,
                        new Vector2(_listX + 24, foresightY + 4), Game1.textColor);
                foresightY += ForesightLineHeight;
            }

            if (_rows.Count == 0)
            {
                Utility.drawTextWithShadow(b, "Nothing new to plan for yet — reach further this run!",
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
                else
                {
                    bool affordable = _state.JunimoPoints >= row.Def.Cost;
                    Utility.drawTextWithShadow(b, row.Def.DisplayName, Game1.smallFont,
                        new Vector2(_listX + 24, rowY + 6), Game1.textColor);
                    string cost = $"{row.Def.Cost} JP";
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
    }
}
