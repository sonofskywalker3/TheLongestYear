using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.Menus;

namespace TheLongestYear.UI
{
    /// <summary>The look of a tab in the Junimo Shrine's left-hand strip, shared with the one
    /// book's tabs (<see cref="OneBookMenu"/>): a menu box, dimmed when not selected, with its
    /// label in the small font, shrunk to fit when it would overflow the tab and centred on its
    /// ink (<see cref="TextInk"/>).</summary>
    internal static class TabStrip
    {
        private const float InactiveAlpha = 0.7f;
        private const int LabelInset = 12;

        public static void DrawTab(SpriteBatch b, Rectangle bounds, string label, bool active)
        {
            Color tint = active ? Color.White : Color.White * InactiveAlpha;
            IClickableMenu.drawTextureBox(b, Game1.menuTexture, new Rectangle(0, 256, 60, 60),
                bounds.X, bounds.Y, bounds.Width, bounds.Height, tint, 1f, false);

            Vector2 labelSize = Game1.smallFont.MeasureString(label);
            float available = bounds.Width - LabelInset * 2;
            float labelScale = labelSize.X > available ? available / labelSize.X : 1f;
            Utility.drawTextWithShadow(b, label, Game1.smallFont,
                new Vector2(bounds.X + LabelInset, TextInk.CentredY(Game1.smallFont, label, bounds.Y, bounds.Height, labelScale)),
                Game1.textColor, labelScale);
        }
    }
}
