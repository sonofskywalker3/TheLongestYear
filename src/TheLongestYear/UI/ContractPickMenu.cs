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

namespace TheLongestYear.UI
{
    /// <summary>
    /// The weekly planning hub: champion-offer cards (1 of 2) and the foresight preview surface
    /// (weather + cart). Opened automatically on DayStarted at week-start. Modal: the player must
    /// pick a theme before the menu can be closed (no close button, Escape/B swallowed until picked).
    /// The actual foresight content + upgrade-tier gating are Plan 06; here we render placeholder
    /// rows at a config-default count to lock the layout in.
    /// </summary>
    internal sealed class ContractPickMenu : IClickableMenu
    {
        private const int CardWidth = 520;
        private const int CardHeight = 320;
        private const int CardSpacing = 32;
        private const int PreviewRowHeight = 44;
        private const int PreviewSpacing = 8;
        private const int PanelPadding = 40;
        private const int JunimoSpriteSize = 64;

        private const int CardIdLeft = 5100;
        private const int CardIdRight = 5101;
        private const int WeatherIdBase = 5200;
        private const int CartIdBase = 5300;

        // Icon grid constants: 3 columns x 2 rows = 6 max visible items
        private const int IconGridCols = 3;
        private const int IconGridRows = 2;
        private const int MaxIcons = IconGridCols * IconGridRows;
        private const int IconSize = 64;
        private const int IconGap = 16;

        private readonly IMonitor _monitor;
        private readonly RunController _runController;
        private readonly GameplayConfig _config;
        private readonly RunState _run;
        private readonly YearPlan _plan;
        private readonly IReadOnlyList<Theme> _offer;

        private ClickableComponent _leftCard;
        private ClickableComponent _rightCard;
        private readonly List<ClickableComponent> _weatherRows = new List<ClickableComponent>();
        private readonly List<ClickableComponent> _cartRows = new List<ClickableComponent>();

        // Junimo sprite texture (null if the asset is missing).
        private readonly Texture2D _junimoTexture;

        // Resolved item lists for the icon grid (cached; up to MaxIcons per card).
        private readonly List<Item> _leftItems = new List<Item>();
        private readonly List<Item> _rightItems = new List<Item>();
        private int _leftOverflow;
        private int _rightOverflow;

        // Icon bounds for hover detection (populated in RecomputeBoundsAndLayout).
        private readonly List<Rectangle> _leftIconBounds = new List<Rectangle>();
        private readonly List<Rectangle> _rightIconBounds = new List<Rectangle>();

        private string _hoverText = "";
        private bool _themePicked = false;

        public ContractPickMenu(IMonitor monitor, RunController runController, GameplayConfig config,
            RunState run, YearPlan plan, IReadOnlyList<Theme> offer)
            : base(0, 0, 0, 0, showUpperRightCloseButton: false)
        {
            _monitor = monitor;
            _runController = runController;
            _config = config;
            _run = run;
            _plan = plan;
            _offer = offer ?? new List<Theme>();

            // Load Junimo sprite; fall back gracefully if the asset is unavailable.
            try
            {
                _junimoTexture = Game1.content.Load<Texture2D>("LooseSprites\\Junimo");
            }
            catch (Exception)
            {
                _junimoTexture = null;
            }

            // Resolve item lists for each offered theme now (cached per menu open).
            ResolveItemLists();

            RecomputeBoundsAndLayout();

            if (Game1.options.snappyMenus && Game1.options.gamepadControls)
            {
                this.snapToDefaultClickableComponent();
            }
        }

        // ---------- modal guard ----------

        public override bool readyToClose() => _themePicked;

        public override void receiveKeyPress(Microsoft.Xna.Framework.Input.Keys key)
        {
            // Block Escape and any close-by-keyboard until the player picks a theme.
            if (!_themePicked && (key == Microsoft.Xna.Framework.Input.Keys.Escape))
                return;
            base.receiveKeyPress(key);
        }

        public override void receiveGamePadButton(Microsoft.Xna.Framework.Input.Buttons b)
        {
            if (b == Microsoft.Xna.Framework.Input.Buttons.A && currentlySnappedComponent != null)
            {
                if (currentlySnappedComponent == _leftCard && _offer.Count > 0) { ConfirmChampion(_offer[0]); return; }
                if (currentlySnappedComponent == _rightCard && _offer.Count > 1) { ConfirmChampion(_offer[1]); return; }
            }
            // Swallow B (back/close) until the player picks.
            if (b == Microsoft.Xna.Framework.Input.Buttons.B && !_themePicked)
                return;
            base.receiveGamePadButton(b);
        }

        // ---------- layout ----------

        public override void gameWindowSizeChanged(Rectangle oldBounds, Rectangle newBounds)
        {
            base.gameWindowSizeChanged(oldBounds, newBounds);
            RecomputeBoundsAndLayout();
        }

        private void ResolveItemLists()
        {
            ResolveItems(_offer.Count > 0 ? (Theme?)_offer[0] : null, _leftItems, out _leftOverflow);
            ResolveItems(_offer.Count > 1 ? (Theme?)_offer[1] : null, _rightItems, out _rightOverflow);
        }

        private void ResolveItems(Theme? theme, List<Item> items, out int overflow)
        {
            items.Clear();
            overflow = 0;
            if (theme == null) return;

            Contract c = _plan.Get(_run.Season, theme.Value);
            int total = c.RequiredItemIds.Count;
            overflow = Math.Max(0, total - MaxIcons);
            int take = Math.Min(total, MaxIcons);
            for (int i = 0; i < take; i++)
            {
                try
                {
                    Item item = ItemRegistry.Create(c.RequiredItemIds[i], 1, 0, allowNull: true);
                    items.Add(item); // item may be null — DrawIconGrid handles null defensively
                }
                catch (Exception)
                {
                    items.Add(null);
                }
            }
        }

        private void RecomputeBoundsAndLayout()
        {
            int previewRows = _config.DefaultWeatherPreviewSlots + _config.DefaultCartPreviewSlots;
            int previewBlock = previewRows == 0
                ? 0
                : (previewRows * PreviewRowHeight) + ((previewRows - 1) * PreviewSpacing) + PanelPadding;

            // Title area: Junimo sprite (JunimoSpriteSize) + gap (8) + title text line (~40) + gap (16)
            int titleBlock = (_junimoTexture != null ? JunimoSpriteSize + 8 : 0) + 40 + 16;

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

            for (int i = 0; i < _config.DefaultWeatherPreviewSlots; i++)
            {
                var row = new ClickableComponent(new Rectangle(rowX, rowY, rowWidth, PreviewRowHeight),
                    "weather-" + i)
                {
                    myID = WeatherIdBase + i,
                    upNeighborID = i == 0 ? CardIdLeft : (WeatherIdBase + i - 1),
                    downNeighborID = i == _config.DefaultWeatherPreviewSlots - 1
                        ? (_config.DefaultCartPreviewSlots > 0 ? CartIdBase : -1)
                        : (WeatherIdBase + i + 1)
                };
                _weatherRows.Add(row);
                rowY += PreviewRowHeight + PreviewSpacing;
            }

            for (int i = 0; i < _config.DefaultCartPreviewSlots; i++)
            {
                var row = new ClickableComponent(new Rectangle(rowX, rowY, rowWidth, PreviewRowHeight),
                    "cart-" + i)
                {
                    myID = CartIdBase + i,
                    upNeighborID = i == 0
                        ? (_config.DefaultWeatherPreviewSlots > 0
                            ? (WeatherIdBase + _config.DefaultWeatherPreviewSlots - 1)
                            : CardIdLeft)
                        : (CartIdBase + i - 1),
                    downNeighborID = i == _config.DefaultCartPreviewSlots - 1 ? -1 : (CartIdBase + i + 1)
                };
                _cartRows.Add(row);
                rowY += PreviewRowHeight + PreviewSpacing;
            }

            this.initializeUpperRightCloseButton();

            // Base populateClickableComponentList uses GetType().GetFields() which defaults to PUBLIC only,
            // so our private fields would be skipped. Build the list ourselves.
            allClickableComponents = new List<ClickableComponent>();
            allClickableComponents.Add(_leftCard);
            allClickableComponents.Add(_rightCard);
            allClickableComponents.AddRange(_weatherRows);
            allClickableComponents.AddRange(_cartRows);
            // upperRightCloseButton is null (showUpperRightCloseButton: false) — no-op, but left for clarity.
            if (upperRightCloseButton != null)
                allClickableComponents.Add(upperRightCloseButton);

            // Pre-compute icon bounds for hover detection (based on card positions).
            ComputeIconBounds(_leftCard, _leftIconBounds);
            ComputeIconBounds(_rightCard, _rightIconBounds);
        }

        private void ComputeIconBounds(ClickableComponent card, List<Rectangle> bounds)
        {
            bounds.Clear();
            if (card == null) return;

            // Icons start after: card padding (24) + theme name line (~40) + bonus line (~24) + liability line (~24) + "Required:" line (~24) + gap (8)
            int iconStartX = card.bounds.X + 24;
            int iconStartY = card.bounds.Y + 24 + 40 + 24 + 24 + 24 + 8;

            for (int row = 0; row < IconGridRows; row++)
            {
                for (int col = 0; col < IconGridCols; col++)
                {
                    int x = iconStartX + col * (IconSize + IconGap);
                    int y = iconStartY + row * (IconSize + IconGap);
                    bounds.Add(new Rectangle(x, y, IconSize, IconSize));
                }
            }
        }

        private int FirstRowIdBelowCards()
        {
            if (_config.DefaultWeatherPreviewSlots > 0) return WeatherIdBase;
            if (_config.DefaultCartPreviewSlots > 0) return CartIdBase;
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

            // Check card hover for description tooltip.
            if (_leftCard != null && _leftCard.containsPoint(x, y) && _offer.Count > 0)
                _hoverText = DescribeCard(_offer[0]);
            else if (_rightCard != null && _rightCard.containsPoint(x, y) && _offer.Count > 1)
                _hoverText = DescribeCard(_offer[1]);

            // Check icon hover for item display name tooltip (overrides card description if more specific).
            CheckIconHover(x, y, _leftItems, _leftIconBounds);
            CheckIconHover(x, y, _rightItems, _rightIconBounds);
        }

        private void CheckIconHover(int x, int y, List<Item> items, List<Rectangle> bounds)
        {
            for (int i = 0; i < items.Count && i < bounds.Count; i++)
            {
                if (items[i] != null && bounds[i].Contains(x, y))
                {
                    _hoverText = items[i].DisplayName;
                    return;
                }
            }
        }

        public override void receiveLeftClick(int x, int y, bool playSound = true)
        {
            base.receiveLeftClick(x, y, playSound);

            if (_leftCard != null && _leftCard.containsPoint(x, y) && _offer.Count > 0)
                ConfirmChampion(_offer[0]);
            else if (_rightCard != null && _rightCard.containsPoint(x, y) && _offer.Count > 1)
                ConfirmChampion(_offer[1]);
        }

        private void ConfirmChampion(Theme theme)
        {
            _themePicked = true;
            _runController.ChampionByName(theme.ToString());
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
            int drawY = yPositionOnScreen + 16;

            // Junimo sprite centred above the title.
            if (_junimoTexture != null)
            {
                int junimoX = panelCenterX - JunimoSpriteSize / 2;
                b.Draw(_junimoTexture,
                    new Rectangle(junimoX, drawY, JunimoSpriteSize, JunimoSpriteSize),
                    new Rectangle(0, 0, 16, 16),
                    Color.White);
                drawY += JunimoSpriteSize + 8;
            }

            // Title.
            SpriteText.drawStringHorizontallyCenteredAt(b, "Pick a theme", panelCenterX, drawY);
            drawY += 40 + 16;

            DrawCard(b, _leftCard, _offer.Count > 0 ? (Theme?)_offer[0] : null, _leftItems, _leftOverflow, _leftIconBounds);
            DrawCard(b, _rightCard, _offer.Count > 1 ? (Theme?)_offer[1] : null, _rightItems, _rightOverflow, _rightIconBounds);

            for (int i = 0; i < _weatherRows.Count; i++)
                DrawPreviewRow(b, _weatherRows[i], $"Weather, day {i + 1}: ???   (Weather Sage tier {i + 1} reveals this)");
            for (int i = 0; i < _cartRows.Count; i++)
                DrawPreviewRow(b, _cartRows[i], $"Cart slot {i + 1}: ???   (Cart Whisperer reveals this)");

            base.draw(b);

            if (!string.IsNullOrEmpty(_hoverText))
                IClickableMenu.drawHoverText(b, _hoverText, Game1.smallFont);

            Game1.mouseCursorTransparency = 1f;
            this.drawMouse(b);
        }

        private void DrawCard(SpriteBatch b, ClickableComponent card, Theme? theme,
            List<Item> items, int overflow, List<Rectangle> iconBounds)
        {
            if (card == null) return;

            Color tint = (currentlySnappedComponent == card)
                ? Color.White
                : Color.White * 0.9f;
            IClickableMenu.drawTextureBox(b, Game1.menuTexture, new Rectangle(0, 256, 60, 60),
                card.bounds.X, card.bounds.Y, card.bounds.Width, card.bounds.Height,
                tint, 1f, false);

            if (theme == null)
            {
                Utility.drawTextWithShadow(b, "(no offer)", Game1.smallFont,
                    new Vector2(card.bounds.X + 24, card.bounds.Y + 24), Game1.textColor);
                return;
            }

            var (bonus, liability) = ThemeModifiers.For(theme.Value);
            string bonusName = ThemeModifiers.DisplayNameFor(bonus);
            string liabilityName = ThemeModifiers.DisplayNameFor(liability);

            int textX = card.bounds.X + 24;
            int textY = card.bounds.Y + 24;

            // Theme name
            Utility.drawTextWithShadow(b, theme.Value.ToString(), Game1.smallFont,
                new Vector2(textX, textY), Game1.textColor);
            textY += 40;

            // Bonus / liability
            Utility.drawTextWithShadow(b, $"Bonus: {bonusName}", Game1.smallFont,
                new Vector2(textX, textY), Game1.textColor);
            textY += 24;

            Utility.drawTextWithShadow(b, $"Liability: {liabilityName}", Game1.smallFont,
                new Vector2(textX, textY), Game1.textColor);
            textY += 24;

            // Required items section header
            Utility.drawTextWithShadow(b, "Required:", Game1.smallFont,
                new Vector2(textX, textY), Game1.textColor);
            textY += 24 + 8;

            // Draw icon grid
            DrawIconGrid(b, items, overflow, iconBounds);
        }

        private void DrawIconGrid(SpriteBatch b, List<Item> items, int overflow, List<Rectangle> iconBounds)
        {
            for (int i = 0; i < items.Count && i < iconBounds.Count; i++)
            {
                Item item = items[i];
                Rectangle bounds = iconBounds[i];
                Vector2 pos = new Vector2(bounds.X, bounds.Y);

                if (item != null)
                {
                    item.drawInMenu(b, pos, 4f, 1f, 0.86f, StackDrawType.Hide, Color.White, false);
                }
                else
                {
                    // Placeholder rectangle for unresolved items.
                    b.Draw(Game1.fadeToBlackRect, bounds, Color.Gray * 0.5f);
                }
            }

            // "+N more" badge below the icon grid if items were truncated.
            if (overflow > 0 && iconBounds.Count > 0)
            {
                // Position below the last icon row.
                Rectangle lastBounds = iconBounds[iconBounds.Count - 1];
                int badgeX = lastBounds.X;
                int badgeY = lastBounds.Y + IconSize + 4;
                Utility.drawTextWithShadow(b, $"+{overflow} more", Game1.smallFont,
                    new Vector2(badgeX, badgeY), Game1.textColor * 0.8f);
            }
        }

        private void DrawPreviewRow(SpriteBatch b, ClickableComponent row, string text)
        {
            IClickableMenu.drawTextureBox(b, Game1.menuTexture, new Rectangle(0, 256, 60, 60),
                row.bounds.X, row.bounds.Y, row.bounds.Width, row.bounds.Height,
                Color.White * 0.7f, 1f, false);
            Utility.drawTextWithShadow(b, text, Game1.smallFont,
                new Vector2(row.bounds.X + 16, row.bounds.Y + 12),
                Game1.textColor);
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
