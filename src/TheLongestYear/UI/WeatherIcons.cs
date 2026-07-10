using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using TheLongestYear.Core;

namespace TheLongestYear.UI
{
    /// <summary>Single source for forecast weather icons + labels, shared by the shrine planning
    /// board and the weekly hub (previously two hand-synced copies of the same switch — a rect or
    /// weather-string fix applied to one menu silently missed the other).</summary>
    internal static class WeatherIcons
    {
        /// <summary>Weather-icon texture + source rect matching the in-game TV/HUD icons.
        /// Green rain's icon lives on the 1.6 sheet (<c>Game1.mouseCursors_1_6</c>, same rect the
        /// TV forecast uses); everything else is on <c>Game1.mouseCursors</c>. Unknown/Sun falls
        /// through to the sunny icon.</summary>
        internal static (Texture2D Texture, Rectangle Source) Source(string weather) => weather switch
        {
            "Rain" => (Game1.mouseCursors, new Rectangle(465, 333, 13, 13)),
            "Storm" => (Game1.mouseCursors, new Rectangle(413, 346, 13, 13)),
            "Snow" => (Game1.mouseCursors, new Rectangle(465, 346, 13, 13)),
            "Festival" => (Game1.mouseCursors, new Rectangle(413, 372, 13, 13)),
            WeatherScheduler.GreenRain => (Game1.mouseCursors_1_6, new Rectangle(178, 363, 13, 13)),
            _ => (Game1.mouseCursors, new Rectangle(413, 333, 13, 13)), // Sun / default
        };

        /// <summary>Human label for a forecast weather string ("GreenRain" → "Green Rain").</summary>
        internal static string Label(string weather)
            => weather == WeatherScheduler.GreenRain ? "Green Rain" : weather;
    }
}
