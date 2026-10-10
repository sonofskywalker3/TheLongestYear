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
