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
        private const int CardWidth = 560;
        // Trimmed back to ~v1 height now that bundle-progress rows live in SeasonGoalsMenu.
        private const int CardHeight = 360;
        private const int CardSpacing = 32;
        private const int CardInnerPad = 28;
        private const int PanelPadding = 48;

        // ---------- Inner card layout ----------
        private const int ThemeNameLineHeight = 48;
        private const int BodyLineHeight = 28;
        private const int SectionGap = 12;

        // ---------- Bonus item icons ----------
        // Smaller than the v1 icon grid — bonus row needs to fit up to 7 icons in CardWidth-pad.
        private const float BonusIconScale = 0.75f;
        private const int BonusIconSize = 48;
        private const int BonusIconGap = 10;

        // ---------- Preview rows (foresight, Plan 06) ----------
        private const int PreviewRowHeight = 44;
        private const int PreviewSpacing = 8;
        private const int JunimoSpriteSize = 96;

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
        private readonly List<ClickableComponent> _weatherRows = new List<ClickableComponent>();
        private readonly List<ClickableComponent> _cartRows = new List<ClickableComponent>();

        private string[] _weatherForecast;
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
                : System.Array.Empty<string>();

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
            if (b == Microsoft.Xna.Framework.Input.Buttons.A && currentlySnappedComponent != null)
            {
                if (currentlySnappedComponent == _leftCard && _offer.Count > 0) { ConfirmSelection(_offer[0]); return; }
                if (currentlySnappedComponent == _rightCard && _offer.Count > 1) { ConfirmSelection(_offer[1]); return; }
            }
            if (b == Microsoft.Xna.Framework.Input.Buttons.B && !_themePicked) return;
            base.receiveGamePadButton(b);
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
                Item item = null;
                try { item = ItemRegistry.Create(id, stack, 0, allowNull: true); }
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
            int previewRows = _weatherSageSlots + _cartPreviewSlots;
            int previewBlock = previewRows == 0
                ? 0
                : (previewRows * PreviewRowHeight) + ((previewRows - 1) * PreviewSpacing) + PanelPadding;

            int titleBlock = 24 + (_junimoTexture != null ? JunimoSpriteSize + 12 : 0) + 48 + 20;

            width = (CardWidth * 2) + CardSpacing + (PanelPadding * 2);
            height = titleBlock + CardHeight + previewBlock + PanelPadding;

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
                downNeighborID = FirstRowIdBelowCards()
            };
            _rightCard = new ClickableComponent(new Rectangle(cardsRightX, cardsY, CardWidth, CardHeight),
                _offer.Count > 1 ? _offer[1].ToString() : "right-card")
            {
                myID = CardIdRight,
                leftNeighborID = CardIdLeft,
                downNeighborID = FirstRowIdBelowCards()
            };

            _weatherRows.Clear();
            _cartRows.Clear();

            int rowX = xPositionOnScreen + PanelPadding;
            int rowWidth = width - (PanelPadding * 2);
            int rowY = cardsY + CardHeight + PanelPadding;

            for (int i = 0; i < _weatherSageSlots; i++)
            {
                var row = new ClickableComponent(new Rectangle(rowX, rowY, rowWidth, PreviewRowHeight),
                    "weather-" + i)
                {
                    myID = WeatherIdBase + i,
                    upNeighborID = i == 0 ? CardIdLeft : (WeatherIdBase + i - 1),
                    downNeighborID = i == _weatherSageSlots - 1
                        ? (_cartPreviewSlots > 0 ? CartIdBase : -1)
                        : (WeatherIdBase + i + 1)
                };
                _weatherRows.Add(row);
                rowY += PreviewRowHeight + PreviewSpacing;
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

            allClickableComponents = new List<ClickableComponent>();
            allClickableComponents.Add(_leftCard);
            allClickableComponents.Add(_rightCard);
            allClickableComponents.AddRange(_weatherRows);
            allClickableComponents.AddRange(_cartRows);

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
            int y = card.bounds.Y + card.bounds.Height - CardInnerPad - BonusIconSize;
            for (int i = 0; i < count; i++)
                bounds.Add(new Rectangle(startX + i * (BonusIconSize + BonusIconGap), y, BonusIconSize, BonusIconSize));
        }

        private int FirstRowIdBelowCards()
        {
            if (_weatherSageSlots > 0) return WeatherIdBase;
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

            if (_leftCard != null && _leftCard.containsPoint(x, y) && _offer.Count > 0)
                _hoverText = DescribeCard(_offer[0]);
            else if (_rightCard != null && _rightCard.containsPoint(x, y) && _offer.Count > 1)
                _hoverText = DescribeCard(_offer[1]);

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

            if (_leftCard != null && _leftCard.containsPoint(x, y) && _offer.Count > 0)
                ConfirmSelection(_offer[0]);
            else if (_rightCard != null && _rightCard.containsPoint(x, y) && _offer.Count > 1)
                ConfirmSelection(_offer[1]);
        }

        private void ConfirmSelection(Theme theme)
        {
            _themePicked = true;
            if (_isPreSelectForNextMonth)
                _runController.PreSelectForNextMonth(theme);
            else
                _runController.SelectByName(theme.ToString());
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

            for (int i = 0; i < _weatherRows.Count; i++)
            {
                string label = (i < _weatherForecast.Length) ? _weatherForecast[i] : "?";
                DrawPreviewRow(b, _weatherRows[i], $"Day {i + 1}: {label}");
            }
            for (int i = 0; i < _cartRows.Count; i++)
            {
                string label = (i < _cartItems.Count && _cartItems[i] != null)
                    ? _cartItems[i].DisplayName
                    : "?";
                DrawPreviewRow(b, _cartRows[i], $"Cart: {label}");
            }

            base.draw(b);

            if (!string.IsNullOrEmpty(_hoverText))
                IClickableMenu.drawHoverText(b, _hoverText, Game1.smallFont);

            Game1.mouseCursorTransparency = 1f;
            this.drawMouse(b);
        }

        private void DrawCard(SpriteBatch b, ClickableComponent card, Theme? theme,
            List<Item> bonus, List<Rectangle> bonusBounds)
        {
            if (card == null) return;

            bool isFocused = currentlySnappedComponent == card;

            // Visible focus: a 4-pixel yellow inset border on the snapped card. The prior 10%
            // tint difference was too subtle to register as "this is the one you're picking" —
            // the 2026-05-27 playtest reported "controller can't pick between themes" partly
            // because the player couldn't see which card was selected.
            if (isFocused)
            {
                const int borderInset = -8;
                const int borderThickness = 4;
                Color borderColor = Color.Gold;
                Rectangle r = new Rectangle(
                    card.bounds.X + borderInset,
                    card.bounds.Y + borderInset,
                    card.bounds.Width - borderInset * 2,
                    card.bounds.Height - borderInset * 2);
                // Top / bottom / left / right.
                b.Draw(Game1.staminaRect, new Rectangle(r.X, r.Y, r.Width, borderThickness), borderColor);
                b.Draw(Game1.staminaRect, new Rectangle(r.X, r.Bottom - borderThickness, r.Width, borderThickness), borderColor);
                b.Draw(Game1.staminaRect, new Rectangle(r.X, r.Y, borderThickness, r.Height), borderColor);
                b.Draw(Game1.staminaRect, new Rectangle(r.Right - borderThickness, r.Y, borderThickness, r.Height), borderColor);
            }

            Color tint = isFocused ? Color.White : Color.White * 0.7f;
            IClickableMenu.drawTextureBox(b, Game1.menuTexture, new Rectangle(0, 256, 60, 60),
                card.bounds.X, card.bounds.Y, card.bounds.Width, card.bounds.Height,
                tint, 1f, false);

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

            // Theme name (big, centred).
            string themeName = theme.Value.ToString();
            Vector2 nameSize = Game1.dialogueFont.MeasureString(themeName);
            float nameX = card.bounds.X + (card.bounds.Width - nameSize.X) / 2f;
            Utility.drawTextWithShadow(b, themeName, Game1.dialogueFont,
                new Vector2(nameX, textY), Game1.textColor);
            textY += ThemeNameLineHeight;

            // Bonus + liability lines.
            Color bonusColor = new Color(34, 110, 34);
            Color liabilityColor = new Color(160, 34, 34);
            Utility.drawTextWithShadow(b, bonusName, Game1.smallFont,
                new Vector2(textX, textY), bonusColor);
            textY += BodyLineHeight;
            Utility.drawTextWithShadow(b, liabilityName, Game1.smallFont,
                new Vector2(textX, textY), liabilityColor);
            textY += BodyLineHeight + SectionGap;

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

        private string DescribeCard(Theme theme)
        {
            var (bonus, liability) = ThemeModifiers.For(theme);
            string bonusName = ThemeModifiers.DisplayNameFor(bonus);
            string liabilityName = ThemeModifiers.DisplayNameFor(liability);
            return $"{theme}\nBonus: {bonusName}\nLiability: {liabilityName}";
        }
    }
}
