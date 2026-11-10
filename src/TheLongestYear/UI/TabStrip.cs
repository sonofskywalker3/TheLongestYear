using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.Menus;

namespace TheLongestYear.UI
{
    /// <summary>The look of a tab in the Junimo Shrine's left-hand strip, shared with the one
    /// book's tabs (<see cref="OneBookMenu"/>): a menu box, dimmed when not selected, with its
    /// label in the small font, shrunk to fit when it would overflow the tab and centred on its
    /// ink (<see cref="TextInk"/>). The shrine passes a count ("3/12"), drawn smaller in a
    /// right-hand column; the label then shrinks to the room left of it.</summary>
    internal static class TabStrip
    {
        private const float InactiveAlpha = 0.7f;
        private const int LabelInset = 12;
        private const float CountScale = 0.8f;
        private const float CountAlpha = 0.8f;
        private const int CountGap = 8;

        public static void DrawTab(SpriteBatch b, Rectangle bounds, string label, bool active, string count = null)
        {
            Color tint = active ? Color.White : Color.White * InactiveAlpha;
            IClickableMenu.drawTextureBox(b, Game1.menuTexture, new Rectangle(0, 256, 60, 60),
                bounds.X, bounds.Y, bounds.Width, bounds.Height, tint, 1f, false);

            float available = bounds.Width - LabelInset * 2;
            if (!string.IsNullOrEmpty(count))
            {
                float countWidth = Game1.smallFont.MeasureString(count).X * CountScale;
                Utility.drawTextWithShadow(b, count, Game1.smallFont,
                    new Vector2(bounds.Right - LabelInset - countWidth,
                        TextInk.CentredY(Game1.smallFont, count, bounds.Y, bounds.Height, CountScale)),
                    Game1.textColor * CountAlpha, CountScale, shadowIntensity: CountAlpha);
                available -= countWidth + CountGap;
            }

            Vector2 labelSize = Game1.smallFont.MeasureString(label);
            float labelScale = labelSize.X > available ? available / labelSize.X : 1f;
            Utility.drawTextWithShadow(b, label, Game1.smallFont,
                new Vector2(bounds.X + LabelInset, TextInk.CentredY(Game1.smallFont, label, bounds.Y, bounds.Height, labelScale)),
                Game1.textColor, labelScale);
        }
    }
}
