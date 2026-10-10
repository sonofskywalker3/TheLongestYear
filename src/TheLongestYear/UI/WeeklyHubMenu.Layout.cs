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
    internal sealed partial class WeeklyHubMenu
    {
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
    }
}
