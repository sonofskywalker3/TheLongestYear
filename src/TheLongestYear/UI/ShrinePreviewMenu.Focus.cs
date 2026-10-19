using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;
using TheLongestYear.Core;
using TheLongestYear.Focus;

namespace TheLongestYear.UI
{
    /// <summary>The Focus tab: the year's three chosen focus cards, each a row with its icon,
    /// name, price and a Buy button (or Owned / All kept), then its description. Host only, like
    /// Boosts. A building card hands the screen to the placement menu, which closes this one.</summary>
    internal sealed partial class ShrinePreviewMenu
    {
        private const int FocusIconPx = 48;
        private const float FocusIconScale = 0.75f;   // drawInMenu 1f is 64px
        private const int FocusIconHalfBox = 32;      // drawInMenu centres the sprite in a 64px box
        private const int FocusTextIndent = 24 + FocusIconPx + 12;

        private readonly FocusService _focus;

        /// <summary>Game tick of the last focus Buy. One gamepad A press can arrive as both a
        /// button and a synthesized click in the same tick (see JunimoShrineMenu.TryBuy).</summary>
        private int _lastFocusBuyTick = -1;

        private void BuildFocusRows()
        {
            if (!Context.IsMainPlayer)
            {
                _rows.Add(Note(Strings.Get("focus.row.host-only")));
                return;
            }
            IReadOnlyList<(FocusHand Hand, FocusQuote Quote)> cards = _focus?.ChosenCards();
            if (cards == null || cards.Count == 0)
            {
                _rows.Add(Note(Strings.Get("focus.row.none")));
                return;
            }

            int wrapWidth = ListWidthForWindow() - FocusTextIndent - 64;
            foreach ((FocusHand hand, FocusQuote quote) in cards)
            {
                FocusCardFace face = FocusCardStrip.FaceOf(hand, quote);
                _rows.Add(new Row { Kind = RowKind.FocusCard, Hand = hand, Quote = quote, Face = face });
                string text = face.Description;
                if (!hand.Bought && face.Note.Length > 0)
                    text = text.Length > 0 ? text + " " + face.Note : face.Note;
                if (text.Length == 0) continue;
                foreach (string line in Game1.parseText(text, Game1.smallFont, wrapWidth).Split('\n'))
                    _rows.Add(new Row { Kind = RowKind.FocusText, Text = line });
            }
        }

        /// <summary>A card row draws a Buy button only while the card can still be bought.</summary>
        private static bool HasFocusButton(Row row)
            => row.Kind == RowKind.FocusCard && FocusCardStrip.StateOf(row.Hand, row.Quote) == FocusCardState.Buy;

        private void BuyFocus(Row row)
        {
            if (!HasFocusButton(row) || _focus == null) return;
            if (Game1.ticks == _lastFocusBuyTick) return;   // the same press, dispatched twice
            _lastFocusBuyTick = Game1.ticks;

            FocusTheme theme = row.Hand.Theme;
            if (!_focus.CanAfford(theme))
            {
                Game1.playSound("cancel");
                Game1.addHUDMessage(new HUDMessage(Strings.Get("focus.cant-afford",
                    new Dictionary<string, string> { ["jp"] = row.Quote.Price.ToString() })));
                return;
            }

            FocusBuyResult result = _focus.Buy(theme);
            // Placing: the placement screen has already replaced this menu. Touching the active
            // menu now would cancel the placement, so leave it alone.
            if (result == FocusBuyResult.Placing) return;
            BuildRows();
            ClampScroll();
        }

        private void DrawFocusRow(SpriteBatch b, Row row, int rowY)
        {
            if (row.Kind == RowKind.FocusText)
            {
                Utility.drawTextWithShadow(b, row.Text, Game1.smallFont,
                    new Vector2(_listX + FocusTextIndent, rowY + 6), Game1.textColor * 0.85f);
                return;
            }

            int iconCentreX = _listX + 24 + FocusIconPx / 2;
            int iconCentreY = rowY + RowHeight / 2;
            row.Face.Icon?.drawInMenu(b, new Vector2(iconCentreX - FocusIconHalfBox, iconCentreY - FocusIconHalfBox), FocusIconScale);
            Utility.drawTextWithShadow(b, row.Face.Name, Game1.smallFont,
                new Vector2(_listX + FocusTextIndent, rowY + 6), Game1.textColor);

            FocusCardState state = FocusCardStrip.StateOf(row.Hand, row.Quote);
            Rectangle button = BoostButtonBounds(rowY);
            if (state != FocusCardState.Buy)
            {
                string label = FocusCardStrip.StatusText(row.Hand, row.Quote);
                Vector2 labelSize = Game1.smallFont.MeasureString(label);
                Utility.drawTextWithShadow(b, label, Game1.smallFont,
                    new Vector2(button.X + (button.Width - labelSize.X) / 2f, rowY + 6), FocusCardStrip.StatusColor(state));
                return;
            }

            // The same rule as FocusService.CanAfford (and the HUD icons), on the row's quote and the
            // live JP: re-quoting here would scan every chest each frame for a tool card. The click
            // still checks through FocusService.CanAfford/Buy on a fresh quote.
            bool affordable = _state.JunimoPoints >= row.Quote.Price;
            string cost = FocusCardStrip.StatusText(row.Hand, row.Quote);
            Vector2 costSize = Game1.smallFont.MeasureString(cost);
            Utility.drawTextWithShadow(b, cost, Game1.smallFont,
                new Vector2(button.X - 16 - costSize.X, rowY + 6), affordable ? Game1.textColor : Color.Brown);

            IClickableMenu.drawTextureBox(b, Game1.mouseCursors, new Rectangle(432, 439, 9, 9),
                button.X, button.Y, button.Width, button.Height,
                affordable ? Color.White : Color.Gray, 4f, drawShadow: false);
            string buy = Strings.Get("focus.row.buy");
            Vector2 buySize = Game1.smallFont.MeasureString(buy);
            Utility.drawTextWithShadow(b, buy, Game1.smallFont,
                new Vector2(button.X + (button.Width - buySize.X) / 2f, button.Y + (button.Height - buySize.Y) / 2f),
                Game1.textColor);
        }
    }
}
