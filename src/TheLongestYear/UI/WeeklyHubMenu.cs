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
        private const int CardHeight = 344;   // taller so the bonus row clears both the text above and the frame below
        private const int CardSpacing = 24;
        private const int CardInnerPad = 20;
        private const int PanelPadding = 32;

        // ---------- Inner card layout ----------
        private const int ThemeNameLineHeight = 44;
        private const int BodyLineHeight = 26;
        private const int SectionGap = 8;

        // ---------- Card positions (the slot index the randomizer's multiplier is keyed on) ----------
        private const int LeftSlot = 0;
        private const int RightSlot = 1;
        private const int NoSlot = -1;

        // ---------- Face-down mystery card ----------
        private const float MysteryMarkScale = 3f;
        private const int MysteryMarkGap = 16;

        // ---------- Header tip lines (banking tip + season-multiplier line) ----------
        // Vertical gap from the banking-tip line to the season-multiplier line below it
        // (0.12.0 clarity pass). titleBlock must reserve this same amount so the cards
        // don't start until this second line has fully cleared — see RecomputeBoundsAndLayout.
        private const int SeasonMultLineHeight = 36;

        // ---------- Bonus item icons ----------
        // Smaller than the v1 icon grid — bonus row needs to fit up to 7 icons in CardWidth-pad.
        private const float BonusIconScale = 0.75f;
        private const int BonusIconSize = 48;
        private const int BonusIconGap = 10;
        // Bottom margin for the bonus row (icons + their header), so they sit off the card frame.
        private const int BonusBottomMargin = 34;

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
        // Per-cell hover bounds (number+icon rows), so hovering a day shows "Day N - Weather"
        // exactly like the planning-shrine board. Rebuilt in RecomputeBoundsAndLayout.
        private readonly List<(Rectangle Bounds, ForecastDay Day)> _weatherCells = new List<(Rectangle, ForecastDay)>();

        // ---------- Component IDs ----------
        private const int CardIdLeft = 5100;
        private const int CardIdRight = 5101;
        private const int WeatherIdBase = 5200;
        private const int CartIdBase = 5300;

        private readonly IMonitor _monitor;
        private readonly RunController _runController;
        private readonly GameplayConfig _config;
        private readonly RunState _run;

        /// <summary>Season the menu's bundle progress + bonus preview reflect. May be NEXT season
        /// (Sunday-night day-28 case) — see <see cref="_isPreSelectForNextMonth"/>.</summary>
        private readonly CoreSeason _offerSeason;

        /// <summary>True when this hub is for next-month's week 1 (player is pre-picking before
        /// sleeping on day 28). The pick routes through <see cref="RunController.PreSelectForNextMonth"/>
        /// rather than the normal current-week selection path.</summary>
        private readonly bool _isPreSelectForNextMonth;
        private readonly RandomizerSettings _rand;

        /// <summary>Double theme week (spec section 6): one click takes both cards. Never on the day-28
        /// pre-pick hub, and an offer with fewer than two cards is a normal week. Recomputed when the
        /// offer changes (a reroll on a double week rerolls the pair).</summary>
        private bool _double;

        private const int DoubleWeekCards = 2;

        private bool ComputeDouble()
            => !_isPreSelectForNextMonth
               && _offer.Count == DoubleWeekCards
               && DoubleWeek.Is(_run.Seed, OfferWeek, _rand.DoubleThemeWeek);

        /// <summary>The card beside <paramref name="slot"/> on a double week (its drawback must not block
        /// that card's goals, matching RunController.SelectBoth); null on a single week.</summary>
        private Theme? OtherCard(int slot)
            => _double ? _offer[slot == LeftSlot ? RightSlot : LeftSlot] : null;

        private IReadOnlyList<Theme> _offer;

        private ClickableComponent _leftCard;
        private ClickableComponent _rightCard;
        private ClickableComponent _rerollButton;
        private int _rerollCounter;
        private readonly System.Func<long> _getJp;
        private readonly System.Action<long> _spendJp;
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
            RunState run, IReadOnlyList<Theme> offer,
            CoreSeason? offerSeason = null, bool isPreSelectForNextMonth = false,
            int weatherSageSlots = 0, int cartPreviewSlots = 0,
            System.Func<long> getJp = null, System.Action<long> spendJp = null)
            : base(0, 0, 0, 0, showUpperRightCloseButton: false)
        {
            _getJp = getJp;
            _spendJp = spendJp;
            _monitor = monitor;
            _runController = runController;
            _config = config;
            _run = run;
            _offer = offer ?? new List<Theme>();
            _offerSeason = offerSeason ?? run.Season;
            _isPreSelectForNextMonth = isPreSelectForNextMonth;
            // The day-28 hub offers next month's week: read live rather than snapshot a future week.
            _rand = isPreSelectForNextMonth ? (config.Randomizer ?? new RandomizerSettings()) : runController.Randomizer;
            _double = ComputeDouble();
            _weatherSageSlots = weatherSageSlots;

            // A re-roll sticks for the week (Nijah, Nexus 2026-09-28): reopening the hub shows the
            // stored re-rolled pair, and the restored count keeps the pick on the re-roll path.
            IReadOnlyList<Theme> rerolled = _run.RerolledOfferFor(OfferWeek, SelectionsForOffer);
            if (rerolled != null)
            {
                _offer = rerolled.ToList();
                _double = ComputeDouble();
                _rerollCounter = System.Math.Max(1, _run.RerollCount);
                _monitor.Log(
                    $"WeeklyHubMenu: restored re-rolled offer for week {OfferWeek} = " +
                    $"[{string.Join(", ", CardMultiplier.OfferLabels(_offer, _run.Seed, OfferWeek, _rand, _double))}] (reroll #{_rerollCounter}).",
                    LogLevel.Info);
                if (CardMultiplier.AnySealed(_offer.Count, _run.Seed, OfferWeek, _rand, _double))
                    _monitor.Log($"WeeklyHubMenu: restored offer with the face-down card = [{string.Join(", ", _offer)}].",
                        LogLevel.Trace);
            }
            _cartPreviewSlots = cartPreviewSlots;

            try { _junimoTexture = Game1.content.Load<Texture2D>("Characters\\Junimo"); }
            catch (Exception) { _junimoTexture = null; }

            _weatherForecast = _weatherSageSlots > 0
                ? WeatherForecast.Build(
                    (int)Game1.uniqueIDForThisGame,
                    (int)Game1.stats.DaysPlayed,
                    Game1.dayOfMonth,
                    (int)Game1.season,
                    _weatherSageSlots,
                    Loop.GreenRainDay.VanillaSummerDay())
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
                if (currentlySnappedComponent == _leftCard && _offer.Count > 0) { ConfirmSelection(_offer[0], LeftSlot); return; }
                if (currentlySnappedComponent == _rightCard && _offer.Count > 1) { ConfirmSelection(_offer[1], RightSlot); return; }
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

        /// <summary>Resolve the bonus-item preview for each card's theme from the current offer.
        /// The face-down card gets none: no icons, so no item tooltips either.</summary>
        private void ResolvePerCardData()
        {
            ResolveBonusItemsForTheme(_offer.Count > 0 && !IsSealed(LeftSlot) ? (Theme?)_offer[0] : null, _leftBonus);
            ResolveBonusItemsForTheme(_offer.Count > 1 && !IsSealed(RightSlot) ? (Theme?)_offer[1] : null, _rightBonus,
                // Double week: the right card shows its list without the lines the left card owns.
                ownedByOtherCard: _double ? PreviewFor(_offer[0]) : null);
        }

        /// <summary>True when the card in <paramref name="slot"/> is face down this offer week. Keyed on
        /// seed, week and slot only, so a reroll keeps the same slot sealed.</summary>
        private bool IsSealed(int slot) => CardMultiplier.IsSealed(_run.Seed, OfferWeek, slot, _rand, _double);

        /// <summary>Show a multiplier line on face-up cards when multipliers are in play this week.</summary>
        private bool ShowsMultiplier
            => _rand.RandomMultiplier || (!_double && CardMultiplier.IsMysteryWeek(_run.Seed, OfferWeek, _rand.MysteryCard));

        /// <summary>The goals a card's theme would get, as the hub previews them.</summary>
        private IReadOnlyList<BonusSlot> PreviewFor(Theme theme)
        {
            // Sample for the OFFER's season (which is next-season on day 28's Sunday-night hub).
            int week = _isPreSelectForNextMonth ? _run.WeekOfYear + 1 : _run.WeekOfYear;
            return _runController.PreviewSlotsForTheme(theme, _offerSeason, week);
        }

        private void ResolveBonusItemsForTheme(Theme? theme, List<Item> dest, IReadOnlyList<BonusSlot> ownedByOtherCard = null)
        {
            dest.Clear();
            if (theme == null) return;

            IReadOnlyList<BonusSlot> sample = PreviewFor(theme.Value);
            if (ownedByOtherCard != null)
                sample = GoalLists.Dedupe(ownedByOtherCard, sample);

            foreach (BonusSlot slot in sample)
            {
                // Each icon shows ITS slot's true requirement (stack badge + quality star) —
                // 2026-07-09 slot redesign; replaces the global per-id MAX maps here.
                Item item = null;
                try { item = ItemRegistry.Create(slot.ItemId, slot.Stack, slot.Quality, allowNull: true); }
                catch (Exception) { item = null; }
                dest.Add(item);
            }
        }

        /// <summary>Current season's JP multiplier, formatted for display ("1", "1.5", "2.5", "4").
        /// Resolved through the exact same source the JP economy uses (<see cref="JpCalculator"/>
        /// over <see cref="GameplayConfig.Jp"/>) rather than re-deriving the season/multiplier
        /// mapping here, so this display can never drift from what donations actually award.</summary>
        private string SeasonMultiplierDisplay()
        {
            int week = _isPreSelectForNextMonth ? _run.WeekOfYear + 1 : _run.WeekOfYear;
            double mult = new JpCalculator(_config.Jp).Multiplier(week);
            return mult.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
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

            int titleBlock = 24 + (_junimoTexture != null ? JunimoSpriteSize + 12 : 0) + 48 + 32 + 20 + SeasonMultLineHeight;

            width = (CardWidth * 2) + CardSpacing + (PanelPadding * 2);
            // Reserve space for the reroll debug button row below preview rows / cards — only when
            // the button is enabled (Randomizer Rerolls is not Off; off by default).
            int rerollBlock = (_rand.Rerolls != RerollMode.Off) ? RerollButtonHeight + 24 : 0;
            height = titleBlock + CardHeight + previewBlock + rerollBlock + PanelPadding;

            xPositionOnScreen = (Game1.uiViewport.Width - width) / 2;
            yPositionOnScreen = (Game1.uiViewport.Height - height) / 2;

            int cardsY = yPositionOnScreen + titleBlock;
            int cardsLeftX = xPositionOnScreen + PanelPadding;
            int cardsRightX = cardsLeftX + CardWidth + CardSpacing;

            _leftCard = new ClickableComponent(new Rectangle(cardsLeftX, cardsY, CardWidth, CardHeight),
                _offer.Count > 0 ? ThemeDisplay.Name(_offer[0]) : "left-card")
            {
                myID = CardIdLeft,
                rightNeighborID = CardIdRight,
                downNeighborID = FirstRowIdBelowCards() != -1 ? FirstRowIdBelowCards() : ((_rand.Rerolls != RerollMode.Off) ? RerollButtonId : -1)
            };
            _rightCard = new ClickableComponent(new Rectangle(cardsRightX, cardsY, CardWidth, CardHeight),
                _offer.Count > 1 ? ThemeDisplay.Name(_offer[1]) : "right-card")
            {
                myID = CardIdRight,
                leftNeighborID = CardIdLeft,
                downNeighborID = FirstRowIdBelowCards() != -1 ? FirstRowIdBelowCards() : ((_rand.Rerolls != RerollMode.Off) ? RerollButtonId : -1)
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
            _weatherCells.Clear();
            if (_weatherSageSlots > 0)
            {
                _weatherBlockY = rowY;
                // Hover bounds span the number + icon rows of each day column (matches the shrine
                // board), so the draw loop and the hover hit-test stay in lockstep.
                int numY = _weatherBlockY + WeatherHeaderH;
                for (int i = 0; i < _weatherForecast.Length; i++)
                {
                    int cellX = _weatherBlockX + i * WeatherCellWidth;
                    var bounds = new Rectangle(cellX, numY, WeatherCellWidth, WeatherNumberRowH + WeatherIconRowH);
                    _weatherCells.Add((bounds, _weatherForecast[i]));
                }
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
            // Randomizer Rerolls not being Off (off by default). When disabled it isn't built or added,
            // so receiveLeftClick / DrawRerollButton (both null-guarded) skip it entirely.
            if (_rand.Rerolls != RerollMode.Off)
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
            int y = card.bounds.Y + card.bounds.Height - BonusBottomMargin - BonusIconSize;
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

            // Weather calendar cells: "Day N - Weather", same as the planning-shrine board.
            if (string.IsNullOrEmpty(_hoverText))
            {
                foreach (var (bounds, day) in _weatherCells)
                {
                    if (bounds.Contains(x, y))
                    {
                        _hoverText = Strings.Get("menu.hub.day-label", new Dictionary<string, string>
                        {
                            ["day"] = day.DayOfMonth.ToString(),
                            ["weather"] = WeatherIcons.Label(day.Weather),
                        });
                        return;
                    }
                }
            }
        }

        private void CheckBonusIconHover(int x, int y, List<Item> items, List<Rectangle> bounds)
        {
            for (int i = 0; i < items.Count && i < bounds.Count; i++)
            {
                if (items[i] != null && bounds[i].Contains(x, y))
                {
                    // Show quantity in the hover so the player sees "Wood x99 (1.5x)" not just "Wood (1.5x)".
                    int hoverStack = items[i].Stack;
                    string qty = hoverStack > 1
                        ? Strings.Get("menu.hub.qty-suffix", new Dictionary<string, string> { ["count"] = hoverStack.ToString() })
                        : "";
                    _hoverText = Strings.Get("menu.hub.bonus-hover", new Dictionary<string, string>
                    {
                        ["name"] = items[i].DisplayName,
                        ["qty"] = qty,
                    });
                    return;
                }
            }
        }

        public override void receiveLeftClick(int x, int y, bool playSound = true)
        {
            base.receiveLeftClick(x, y, playSound);

            if (_rerollButton != null && _rerollButton.containsPoint(x, y))
            {
                Game1.playSound(TryRerollFromButton(out _) ? "smallSelect" : "cancel");
                return;
            }
            if (_leftCard != null && _leftCard.containsPoint(x, y) && _offer.Count > 0)
                ConfirmSelection(_offer[0], LeftSlot);
            else if (_rightCard != null && _rightCard.containsPoint(x, y) && _offer.Count > 1)
                ConfirmSelection(_offer[1], RightSlot);
        }

        /// <summary>The re-roll button's click: a paid reroll that cannot change the offer, or that
        /// the player cannot afford, is refused and takes nothing (final review T2: no charge, no
        /// count, no price step). On success the free or paid reroll runs. <paramref name="message"/>
        /// is the outcome line (also used by the tly_reroll paid command).</summary>
        private bool TryRerollFromButton(out string message)
        {
            long cost = CurrentRerollCost();
            long before = _getJp?.Invoke() ?? 0;
            if (cost > 0)
            {
                if (cost > before)
                {
                    message = $"Reroll refused: costs {cost} JP, only {before} JP.";
                    return false;
                }
                if (!RerollCanChange())
                {
                    message = "Reroll refused: no other pair to show.";
                    return false;
                }
                _spendJp?.Invoke(cost);
            }
            RerollOffer();
            message = cost > 0
                ? $"Reroll paid {cost} JP (JP {before} -> {_getJp?.Invoke() ?? 0})"
                : "Reroll (free)";
            return true;
        }

        /// <summary>The reroll button's own click path, for tly_reroll paid.</summary>
        public string RerollPaidForDebug()
        {
            TryRerollFromButton(out string message);
            return message;
        }

        /// <summary>The card click for one side, for tly_select &lt;theme&gt; &lt;left|right&gt;: the real card
        /// path (goal multiplier and mystery card included). Fails if the theme is not on that side.</summary>
        public bool TryPickSide(string themeName, string side, out string error)
        {
            error = null;
            int slot;
            if (side.Equals("left", System.StringComparison.OrdinalIgnoreCase)) slot = LeftSlot;
            else if (side.Equals("right", System.StringComparison.OrdinalIgnoreCase)) slot = RightSlot;
            else { error = $"side must be left or right, got '{side}'."; return false; }
            if (!System.Enum.TryParse(themeName, ignoreCase: true, out Theme theme))
            { error = $"unknown theme '{themeName}'."; return false; }
            if (_offer.Count <= slot || _offer[slot] != theme)
            {
                // The face-down card never shows its theme at Warn/Info; the real offer goes to Trace.
                error = $"{theme} is not on the {side} card (offer: [{string.Join(", ", CardMultiplier.OfferLabels(_offer, _run.Seed, OfferWeek, _rand, _double))}]).";
                _monitor.Log($"tly_select: real offer = [{string.Join(", ", _offer)}].", LogLevel.Trace);
                return false;
            }
            ConfirmSelection(theme, slot);
            if (!LastPickTook)
            { error = $"{theme} was rejected (already picked this month, or not a valid offer)."; return false; }
            double mult = _isPreSelectForNextMonth ? _run.NextMonthGoalMultiplier : _run.CurrentGoalMultiplier;
            if (_run.IsDoubleWeekSelection && !_isPreSelectForNextMonth)
                _monitor.Log(
                    $"Double week: selected {_run.CurrentSelection} (slot {LeftSlot}, goal JP {CardMultiplier.Format(_run.CurrentGoalMultiplier)}) " +
                    $"and {_run.SecondSelection} (slot {RightSlot}, goal JP {CardMultiplier.Format(_run.SecondGoalMultiplier)})",
                    LogLevel.Info);
            else
                _monitor.Log($"Selected {theme} (slot {slot}, goal JP {CardMultiplier.Format(mult)})", LogLevel.Info);
            return true;
        }

        /// <summary>One line per card for tly_hubcards: slot, theme (? plus the real theme at Trace when
        /// face down), drawback id and multiplier.</summary>
        public void LogCards()
        {
            for (int slot = 0; slot < _offer.Count; slot++)
            {
                Theme theme = _offer[slot];
                bool sealedCard = IsSealed(slot);
                string drawback = RandomPairing.LiabilityFor(_run.Seed, OfferWeek, theme, _rand.RandomPairings, OtherCard(slot));
                double mult = CardMultiplier.ForCard(_run.Seed, OfferWeek, theme, slot, _rand, _double);
                _monitor.Log(
                    $"Hub card slot {slot}: {(sealedCard ? CardMultiplier.SealedLabel : theme.ToString())}, " +
                    $"drawback {drawback}, goal JP {CardMultiplier.Format(mult)}{(sealedCard ? " (face down)" : "")}",
                    LogLevel.Info);
                if (sealedCard)
                    _monitor.Log($"Hub card slot {slot} is {theme}.", LogLevel.Trace);
            }
        }

        /// <summary>The week whose offer this hub shows: next month's week 1 on the day-28 pre-pick hub.</summary>
        private int OfferWeek => _isPreSelectForNextMonth ? _run.WeekOfYear + 1 : _run.WeekOfYear;

        /// <summary>JP the next reroll charges: rerolls already made this week set the price.</summary>
        private long CurrentRerollCost()
            => RerollPricing.CostOf(_rand.Rerolls, _run.RerollWeek == OfferWeek ? _run.RerollCount : 0);

        /// <summary>Cached <see cref="RerollCycle.CanChange"/> for the offer on screen; cleared by
        /// <see cref="RerollOffer"/>. The candidates do not change while the hub is open.</summary>
        private bool? _rerollCanChange;

        private bool RerollCanChange()
            => _rerollCanChange ??= RerollCycle.CanChange(
                _runController.OfferCandidates(OfferWeek, _offerSeason, SelectionsForOffer), _offer);

        /// <summary>The picks the offer excludes. The day-28 pre-pick is for next month, so none
        /// (the same rule <see cref="MenuLauncher.OpenWeeklyHub"/> uses for the first offer).</summary>
        private IReadOnlyCollection<Theme> SelectionsForOffer => _isPreSelectForNextMonth
            ? System.Array.Empty<Theme>()
            : _run.SelectedThemesThisMonth;

        /// <summary>
        /// Regenerate the offer (Randomizer Rerolls setting). Salt the underlying seed with the
        /// week's re-roll count so the offer stays deterministic. Candidates are every theme not
        /// picked this month that can ask for at least one goal, and <see cref="RerollCycle"/>
        /// never repeats a pair shown this week until every pair has been shown (Nijah, Nexus
        /// 2026-09-28). The re-rolled offer, seen pairs and count are kept on the RunState for the
        /// offer week, so reopening the hub that week shows the same pair.
        /// </summary>
        private void RerollOffer()
        {
            int week = OfferWeek;
            if (_run.RerollWeek != week)
            {
                // Stale state from another week (or none): start this week's cycle.
                _run.ClearReroll();
                _rerollCounter = 0;
            }
            // The offer on screen counts as shown (the first offer, on the first re-roll).
            string onScreen = RerollCycle.PairKey(_offer);
            if (_offer.Count > 0 && !_run.RerollSeenPairs.Contains(onScreen))
                _run.RerollSeenPairs.Add(onScreen);

            _rerollCounter++;
            IReadOnlyList<Theme> candidates = _runController
                .OfferCandidates(week, _offerSeason, SelectionsForOffer);
            var rng = new System.Random(_run.Seed ^ (week * 7919) ^ (_rerollCounter * RerollSaltPrime));
            _offer = RerollCycle.Next(candidates, _run.RerollSeenPairs, _offer, rng).ToList();
            _double = ComputeDouble();
            _run.RecordReroll(week, _offer, _rerollCounter);
            _rerollCanChange = null;
            ResolvePerCardData();
            RecomputeBoundsAndLayout();
            // The face-down card shows as "?" at Info; the real offer goes to Trace (final review I2).
            _monitor.Log(
                $"WeeklyHubMenu reroll #{_rerollCounter}: offer = " +
                $"[{string.Join(", ", CardMultiplier.OfferLabels(_offer, _run.Seed, OfferWeek, _rand, _double))}].",
                LogLevel.Info);
            if (CardMultiplier.AnySealed(_offer.Count, _run.Seed, OfferWeek, _rand, _double))
                _monitor.Log($"WeeklyHubMenu reroll #{_rerollCounter}: offer with the face-down card = [{string.Join(", ", _offer)}].",
                    LogLevel.Trace);
        }

        /// <summary>The re-roll button, for the tly_reroll console command (works whether or not the
        /// button is enabled, so a headless run can press it).</summary>
        public void RerollForDebug() => RerollOffer();

        /// <summary>The card click, by theme name, for the tly_select console command: the same
        /// commit path as the mouse (current-week pick or day-28 pre-pick), then the menu closes.
        /// Any theme is accepted, on or off the cards (it is a debug command).</summary>
        public bool ConfirmByName(string themeName)
        {
            if (!System.Enum.TryParse(themeName, ignoreCase: true, out Theme theme))
                return false;
            _forcedPick = !_offer.Contains(theme);
            // A debug pick pays 1x, like every other pick made off the cards.
            ConfirmSelection(theme, NoSlot);
            return true;
        }

        private bool _forcedPick;

        /// <summary>True when the last <see cref="ConfirmSelection"/> actually recorded the pick
        /// (SelectByName can reject it), so debug logs never claim a pick that did not happen.</summary>
        public bool LastPickTook { get; private set; }

        /// <param name="slot">The card position picked (0 left, 1 right); it sets the goal multiplier.</param>
        private void ConfirmSelection(Theme theme, int slot)
        {
            _themePicked = true;
            // Double week: either card (mouse, A button, or a console pick of a card on offer)
            // takes both, left card first (slot 0) and right card second (slot 1).
            if (_double && !_forcedPick)
            {
                _runController.SelectBoth(_offer[LeftSlot], _offer[RightSlot], skipOfferCheck: _rerollCounter > 0);
                LastPickTook = _run.CurrentSelection == _offer[LeftSlot] && _run.SecondSelection == _offer[RightSlot];
                Game1.playSound("smallSelect");
                this.exitThisMenu();
                return;
            }
            if (_isPreSelectForNextMonth)
                _runController.PreSelectForNextMonth(theme, slot);
            else
                // skipOfferCheck whenever the menu has rerolled so picks off the rerolled
                // offer aren't rejected by RunController's canonical OfferForWeek validation.
                // The reroll path already excludes already-selected-this-month themes, so the
                // gameplay rule that matters is preserved. A console pick off the cards is forced.
                _runController.SelectByName(theme.ToString(), skipOfferCheck: _rerollCounter > 0 || _forcedPick, slot: slot);
            LastPickTook = _isPreSelectForNextMonth
                ? _run.NextMonthSelection == theme
                : _run.CurrentSelection == theme;
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

            SpriteText.drawStringHorizontallyCenteredAt(b, _double ? Strings.Get("menu.hub.double-week") : Strings.Get("menu.hub.pick-theme"), panelCenterX, drawY);

            drawY += 48;
            string bankingTip = Strings.Get("menu.hub.banking-tip");
            Vector2 tipSize = Game1.smallFont.MeasureString(bankingTip);
            Utility.drawTextWithShadow(b, bankingTip, Game1.smallFont,
                new Vector2(panelCenterX - tipSize.X / 2f, drawY),
                Game1.textColor);

            drawY += SeasonMultLineHeight;
            string multTip = Strings.Get("menu.hub.season-mult",
                new Dictionary<string, string> { ["mult"] = SeasonMultiplierDisplay() });
            Vector2 multSize = Game1.smallFont.MeasureString(multTip);
            Utility.drawTextWithShadow(b, multTip, Game1.smallFont,
                new Vector2(panelCenterX - multSize.X / 2f, drawY),
                Game1.textColor);

            DrawCard(b, _leftCard, _offer.Count > 0 ? (Theme?)_offer[0] : null, _leftBonus, _leftBonusBounds, LeftSlot);
            DrawCard(b, _rightCard, _offer.Count > 1 ? (Theme?)_offer[1] : null, _rightBonus, _rightBonusBounds, RightSlot);

            DrawWeatherCalendar(b);
            for (int i = 0; i < _cartRows.Count; i++)
            {
                string label = (i < _cartItems.Count && _cartItems[i] != null)
                    ? _cartItems[i].DisplayName
                    : "?";
                DrawPreviewRow(b, _cartRows[i], Strings.Get("menu.hub.cart-label", new Dictionary<string, string> { ["label"] = label }));
            }

            DrawRerollButton(b);

            base.draw(b);

            if (!string.IsNullOrEmpty(_hoverText))
                HoverText.Draw(b, _hoverText);

            Game1.mouseCursorTransparency = 1f;
            this.drawMouse(b);
        }

        /// <summary>Render the playtester-only reroll button below the cards / preview rows.
        /// Plain texture-box + centred label; click rerolls the theme offer in place.</summary>
        private void DrawRerollButton(SpriteBatch b)
        {
            if (_rerollButton == null) return;

            long cost = CurrentRerollCost();
            bool blocked = cost > 0 && (cost > (_getJp?.Invoke() ?? 0) || !RerollCanChange());
            float boxAlpha = blocked ? 0.5f : 1f;
            IClickableMenu.drawTextureBox(b, Game1.menuTexture, new Rectangle(0, 256, 60, 60),
                _rerollButton.bounds.X, _rerollButton.bounds.Y,
                _rerollButton.bounds.Width, _rerollButton.bounds.Height,
                Color.White * boxAlpha, 1f, false);

            string label = cost > 0
                ? Strings.Get("menu.hub.reroll-cost", new Dictionary<string, string> { ["cost"] = cost.ToString() })
                : _rerollCounter == 0
                ? Strings.Get("menu.hub.reroll")
                : Strings.Get("menu.hub.reroll-count", new Dictionary<string, string> { ["count"] = _rerollCounter.ToString() });
            Vector2 size = Game1.smallFont.MeasureString(label);
            float labelX = _rerollButton.bounds.X + (_rerollButton.bounds.Width - size.X) / 2f;
            float labelY = _rerollButton.bounds.Y + (_rerollButton.bounds.Height - size.Y) / 2f;
            Utility.drawTextWithShadow(b, label, Game1.smallFont,
                new Vector2(labelX, labelY), Game1.textColor * boxAlpha);
        }

        /// <summary>Draw the weather foresight as a calendar strip (a "Weather" header, a row of
        /// day-of-month numbers, then a row of HUD weather icons in faint cells) — the same look as
        /// the planning-shrine board, so the two surfaces are visually consistent.</summary>
        private void DrawWeatherCalendar(SpriteBatch b)
        {
            if (_weatherSageSlots <= 0 || _weatherBlockY < 0 || _weatherForecast.Length == 0)
                return;

            Utility.drawTextWithShadow(b, Strings.Get("menu.hub.weather-header"), Game1.dialogueFont,
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

                var (tex, src) = WeatherIcons.Source(_weatherForecast[i].Weather);
                float iconX = cellX + (WeatherCellWidth - WeatherIconPx) / 2f;
                b.Draw(tex, new Vector2(iconX, iconY), src, Color.White, 0f,
                    Vector2.Zero, WeatherIconScale, SpriteEffects.None, 0.9f);
            }
        }

        // Icon + label lookups live in the shared WeatherIcons helper (one copy for both menus).

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
            List<Item> bonus, List<Rectangle> bonusBounds, int slot)
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
                Utility.drawTextWithShadow(b, Strings.Get("menu.hub.no-offer"), Game1.smallFont,
                    new Vector2(card.bounds.X + 24, card.bounds.Y + 24), Game1.textColor);
                return;
            }

            if (IsSealed(slot))
            {
                DrawSealedCard(b, card, theme.Value, slot);
                return;
            }

            string bonusMod = ThemeModifiers.For(theme.Value).BonusId;
            string liabilityMod = RandomPairing.LiabilityFor(_run.Seed, OfferWeek, theme.Value, _rand.RandomPairings, OtherCard(slot));
            string bonusName = ThemeModifiers.DisplayNameFor(bonusMod);
            string liabilityName = ThemeModifiers.DisplayNameFor(liabilityMod);

            int textX = card.bounds.X + CardInnerPad;
            int textY = card.bounds.Y + CardInnerPad;
            int textWidth = card.bounds.Width - CardInnerPad * 2;

            // Theme name (big, centred).
            string themeName = ThemeDisplay.Name(theme.Value);
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

            // Bonus header above the icon row (both share BonusBottomMargin so they move together).
            int bonusHeaderY = card.bounds.Y + card.bounds.Height - BonusBottomMargin - BonusIconSize - BodyLineHeight - 4;

            // Randomizer card multiplier, under the drawback. Two-line bonus and drawback lines still
            // leave room above the bonus header; the clamp keeps an extreme wrap off the header.
            if (ShowsMultiplier)
            {
                string multLine = Strings.Get("menu.hub.card-mult", new Dictionary<string, string>
                {
                    ["mult"] = CardMultiplier.Format(CardMultiplier.ForCard(_run.Seed, OfferWeek, theme.Value, slot, _rand, _double)),
                });
                int multHeight = (int)Game1.smallFont.MeasureString(multLine).Y;
                int multY = System.Math.Min(textY, bonusHeaderY - multHeight);
                Utility.drawTextWithShadow(b, multLine, Game1.smallFont,
                    new Vector2(textX, multY), Game1.textColor);
            }
            Utility.drawTextWithShadow(b, Strings.Get("menu.hub.bonus-week"), Game1.smallFont,
                new Vector2(textX, bonusHeaderY), Game1.textColor);

            // Bonus item icons (pre-computed bounds). An EMPTY pool (every slot this theme could
            // ask for is already donated) used to render as a blank icon row, which players read
            // as a bug (Bumblewyn, Nexus posts 2026-08-15). Say what it means instead — the
            // selection path auto-lifts the drawback (RunController.ApplyEmptyPoolLiftIfNeeded).
            if (bonus.Count == 0)
            {
                int iconRowY = card.bounds.Y + card.bounds.Height - BonusBottomMargin - BonusIconSize;
                string none = Game1.parseText(Strings.Get("menu.hub.bonus-none"), Game1.smallFont, textWidth);
                Utility.drawTextWithShadow(b, none, Game1.smallFont,
                    new Vector2(textX, iconRowY), Game1.textColor);
            }
            else
            {
                DrawBonusIcons(b, bonus, bonusBounds);
            }
        }

        /// <summary>The face-down mystery card: a large "?" and its multiplier, nothing else (no theme,
        /// buff, drawback or goal icons). Still a normal card for clicks and gamepad focus.</summary>
        private void DrawSealedCard(SpriteBatch b, ClickableComponent card, Theme theme, int slot)
        {
            const string mark = "?";
            Vector2 markSize = Game1.dialogueFont.MeasureString(mark) * MysteryMarkScale;
            string multLine = Strings.Get("menu.hub.mystery-mult", new Dictionary<string, string>
            {
                ["mult"] = CardMultiplier.Format(CardMultiplier.ForCard(_run.Seed, OfferWeek, theme, slot, _rand, _double)),
            });
            int textWidth = card.bounds.Width - CardInnerPad * 2;
            string multWrapped = Game1.parseText(multLine, Game1.smallFont, textWidth);
            Vector2 multSize = Game1.smallFont.MeasureString(multWrapped);

            float blockHeight = markSize.Y + MysteryMarkGap + multSize.Y;
            float top = card.bounds.Y + (card.bounds.Height - blockHeight) / 2f;
            Utility.drawTextWithShadow(b, mark, Game1.dialogueFont,
                new Vector2(card.bounds.X + (card.bounds.Width - markSize.X) / 2f, top),
                Game1.textColor, MysteryMarkScale);
            Utility.drawTextWithShadow(b, multWrapped, Game1.smallFont,
                new Vector2(card.bounds.X + (card.bounds.Width - multSize.X) / 2f, top + markSize.Y + MysteryMarkGap),
                Game1.textColor);
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
                    // The Item is created with the sampled BonusSlot's Stack/Quality above.
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
