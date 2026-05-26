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
    /// (weather + cart). Opened automatically on DayStarted at week-start and via the hotkey.
    /// The actual foresight content + upgrade-tier gating are Plan 06; here we render placeholder
    /// rows at a config-default count to lock the layout in.
    /// </summary>
    internal sealed class ContractPickMenu : IClickableMenu
    {
        private const int CardWidth = 360;
        private const int CardHeight = 220;
        private const int CardSpacing = 32;
        private const int PreviewRowHeight = 44;
        private const int PreviewSpacing = 8;
        private const int PanelPadding = 32;

        private const int CardIdLeft = 5100;
        private const int CardIdRight = 5101;
        private const int WeatherIdBase = 5200;
        private const int CartIdBase = 5300;

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

        private string _hoverText = "";

        public ContractPickMenu(IMonitor monitor, RunController runController, GameplayConfig config,
            RunState run, YearPlan plan, IReadOnlyList<Theme> offer)
            : base(0, 0, 0, 0, showUpperRightCloseButton: true)
        {
            _monitor = monitor;
            _runController = runController;
            _config = config;
            _run = run;
            _plan = plan;
            _offer = offer ?? new List<Theme>();

            RecomputeBoundsAndLayout();

            if (Game1.options.snappyMenus && Game1.options.gamepadControls)
            {
                this.snapToDefaultClickableComponent();
            }
        }

        public override void gameWindowSizeChanged(Rectangle oldBounds, Rectangle newBounds)
        {
            base.gameWindowSizeChanged(oldBounds, newBounds);
            RecomputeBoundsAndLayout();
        }

        private void RecomputeBoundsAndLayout()
        {
            int previewRows = _config.DefaultWeatherPreviewSlots + _config.DefaultCartPreviewSlots;
            int previewBlock = previewRows == 0
                ? 0
                : (previewRows * PreviewRowHeight) + ((previewRows - 1) * PreviewSpacing) + PanelPadding;

            width = (CardWidth * 2) + CardSpacing + (PanelPadding * 2);
            height = 40 + 16 + CardHeight + previewBlock + PanelPadding;

            xPositionOnScreen = (Game1.uiViewport.Width - width) / 2;
            yPositionOnScreen = (Game1.uiViewport.Height - height) / 2;

            int cardsY = yPositionOnScreen + 40 + 16;
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
            if (upperRightCloseButton != null)
                allClickableComponents.Add(upperRightCloseButton);
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

        public override void performHoverAction(int x, int y)
        {
            base.performHoverAction(x, y);
            _hoverText = "";

            if (_leftCard != null && _leftCard.containsPoint(x, y) && _offer.Count > 0)
                _hoverText = DescribeCard(_offer[0]);
            else if (_rightCard != null && _rightCard.containsPoint(x, y) && _offer.Count > 1)
                _hoverText = DescribeCard(_offer[1]);
        }

        public override void receiveLeftClick(int x, int y, bool playSound = true)
        {
            base.receiveLeftClick(x, y, playSound);

            if (_leftCard != null && _leftCard.containsPoint(x, y) && _offer.Count > 0)
                ConfirmChampion(_offer[0]);
            else if (_rightCard != null && _rightCard.containsPoint(x, y) && _offer.Count > 1)
                ConfirmChampion(_offer[1]);
        }

        public override void receiveGamePadButton(Microsoft.Xna.Framework.Input.Buttons b)
        {
            if (b == Microsoft.Xna.Framework.Input.Buttons.A && currentlySnappedComponent != null)
            {
                if (currentlySnappedComponent == _leftCard && _offer.Count > 0) { ConfirmChampion(_offer[0]); return; }
                if (currentlySnappedComponent == _rightCard && _offer.Count > 1) { ConfirmChampion(_offer[1]); return; }
            }
            base.receiveGamePadButton(b);
        }

        private void ConfirmChampion(Theme theme)
        {
            _runController.ChampionByName(theme.ToString());
            Game1.playSound("smallSelect");
            this.exitThisMenu();
        }

        public override void draw(SpriteBatch b)
        {
            b.Draw(Game1.fadeToBlackRect, new Rectangle(0, 0, Game1.uiViewport.Width, Game1.uiViewport.Height),
                Color.Black * 0.5f);

            IClickableMenu.drawTextureBox(b, xPositionOnScreen, yPositionOnScreen, width, height, Color.White);

            string title = $"Week {_run.WeekOfYear}: Champion a theme";
            SpriteText.drawStringHorizontallyCenteredAt(b, title,
                xPositionOnScreen + width / 2, yPositionOnScreen + 24);

            DrawCard(b, _leftCard, _offer.Count > 0 ? (Theme?)_offer[0] : null);
            DrawCard(b, _rightCard, _offer.Count > 1 ? (Theme?)_offer[1] : null);

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

        private void DrawCard(SpriteBatch b, ClickableComponent card, Theme? theme)
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
            // Body uses manual \n line breaks; RequiredItemsSummary keeps text short (<= 4 items + "+N more").
            // Visual overflow on small viewports handled in Task 11.
            string body = $"{theme.Value}\n\nBonus: {bonus}\nLiability: {liability}\n\nRequired: {RequiredItemsSummary(theme.Value)}";
            Utility.drawTextWithShadow(b, body, Game1.smallFont,
                new Vector2(card.bounds.X + 24, card.bounds.Y + 24),
                Game1.textColor);
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
            string required = RequiredItemsSummary(theme);
            return $"{theme}\nBonus: {bonus}\nLiability: {liability}\nRequired: {required}";
        }

        private string RequiredItemsSummary(Theme theme)
        {
            Contract c = _plan.Get(_run.Season, theme);
            if (c.RequiredItemIds.Count == 0)
                return "(nothing this season)";
            if (c.RequiredItemIds.Count <= 4)
                return string.Join(", ", c.RequiredItemIds);
            return string.Join(", ", c.RequiredItemIds.Take(4)) + $" + {c.RequiredItemIds.Count - 4} more";
        }
    }
}
