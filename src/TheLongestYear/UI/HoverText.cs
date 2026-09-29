using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.Menus;

namespace TheLongestYear.UI
{
    /// <summary>Hover tooltips for the mod's menus, wrapped. Vanilla's drawHoverText sizes the box
    /// to the text's unwrapped width and only nudges it back from the right edge, so a long
    /// description ran off the left of the screen at larger UI scales (the Elevator Pass boost,
    /// a streamer's run, 2026-09-29). Wrapping keeps the existing line breaks.</summary>
    internal static class HoverText
    {
        private const int MaxWidth = 600;
        private const int ScreenMargin = 64;

        public static void Draw(SpriteBatch b, string text)
        {
            int width = System.Math.Min(MaxWidth, Game1.uiViewport.Width - ScreenMargin);
            IClickableMenu.drawHoverText(b, Game1.parseText(text, Game1.smallFont, width), Game1.smallFont);
        }
    }
}
