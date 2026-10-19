using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using TheLongestYear.Core;

namespace TheLongestYear.UI
{
    /// <summary>The small "x2" drawn at an icon's top-right on the favoured goal (spec
    /// 2026-10-19-focus-cards, "The favoured goal (x2)"). Gold text with the game's text shadow,
    /// like the white stack numbers beside it. Never red or a warning colour: the badge is a bonus,
    /// and nothing may hint the line is hard.</summary>
    internal static class FavouredBadge
    {
        /// <summary>Text scale on the planning hub's 48px goal icons.</summary>
        public const float HubScale = 0.75f;

        /// <summary>Text scale on the bundle page's 64px ingredient icons.</summary>
        public const float BundleScale = 0.9f;

        /// <summary>How far the badge sits out past the icon's top-right corner, in pixels.</summary>
        private const int Overhang = 4;

        private static readonly Color BadgeColor = Color.Gold;

        /// <param name="alpha">1 normally. The bundle page passes vanilla's 0.25 while another
        /// ingredient takes a partial donation, so the badge dims with its icon.</param>
        public static void Draw(SpriteBatch b, Rectangle icon, float scale, float alpha = 1f)
        {
            string text = Strings.Get("goal.favoured-badge");
            Vector2 size = Game1.smallFont.MeasureString(text) * scale;
            var pos = new Vector2(icon.Right - size.X + Overhang, icon.Y - Overhang);
            Utility.drawTextWithShadow(b, text, Game1.smallFont, pos, BadgeColor * alpha, scale, shadowIntensity: alpha);
        }
    }
}
