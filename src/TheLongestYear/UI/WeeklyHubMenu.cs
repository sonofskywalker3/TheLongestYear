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
    internal sealed partial class WeeklyHubMenu : IClickableMenu
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
            catch (Exception ex)
            {
                TheLongestYear.Loop.PatchLog.Trace($"{GetType().Name}: the Junimo sprite did not load ({ex.GetType().Name}); drawn without it.");
                _junimoTexture = null;
            }

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
                catch (Exception ex)
                {
                    TheLongestYear.Loop.PatchLog.TraceOnce("hub-item:" + slot.ItemId,
                        $"Planning hub: goal item '{slot.ItemId}' could not be created ({ex.GetType().Name}); its icon is left empty.");
                    item = null;
                }
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
    }
}
