using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using TheLongestYear.Core;

namespace TheLongestYear.UI
{
    /// <summary>Where a chosen card stands: buyable at its price, already bought, every rung
    /// already kept, or not open yet.</summary>
    internal enum FocusCardState { Buy, Owned, Done, Locked }

    /// <summary>Draws the three chosen focus cards side by side: an icon and, beside it, the
    /// card's status (price, owned or all kept). <c>compact</c> leaves the name out (it shows on
    /// hover). Display only. A short area puts the label on the left of the cards; a tall one puts
    /// it above them.</summary>
    internal static class FocusCardStrip
    {
        public const int CellHeight = 56;
        private const int Slots = 3;
        private const int IconPx = 48;
        private const float IconScale = 0.75f;   // drawInMenu 1f is 64px
        private const int IconHalfBox = 32;      // drawInMenu centres the sprite in a 64px box
        private const int IconTextGap = 8;
        private const int LabelGap = 16;
        private const int LabelLineH = 32;

        private static readonly Color OwnedGreen = new(30, 130, 30);
        private static readonly Color MutedGray = new(110, 100, 90);

        /// <summary>The faces drawn last, keyed by chain and bought rung, so a strip drawn every
        /// frame builds each icon item once. Cleared on save load and on a locale change
        /// (<see cref="ClearFaces"/>), because a face holds translated text and a live item.</summary>
        private static readonly Dictionary<string, FocusCardFace> FaceCache = new(StringComparer.Ordinal);

        public static void ClearFaces() => FaceCache.Clear();

        public static FocusCardState StateOf(FocusHand hand, FocusQuote quote)
        {
            if (hand.Bought) return FocusCardState.Owned;
            return quote.Kind switch
            {
                FocusQuoteKind.Done => FocusCardState.Done,
                FocusQuoteKind.Locked => FocusCardState.Locked,
                _ => FocusCardState.Buy,
            };
        }

        /// <summary>The status text: the price, "Owned" or "All kept".</summary>
        public static string StatusText(FocusHand hand, FocusQuote quote) => StateOf(hand, quote) switch
        {
            FocusCardState.Owned => Strings.Get("focus.row.owned"),
            FocusCardState.Done => Strings.Get("focus.row.done"),
            _ => Strings.Get("focus.card.price", new Dictionary<string, string> { ["jp"] = quote.Price.ToString() }),
        };

        public static Color StatusColor(FocusCardState state) => state switch
        {
            FocusCardState.Owned => OwnedGreen,
            FocusCardState.Buy => Game1.textColor,
            _ => MutedGray,
        };

        public static FocusCardFace FaceOf(FocusHand hand, FocusQuote quote)
        {
            string bought = hand.Bought ? hand.BoughtUpgradeId : null;
            string key = hand.Chosen + "|" + (bought ?? quote.UpgradeId ?? "");
            if (!FaceCache.TryGetValue(key, out FocusCardFace face))
                FaceCache[key] = face = FocusCardFace.For(hand.Chosen, quote, bought);
            return face;
        }

        /// <summary>The height a strip with its label above the cards needs.</summary>
        public static int StackedHeight => LabelLineH + CellHeight;

        public static void Draw(SpriteBatch b, Rectangle area, IReadOnlyList<(FocusHand Hand, FocusQuote Quote)> cards, bool compact)
        {
            if (cards == null || cards.Count == 0) return;
            string label = Strings.Get("focus.hub.label");
            Vector2 labelSize = Game1.smallFont.MeasureString(label);
            Rectangle cellsArea = CellsArea(area, labelSize);
            Vector2 labelPos = Stacked(area)
                ? new Vector2(area.X, area.Y)
                : new Vector2(area.X, area.Y + (area.Height - labelSize.Y) / 2f);
            Utility.drawTextWithShadow(b, label, Game1.smallFont, labelPos, Game1.textColor);

            for (int i = 0; i < cards.Count && i < Slots; i++)
                DrawCell(b, CellBounds(cellsArea, i), cards[i].Hand, cards[i].Quote, compact);
        }

        /// <summary>The hover text for the cell under (x, y): the card's name and description.</summary>
        public static string HoverAt(Rectangle area, IReadOnlyList<(FocusHand Hand, FocusQuote Quote)> cards, int x, int y)
        {
            if (cards == null || cards.Count == 0) return null;
            Rectangle cellsArea = CellsArea(area, Game1.smallFont.MeasureString(Strings.Get("focus.hub.label")));
            for (int i = 0; i < cards.Count && i < Slots; i++)
            {
                if (!CellBounds(cellsArea, i).Contains(x, y)) continue;
                FocusCardFace face = FaceOf(cards[i].Hand, cards[i].Quote);
                return face.Description.Length > 0 ? face.Name + "\n" + face.Description : face.Name;
            }
            return null;
        }

        private static bool Stacked(Rectangle area) => area.Height >= StackedHeight;

        private static Rectangle CellsArea(Rectangle area, Vector2 labelSize)
        {
            if (Stacked(area))
                return new Rectangle(area.X, area.Y + LabelLineH, area.Width, CellHeight);
            int left = (int)labelSize.X + LabelGap;
            return new Rectangle(area.X + left, area.Y + (area.Height - CellHeight) / 2, Math.Max(0, area.Width - left), CellHeight);
        }

        private static Rectangle CellBounds(Rectangle cellsArea, int slot)
        {
            int w = cellsArea.Width / Slots;
            return new Rectangle(cellsArea.X + slot * w, cellsArea.Y, w, cellsArea.Height);
        }

        private static void DrawCell(SpriteBatch b, Rectangle cell, FocusHand hand, FocusQuote quote, bool compact)
        {
            FocusCardFace face = FaceOf(hand, quote);
            int iconCentreY = cell.Y + cell.Height / 2;
            face.Icon?.drawInMenu(b, new Vector2(cell.X + IconPx / 2 - IconHalfBox, iconCentreY - IconHalfBox), IconScale);

            int textX = cell.X + IconPx + IconTextGap;
            int textWidth = Math.Max(1, cell.Right - textX - IconTextGap);
            FocusCardState state = StateOf(hand, quote);
            string status = StatusText(hand, quote);
            if (compact)
            {
                DrawFitted(b, status, textX, iconCentreY - Game1.smallFont.MeasureString(status).Y / 2f, textWidth, StatusColor(state));
                return;
            }
            float lineH = Game1.smallFont.MeasureString("A").Y;
            float top = iconCentreY - lineH;
            DrawFitted(b, face.Name, textX, top, textWidth, Game1.textColor);
            DrawFitted(b, status, textX, top + lineH, textWidth, StatusColor(state));
        }

        /// <summary>One line of small text, scaled down when it would run past <paramref name="maxWidth"/>.</summary>
        private static void DrawFitted(SpriteBatch b, string text, int x, float y, int maxWidth, Color color)
        {
            float w = Game1.smallFont.MeasureString(text).X;
            float scale = w > maxWidth ? maxWidth / w : 1f;
            Utility.drawTextWithShadow(b, text, Game1.smallFont, new Vector2(x, y), color, scale);
        }
    }
}
