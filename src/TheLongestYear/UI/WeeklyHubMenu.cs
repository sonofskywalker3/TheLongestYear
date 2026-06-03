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
using TheLongestYear.Loop;
using CoreSeason = TheLongestYear.Core.Season;

namespace TheLongestYear.UI
{
    /// <summary>
    /// The weekly planning hub. Two cards (selection-offer themes) each showing theme name,
    /// bonus + liability lines, and the per-week bonus-item preview that picking would activate.
    /// Bundle progress lives in <see cref="SeasonGoalsMenu"/> instead — the hub is the selection
    /// decision surface; goal tracking is a separate menu the player opens at will (UX2).
    /// Opened automatically on Sunday-night DayEnding; modal (no close until a theme is picked).
    ///
    /// Plan 06+ will add weather + cart foresight rows (currently hidden via config); the layout
    /// already reserves vertical space.
    /// </summary>
    internal sealed class WeeklyHubMenu : IClickableMenu
    {
        // ---------- Card dimensions ----------
        // Trimmed 2026-05-28 per playtest UI polish — the prior 560x360 left a lot of empty
        // whitespace between the bonus/liability lines and the icon row. Tighter sizing also
        // gets the panel onto smaller monitors without scrollbars.
        private const int CardWidth = 480;
        private const int CardHeight = 300;
        private const int CardSpacing = 24;
        private const int CardInnerPad = 20;
        private const int PanelPadding = 32;

        // ---------- Inner card layout ----------
        private const int ThemeNameLineHeight = 44;
        private const int BodyLineHeight = 26;
        private const int SectionGap = 8;

        // ---------- Bonus item icons ----------
        // Smaller than the v1 icon grid — bonus row needs to fit up to 7 icons in CardWidth-pad.
        private const float BonusIconScale = 0.75f;
        private const int BonusIconSize = 48;
        private const int BonusIconGap = 10;

        // ---------- Preview rows (foresight, Plan 06) ----------
        private const int PreviewRowHeight = 44;
        private const int PreviewSpacing = 8;
        private const int JunimoSpriteSize = 96;

        // ---------- Weather foresight calendar (mirrors ShrinePreviewMenu's board) ----------
        private const int WeatherCellWidth = 64;
        private const float WeatherIconScale = 3f;   // 13px source → 39px
        private const int WeatherIconPx = 39;
        private const int WeatherHeaderH = 60;        // header text + breathing room before the cells
        private const int WeatherNumberRowH = 30;
        private const int WeatherIconRowH = 52;
        private int _weatherBlockX;
        private int _weatherBlockY = -1;   // top of the weather calendar block (-1 when no weather)

        // ---------- Component IDs ----------
        private const int CardIdLeft = 5100;
        private const int CardIdRight = 5101;
        private const int WeatherIdBase = 5200;
        private const int CartIdBase = 5300;

        private readonly IMonitor _monitor;
        private readonly RunController _runController;
        private readonly GameplayConfig _config;
        private readonly RunState _run;
        private readonly IReadOnlyList<BundleRequirement> _requirements;

        /// <summary>Season the menu's bundle progress + bonus preview reflect. May be NEXT season
        /// (Sunday-night day-28 case) — see <see cref="_isPreSelectForNextMonth"/>.</summary>
        private readonly CoreSeason _offerSeason;

        /// <summary>True when this hub is for next-month's week 1 (player is pre-picking before
        /// sleeping on day 28). The pick routes through <see cref="RunController.PreSelectForNextMonth"/>
        /// rather than the normal current-week selection path.</summary>
        private readonly bool _isPreSelectForNextMonth;

        private IReadOnlyList<Theme> _offer;

        private ClickableComponent _leftCard;
        private ClickableComponent _rightCard;
        private ClickableComponent _rerollButton;
        private int _rerollCounter;
        private readonly List<ClickableComponent> _weatherRows = new List<ClickableComponent>();
        private readonly List<ClickableComponent> _cartRows = new List<ClickableComponent>();

        // ---------- Reroll debug button ----------
        private const int RerollButtonId = 5102;
        private const int RerollButtonWidth = 200;
        private const int RerollButtonHeight = 56;
        private const int RerollSaltPrime = 1399;

        private ForecastDay[] _weatherForecast;
        private System.Collections.Generic.List<StardewValley.ISalable> _cartItems;

        private readonly Texture2D _junimoTexture;

        // Per-card derived data (recomputed on construct + refresh).
        private List<Item> _leftBonus = new List<Item>();
        private List<Item> _rightBonus = new List<Item>();
        private readonly List<Rectangle> _leftBonusBounds = new List<Rectangle>();
        private readonly List<Rectangle> _rightBonusBounds = new List<Rectangle>();

        private string _hoverText = "";
        private bool _themePicked = false;
        private readonly int _weatherSageSlots;
        private readonly int _cartPreviewSlots;

        public WeeklyHubMenu(IMonitor monitor, RunController runController, GameplayConfig config,
            RunState run, IReadOnlyList<BundleRequirement> requirements, IReadOnlyList<Theme> offer,
            CoreSeason? offerSeason = null, bool isPreSelectForNextMonth = false,
            int weatherSageSlots = 0, int cartPreviewSlots = 0)
            : base(0, 0, 0, 0, showUpperRightCloseButton: false)
        {
            _monitor = monitor;
            _runController = runController;
            _config = config;
            _run = run;
            _requirements = requirements ?? new List<BundleRequirement>();
            _offer = offer ?? new List<Theme>();
            _offerSeason = offerSeason ?? run.Season;
            _isPreSelectForNextMonth = isPreSelectForNextMonth;
            _weatherSageSlots = weatherSageSlots;
            _cartPreviewSlots = cartPreviewSlots;

            try { _junimoTexture = Game1.content.Load<Texture2D>("Characters\\Junimo"); }
            catch (Exception) { _junimoTexture = null; }

            _weatherForecast = _weatherSageSlots > 0
                ? WeatherForecast.Build(
                    (int)Game1.uniqueIDForThisGame,
                    (int)Game1.stats.DaysPlayed,
                    Game1.dayOfMonth,
                    (int)Game1.season,
                    _weatherSageSlots)
                : System.Array.Empty<ForecastDay>();

            _cartItems = new System.Collections.Generic.List<StardewValley.ISalable>();
            if (_cartPreviewSlots > 0)
            {
                try
                {
                    var stock = StardewValley.Internal.ShopBuilder.GetShopStock("Traveler");
                    int taken = 0;
                    foreach (var pair in stock)
                    {
                        if (taken >= _cartPreviewSlots) break;
                        _cartItems.Add(pair.Key);
                        taken++;
                    }
                }
                catch (Exception ex)
                {
                    _monitor.Log($"WeeklyHubMenu: failed to build cart preview: {ex.Message}", LogLevel.Warn);
                }
            }

            ResolvePerCardData();
            RecomputeBoundsAndLayout();

            // Always seed gamepad focus on the left card so the controller A path works even
            // when the player has snappyMenus = false (the PC default). The 2026-05-27 playtest
            // reported "controller can't pick between themes" — root cause was
            // currentlySnappedComponent == null on open, leaving receiveGamePadButton(A)
            // with nothing to confirm. Cursor warp is still gated on snappyMenus so we don't
            // hijack the mouse pointer of a non-snap player.
            currentlySnappedComponent = _leftCard;
            if (Game1.options.snappyMenus && Game1.options.gamepadControls)
                this.snapCursorToCurrentSnappedComponent();

            _monitor.Log(
                $"WeeklyHubMenu opened: gamepadControls={Game1.options.gamepadControls}, " +
                $"snappyMenus={Game1.options.snappyMenus}, " +
                $"defaultSnap=leftCard.",
                LogLevel.Trace);
        }

        // ---------- modal guard ----------

        public override bool readyToClose() => _themePicked;

        public override void receiveKeyPress(Microsoft.Xna.Framework.Input.Keys key)
        {
            if (!_themePicked && key == Microsoft.Xna.Framework.Input.Keys.Escape) return;
            base.receiveKeyPress(key);
        }

        public override void receiveGamePadButton(Microsoft.Xna.Framework.Input.Buttons b)
        {
            var btn = b;
            if (btn == Microsoft.Xna.Framework.Input.Buttons.A && currentlySnappedComponent != null)
            {
                if (currentlySnappedComponent == _leftCard && _offer.Count > 0) { ConfirmSelection(_offer[0]); return; }
                if (currentlySnappedComponent == _rightCard && _offer.Count > 1) { ConfirmSelection(_offer[1]); return; }
            }
            if (btn == Microsoft.Xna.Framework.Input.Buttons.B && !_themePicked) return;

            // Drive snap navigation directly from DPad / left-thumbstick events. Vanilla's
            // IClickableMenu.receiveKeyPress only routes to applyMovementKey when
            // snappyMenus = true, but we ALWAYS seed currentlySnappedComponent (so A picks
            // works regardless of the snappy toggle) and want directional input to follow
            // suit. 2026-05-28 playtest: "finger cursor is stuck on the left option and
            // won't move, though A will select it" — root cause was the snappy gate.
            // applyMovementKey directions: 0=up, 1=right, 2=down, 3=left.
            switch (btn)
            {
                case Microsoft.Xna.Framework.Input.Buttons.DPadLeft:
                case Microsoft.Xna.Framework.Input.Buttons.LeftThumbstickLeft:
                    this.applyMovementKey(3);
                    return;
                case Microsoft.Xna.Framework.Input.Buttons.DPadRight:
                case Microsoft.Xna.Framework.Input.Buttons.LeftThumbstickRight:
                    this.applyMovementKey(1);
                    return;
                case Microsoft.Xna.Framework.Input.Buttons.DPadUp:
                case Microsoft.Xna.Framework.Input.Buttons.LeftThumbstickUp:
                    this.applyMovementKey(0);
                    return;
                case Microsoft.Xna.Framework.Input.Buttons.DPadDown:
                case Microsoft.Xna.Framework.Input.Buttons.LeftThumbstickDown:
                    this.applyMovementKey(2);
                    return;
            }

            base.receiveGamePadButton(b);
        }

        /// <summary>
        /// Block vanilla's reflection-driven repopulation of <c>allClickableComponents</c>.
        /// The base implementation uses <c>GetType().GetFields()</c> (public-only by default)
        /// to discover ClickableComponent fields; our <c>_leftCard</c> / <c>_rightCard</c> are
        /// private, so the reflection finds nothing and wipes the list we built manually in
        /// <see cref="RecomputeBoundsAndLayout"/>. Empty allClickableComponents = no snap nav,
        /// which is exactly the 2026-05-28 "DPad stuck on left card" bug.
        /// </summary>
        public override void populateClickableComponentList()
        {
            // Intentionally no-op. RecomputeBoundsAndLayout populates allClickableComponents
            // with the cards + preview rows in the correct order, and the contents are stable
            // until the next window-resize (which calls RecomputeBoundsAndLayout again).
        }

        // ---------- per-card data ----------

        /// <summary>Resolve the bonus-item preview for each card's theme from the current offer.</summary>
        private void ResolvePerCardData()
        {
            ResolveBonusItemsForTheme(_offer.Count > 0 ? (Theme?)_offer[0] : null, _leftBonus);
            ResolveBonusItemsForTheme(_offer.Count > 1 ? (Theme?)_offer[1] : null, _rightBonus);
        }

        private void ResolveBonusItemsForTheme(Theme? theme, List<Item> dest)
        {
            dest.Clear();
            if (theme == null) return;

            int maxCount = _runController.BonusListSizeForCurrentSeason();
            // Sample for the OFFER's season (which is next-season on day 28's Sunday-night hub).
            int week = _isPreSelectForNextMonth ? _run.WeekOfYear + 1 : _run.WeekOfYear;
            IReadOnlyList<string> sample = BonusItemSampler.SampleForTheme(
                _run.Seed, week,
                theme.Value, _offerSeason,
                _requirements,
                id => _runController.IsObtainableInSeason(id, _offerSeason),
                _runController.GetRarityForItem,
                maxCount);

            foreach (string id in sample)
            {
                int stack = _runController.GetStackForIngredient(id);
                // 2026-05-29 user playtest: Quality Crops bundle's Parsnips were rendering
                // without a gold star on the week-2 farming card. Pull the per-id MAX quality
                // (0=basic, 1=silver, 2=gold, 4=iridium) so ItemRegistry.Create stamps the
                // correct quality badge on the bonus icon.
                int quality = _runController.GetQualityForIngredient(id);
                Item item = null;
                try { item = ItemRegistry.Create(id, stack, quality, allowNull: true); }
                catch (Exception) { item = null; }
                dest.Add(item);
            }
        }

        // ---------- layout ----------

        public override void gameWindowSizeChanged(Rectangle oldBounds, Rectangle newBounds)
        {
            base.gameWindowSizeChanged(oldBounds, newBounds);
            RecomputeBoundsAndLayout();
        }

        private void RecomputeBoundsAndLayout()
        {
            // Weather now reserves a fixed calendar strip (header + number row + icon row); cart
            // (if ever shown) still uses stacked text rows below it.
            int weatherBlockH = _weatherSageSlots > 0 ? (WeatherHeaderH + WeatherNumberRowH + WeatherIconRowH) : 0;
            int cartRowsH = _cartPreviewSlots > 0
                ? (_cartPreviewSlots * PreviewRowHeight) + ((_cartPreviewSlots - 1) * PreviewSpacing)
                : 0;
            int previewBlock = (weatherBlockH > 0 || cartRowsH > 0)
                ? weatherBlockH + (weatherBlockH > 0 && cartRowsH > 0 ? PreviewSpacing : 0) + cartRowsH + PanelPadding
                : 0;

            int titleBlock = 24 + (_junimoTexture != null ? JunimoSpriteSize + 12 : 0) + 48 + 20;

            width = (CardWidth * 2) + CardSpacing + (PanelPadding * 2);
            // Reserve space for the reroll debug button row below preview rows / cards — only when
            // the button is enabled (config.EnableThemeReroll, off by default).
            int rerollBlock = _config.EnableThemeReroll ? RerollButtonHeight + 24 : 0;
            height = titleBlock + CardHeight + previewBlock + rerollBlock + PanelPadding;

            xPositionOnScreen = (Game1.uiViewport.Width - width) / 2;
            yPositionOnScreen = (Game1.uiViewport.Height - height) / 2;

            int cardsY = yPositionOnScreen + titleBlock;
            int cardsLeftX = xPositionOnScreen + PanelPadding;
            int cardsRightX = cardsLeftX + CardWidth + CardSpacing;

            _leftCard = new ClickableComponent(new Rectangle(cardsLeftX, cardsY, CardWidth, CardHeight),
                _offer.Count > 0 ? _offer[0].ToString() : "left-card")
            {
                myID = CardIdLeft,
                rightNeighborID = CardIdRight,
                downNeighborID = FirstRowIdBelowCards() != -1 ? FirstRowIdBelowCards() : (_config.EnableThemeReroll ? RerollButtonId : -1)
            };
            _rightCard = new ClickableComponent(new Rectangle(cardsRightX, cardsY, CardWidth, CardHeight),
                _offer.Count > 1 ? _offer[1].ToString() : "right-card")
            {
                myID = CardIdRight,
                leftNeighborID = CardIdLeft,
                downNeighborID = FirstRowIdBelowCards() != -1 ? FirstRowIdBelowCards() : (_config.EnableThemeReroll ? RerollButtonId : -1)
            };

            _weatherRows.Clear();
            _cartRows.Clear();

            int rowX = xPositionOnScreen + PanelPadding;
            int rowWidth = width - (PanelPadding * 2);
            int rowY = cardsY + CardHeight + PanelPadding;

            // Weather is now a non-interactive calendar block (day-number row + icon row), matching
            // the planning-shrine board — not stacked text rows. Reserve its strip, then cart rows
            // (if any) flow below it.
            _weatherBlockX = rowX;
            if (_weatherSageSlots > 0)
            {
                _weatherBlockY = rowY;
                rowY += WeatherHeaderH + WeatherNumberRowH + WeatherIconRowH + PreviewSpacing;
            }
            else
            {
                _weatherBlockY = -1;
            }

            for (int i = 0; i < _cartPreviewSlots; i++)
            {
                var row = new ClickableComponent(new Rectangle(rowX, rowY, rowWidth, PreviewRowHeight),
                    "cart-" + i)
                {
                    myID = CartIdBase + i,
                    upNeighborID = i == 0
                        ? (_weatherSageSlots > 0
                            ? (WeatherIdBase + _weatherSageSlots - 1)
                            : CardIdLeft)
                        : (CartIdBase + i - 1),
                    downNeighborID = i == _cartPreviewSlots - 1 ? -1 : (CartIdBase + i + 1)
                };
                _cartRows.Add(row);
                rowY += PreviewRowHeight + PreviewSpacing;
            }

            // Reroll debug button — centred horizontally, sits in the bottom strip of the
            // panel just above its border. Lets the playtester cycle through theme offers
            // without resetting the run. Not gameplay-balanced; QA-only, gated behind
            // config.EnableThemeReroll (off by default). When disabled it isn't built or added,
            // so receiveLeftClick / DrawRerollButton (both null-guarded) skip it entirely.
            if (_config.EnableThemeReroll)
            {
                int rerollX = xPositionOnScreen + (width - RerollButtonWidth) / 2;
                int rerollY = yPositionOnScreen + height - RerollButtonHeight - 16;
                _rerollButton = new ClickableComponent(
                    new Rectangle(rerollX, rerollY, RerollButtonWidth, RerollButtonHeight),
                    "reroll")
                {
                    myID = RerollButtonId,
                    upNeighborID = CardIdLeft,
                };
            }
            else
            {
                _rerollButton = null;
            }

            allClickableComponents = new List<ClickableComponent>();
            allClickableComponents.Add(_leftCard);
            allClickableComponents.Add(_rightCard);
            allClickableComponents.AddRange(_cartRows);
            if (_rerollButton != null)
                allClickableComponents.Add(_rerollButton);

            ComputeBonusIconBounds(_leftCard, _leftBonus.Count, _leftBonusBounds);
            ComputeBonusIconBounds(_rightCard, _rightBonus.Count, _rightBonusBounds);
        }

        /// <summary>Bonus icon row sits at the bottom of the card, centred horizontally.</summary>
        private void ComputeBonusIconBounds(ClickableComponent card, int count, List<Rectangle> bounds)
        {
            bounds.Clear();
            if (card == null || count == 0) return;

            int totalWidth = count * BonusIconSize + (count - 1) * BonusIconGap;
            int startX = card.bounds.X + (card.bounds.Width - totalWidth) / 2;
            // Extra bottom margin so the icon row isn't crammed against the card's frame.
            int y = card.bounds.Y + card.bounds.Height - (CardInnerPad + 18) - BonusIconSize;
            for (int i = 0; i < count; i++)
                bounds.Add(new Rectangle(startX + i * (BonusIconSize + BonusIconGap), y, BonusIconSize, BonusIconSize));
        }

        private int FirstRowIdBelowCards()
        {
            // Weather is a non-interactive calendar now; only cart rows (if any) are snap targets.
            if (_cartPreviewSlots > 0) return CartIdBase;
            return -1;
        }

        public override void snapToDefaultClickableComponent()
        {
            currentlySnappedComponent = _leftCard;
            this.snapCursorToCurrentSnappedComponent();
        }

        // ---------- input ----------

        public override void performHoverAction(int x, int y)
        {
            base.performHoverAction(x, y);
            _hoverText = "";

            // No card-hover tooltip — the bonus/liability text on the card itself is now
            // self-explanatory (2026-05-28 playtest: "remove the tooltip for the benefit
            // section, and just be very clear about what the benefit and drawback are").
            // Per-item bonus-icon tooltips still surface below so the player can see the
            // exact donation quantities.

            CheckBonusIconHover(x, y, _leftBonus, _leftBonusBounds);
            CheckBonusIconHover(x, y, _rightBonus, _rightBonusBounds);
        }

        private void CheckBonusIconHover(int x, int y, List<Item> items, List<Rectangle> bounds)
        {
            for (int i = 0; i < items.Count && i < bounds.Count; i++)
            {
                if (items[i] != null && bounds[i].Contains(x, y))
                {
                    // Show quantity in the hover so the player sees "Wood x99 (1.5x)" not just "Wood (1.5x)".
                    int hoverStack = items[i].Stack;
                    string qty = hoverStack > 1 ? $" x{hoverStack}" : "";
                    _hoverText = $"{items[i].DisplayName}{qty} (1.5x)";
                    return;
                }
            }
        }

        public override void receiveLeftClick(int x, int y, bool playSound = true)
        {
            base.receiveLeftClick(x, y, playSound);

            if (_rerollButton != null && _rerollButton.containsPoint(x, y))
            {
                RerollOffer();
                Game1.playSound("smallSelect");
                return;
            }
            if (_leftCard != null && _leftCard.containsPoint(x, y) && _offer.Count > 0)
                ConfirmSelection(_offer[0]);
            else if (_rightCard != null && _rightCard.containsPoint(x, y) && _offer.Count > 1)
                ConfirmSelection(_offer[1]);
        }

        /// <summary>
        /// Debug-only: regenerate the offer pool with fresh randomness so the playtester can
        /// cycle through theme combinations without resetting the run. Salt the underlying seed
        /// with an incrementing counter — keeps the offer deterministic-for-debug (same counter
        /// produces same offer) but moves the picker off the originally-rolled pair. Does NOT
        /// change <c>_run.Seed</c> or any persisted state; closing the menu without picking
        /// loses the rerolled offer (next open shows whatever <see cref="SelectionService"/>
        /// would have produced).
        /// </summary>
        private void RerollOffer()
        {
            _rerollCounter++;
            var selectedSet = new HashSet<Theme>(_run.SelectedThemesThisMonth);
            List<Theme> candidates = System.Enum.GetValues(typeof(Theme))
                .Cast<Theme>()
                .Where(t => !selectedSet.Contains(t))
                .OrderBy(t => (int)t)
                .ToList();

            int week = _isPreSelectForNextMonth ? _run.WeekOfYear + 1 : _run.WeekOfYear;
            var rng = new System.Random(_run.Seed ^ (week * 7919) ^ (_rerollCounter * RerollSaltPrime));
            for (int i = candidates.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
            }

            _offer = candidates.Take(SelectionService.OfferSize).ToList();
            ResolvePerCardData();
            RecomputeBoundsAndLayout();
            _monitor.Log(
                $"WeeklyHubMenu reroll #{_rerollCounter}: offer = [{string.Join(", ", _offer)}].",
                LogLevel.Info);
        }

        private void ConfirmSelection(Theme theme)
        {
            _themePicked = true;
            if (_isPreSelectForNextMonth)
                _runController.PreSelectForNextMonth(theme);
            else
                // skipOfferCheck whenever the menu has rerolled so picks off the rerolled
                // offer aren't rejected by RunController's canonical OfferForWeek validation.
                // The reroll path already excludes already-selected-this-month themes, so the
                // gameplay rule that matters is preserved.
                _runController.SelectByName(theme.ToString(), skipOfferCheck: _rerollCounter > 0);
            Game1.playSound("smallSelect");
            this.exitThisMenu();
        }

        // ---------- drawing ----------

        public override void draw(SpriteBatch b)
        {
            b.Draw(Game1.fadeToBlackRect, new Rectangle(0, 0, Game1.uiViewport.Width, Game1.uiViewport.Height),
                Color.Black * 0.5f);
            IClickableMenu.drawTextureBox(b, xPositionOnScreen, yPositionOnScreen, width, height, Color.White);

            int panelCenterX = xPositionOnScreen + width / 2;
            int drawY = yPositionOnScreen + 24;

            if (_junimoTexture != null)
            {
                b.Draw(_junimoTexture,
                    new Rectangle(panelCenterX - JunimoSpriteSize / 2, drawY, JunimoSpriteSize, JunimoSpriteSize),
                    new Rectangle(0, 0, 16, 16), Color.White);
                drawY += JunimoSpriteSize + 12;
            }

            SpriteText.drawStringHorizontallyCenteredAt(b, "Pick a theme", panelCenterX, drawY);

            DrawCard(b, _leftCard, _offer.Count > 0 ? (Theme?)_offer[0] : null, _leftBonus, _leftBonusBounds);
            DrawCard(b, _rightCard, _offer.Count > 1 ? (Theme?)_offer[1] : null, _rightBonus, _rightBonusBounds);

            DrawWeatherCalendar(b);
            for (int i = 0; i < _cartRows.Count; i++)
            {
                string label = (i < _cartItems.Count && _cartItems[i] != null)
                    ? _cartItems[i].DisplayName
                    : "?";
                DrawPreviewRow(b, _cartRows[i], $"Cart: {label}");
            }

            DrawRerollButton(b);

            base.draw(b);

            if (!string.IsNullOrEmpty(_hoverText))
                IClickableMenu.drawHoverText(b, _hoverText, Game1.smallFont);

            Game1.mouseCursorTransparency = 1f;
            this.drawMouse(b);
        }

        /// <summary>Render the playtester-only reroll button below the cards / preview rows.
        /// Plain texture-box + centred label; click rerolls the theme offer in place.</summary>
        private void DrawRerollButton(SpriteBatch b)
        {
            if (_rerollButton == null) return;

            IClickableMenu.drawTextureBox(b, Game1.menuTexture, new Rectangle(0, 256, 60, 60),
                _rerollButton.bounds.X, _rerollButton.bounds.Y,
                _rerollButton.bounds.Width, _rerollButton.bounds.Height,
                Color.White, 1f, false);

            string label = _rerollCounter == 0 ? "Re-roll Themes" : $"Re-roll ({_rerollCounter})";
            Vector2 size = Game1.smallFont.MeasureString(label);
            float labelX = _rerollButton.bounds.X + (_rerollButton.bounds.Width - size.X) / 2f;
            float labelY = _rerollButton.bounds.Y + (_rerollButton.bounds.Height - size.Y) / 2f;
            Utility.drawTextWithShadow(b, label, Game1.smallFont,
                new Vector2(labelX, labelY), Game1.textColor);
        }

        /// <summary>Draw the weather foresight as a calendar strip (a "Weather" header, a row of
        /// day-of-month numbers, then a row of HUD weather icons in faint cells) — the same look as
        /// the planning-shrine board, so the two surfaces are visually consistent.</summary>
        private void DrawWeatherCalendar(SpriteBatch b)
        {
            if (_weatherSageSlots <= 0 || _weatherBlockY < 0 || _weatherForecast.Length == 0)
                return;

            Utility.drawTextWithShadow(b, "Weather", Game1.dialogueFont,
                new Vector2(_weatherBlockX, _weatherBlockY), Game1.textColor);

            int numY = _weatherBlockY + WeatherHeaderH;
            int iconY = numY + WeatherNumberRowH;
            for (int i = 0; i < _weatherForecast.Length; i++)
            {
                int cellX = _weatherBlockX + i * WeatherCellWidth;
                DrawWeatherCell(b, new Rectangle(cellX + 2, numY, WeatherCellWidth - 4, WeatherNumberRowH + WeatherIconRowH));

                string num = _weatherForecast[i].DayOfMonth.ToString();
                Vector2 ns = Game1.smallFont.MeasureString(num);
                Utility.drawTextWithShadow(b, num, Game1.smallFont,
                    new Vector2(cellX + (WeatherCellWidth - ns.X) / 2f, numY), Game1.textColor);

                Rectangle src = WeatherIconSource(_weatherForecast[i].Weather);
                float iconX = cellX + (WeatherCellWidth - WeatherIconPx) / 2f;
                b.Draw(Game1.mouseCursors, new Vector2(iconX, iconY), src, Color.White, 0f,
                    Vector2.Zero, WeatherIconScale, SpriteEffects.None, 0.9f);
            }
        }

        /// <summary>Weather-icon source rect in <c>Game1.mouseCursors</c>, matching the TV/HUD icons
        /// (same mapping as ShrinePreviewMenu). Unknown/Sun falls through to the sunny icon.</summary>
        private static Rectangle WeatherIconSource(string weather) => weather switch
        {
            "Rain" => new Rectangle(465, 333, 13, 13),
            "Storm" => new Rectangle(413, 346, 13, 13),
            "Snow" => new Rectangle(465, 346, 13, 13),
            "Festival" => new Rectangle(413, 372, 13, 13),
            _ => new Rectangle(413, 333, 13, 13), // Sun / default
        };

        /// <summary>A faint filled cell with a thin border (the calendar-grid backing for a weather
        /// column), drawn from the 1×1 white pixel — same styling as the shrine board.</summary>
        private static void DrawWeatherCell(SpriteBatch b, Rectangle r)
        {
            Color fill = Color.SaddleBrown * 0.10f;
            Color border = Color.SaddleBrown * 0.40f;
            b.Draw(Game1.staminaRect, r, fill);
            b.Draw(Game1.staminaRect, new Rectangle(r.X, r.Y, r.Width, 2), border);
            b.Draw(Game1.staminaRect, new Rectangle(r.X, r.Bottom - 2, r.Width, 2), border);
            b.Draw(Game1.staminaRect, new Rectangle(r.X, r.Y, 2, r.Height), border);
            b.Draw(Game1.staminaRect, new Rectangle(r.Right - 2, r.Y, 2, r.Height), border);
        }

        private void DrawCard(SpriteBatch b, ClickableComponent card, Theme? theme,
            List<Item> bonus, List<Rectangle> bonusBounds)
        {
            if (card == null) return;

            // 2026-05-28 playtest: "don't need the yellow highlight on the picker, the cursor
            // is plenty." Both cards now render with the same plain white tint — the snappy-mode
            // finger cursor already shows the player which card has focus.
            IClickableMenu.drawTextureBox(b, Game1.menuTexture, new Rectangle(0, 256, 60, 60),
                card.bounds.X, card.bounds.Y, card.bounds.Width, card.bounds.Height,
                Color.White, 1f, false);

            if (theme == null)
            {
                Utility.drawTextWithShadow(b, "(no offer)", Game1.smallFont,
                    new Vector2(card.bounds.X + 24, card.bounds.Y + 24), Game1.textColor);
                return;
            }

            var (bonusMod, liabilityMod) = ThemeModifiers.For(theme.Value);
            string bonusName = ThemeModifiers.DisplayNameFor(bonusMod);
            string liabilityName = ThemeModifiers.DisplayNameFor(liabilityMod);

            int textX = card.bounds.X + CardInnerPad;
            int textY = card.bounds.Y + CardInnerPad;
            int textWidth = card.bounds.Width - CardInnerPad * 2;

            // Theme name (big, centred).
            string themeName = theme.Value.ToString();
            Vector2 nameSize = Game1.dialogueFont.MeasureString(themeName);
            float nameX = card.bounds.X + (card.bounds.Width - nameSize.X) / 2f;
            Utility.drawTextWithShadow(b, themeName, Game1.dialogueFont,
                new Vector2(nameX, textY), Game1.textColor);
            textY += ThemeNameLineHeight;

            // Bonus + liability lines, word-wrapped to the card's inner width so the plain-
            // English modifier descriptions ("30% chance for mined resources to drop +1") can
            // span 1-2 lines without overflowing the card edge.
            Color bonusColor = new Color(34, 110, 34);
            Color liabilityColor = new Color(160, 34, 34);

            string bonusWrapped = Game1.parseText(bonusName, Game1.smallFont, textWidth);
            Utility.drawTextWithShadow(b, bonusWrapped, Game1.smallFont,
                new Vector2(textX, textY), bonusColor);
            textY += (int)Game1.smallFont.MeasureString(bonusWrapped).Y + 2;

            string liabilityWrapped = Game1.parseText(liabilityName, Game1.smallFont, textWidth);
            Utility.drawTextWithShadow(b, liabilityWrapped, Game1.smallFont,
                new Vector2(textX, textY), liabilityColor);
            textY += (int)Game1.smallFont.MeasureString(liabilityWrapped).Y + SectionGap;

            // Bonus header above the icon row (which is anchored to the card bottom).
            int bonusHeaderY = card.bounds.Y + card.bounds.Height - CardInnerPad - BonusIconSize - BodyLineHeight - 4;
            Utility.drawTextWithShadow(b, "Bonus this week (1.5x):", Game1.smallFont,
                new Vector2(textX, bonusHeaderY), Game1.textColor);

            // Bonus item icons (pre-computed bounds).
            DrawBonusIcons(b, bonus, bonusBounds);
        }

        private void DrawBonusIcons(SpriteBatch b, List<Item> items, List<Rectangle> bounds)
        {
            for (int i = 0; i < items.Count && i < bounds.Count; i++)
            {
                Item item = items[i];
                Rectangle slot = bounds[i];
                Vector2 pos = new Vector2(slot.X, slot.Y);
                if (item != null)
                {
                    // StackDrawType.Draw renders the stack number badge over the icon — needed
                    // so the player sees the actual donation quantity (e.g. Wood = 99, not "1").
                    // The Item is created with Stack=GetStackForIngredient(id) above.
                    item.drawInMenu(b, pos, BonusIconScale, 1f, 0.86f, StackDrawType.Draw, Color.White, false);
                }
                else
                {
                    // Unresolved id — draw "?" placeholder.
                    var qSrc = new Rectangle(403, 496, 5, 7);
                    const int qScale = 4;
                    int qW = qSrc.Width * qScale;
                    int qH = qSrc.Height * qScale;
                    b.Draw(Game1.mouseCursors,
                        new Rectangle(slot.X + (slot.Width - qW) / 2, slot.Y + (slot.Height - qH) / 2, qW, qH),
                        qSrc, Color.White);
                }
            }
        }

        private void DrawPreviewRow(SpriteBatch b, ClickableComponent row, string text)
        {
            IClickableMenu.drawTextureBox(b, Game1.menuTexture, new Rectangle(0, 256, 60, 60),
                row.bounds.X, row.bounds.Y, row.bounds.Width, row.bounds.Height,
                Color.White * 0.7f, 1f, false);
            Utility.drawTextWithShadow(b, text, Game1.smallFont,
                new Vector2(row.bounds.X + 16, row.bounds.Y + 12), Game1.textColor);
        }

    }
}
