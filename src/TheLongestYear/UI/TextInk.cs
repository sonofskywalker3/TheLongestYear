using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Microsoft.Xna.Framework.Graphics;
using TheLongestYear.Core;

namespace TheLongestYear.UI
{
    /// <summary>Centres a one-line label in a box by its drawn ink (<see cref="TextCentering"/>),
    /// read from the font's own glyph metrics. Centring <c>MeasureString(...).Y</c> leaves the
    /// letters flush with the top of a button and a gap below, because that height includes the
    /// space under the baseline and the line gap.</summary>
    internal static class TextInk
    {
        private static readonly ConditionalWeakTable<SpriteFont, Dictionary<char, SpriteFont.Glyph>> GlyphsByFont = new();
        private static readonly ConditionalWeakTable<SpriteFont, Dictionary<string, (float Top, float Height)>> InkByFont = new();

        /// <summary>The y to draw <paramref name="text"/> at so its ink sits centred in the box.</summary>
        public static float CentredY(SpriteFont font, string text, float boxTop, float boxHeight, float scale = 1f)
        {
            (float top, float height) = Extent(font, text);
            return TextCentering.InkCentredY(boxTop, boxHeight, top, height, scale);
        }

        /// <summary>Ink top and height of the line, unscaled. Falls back to the whole measured line
        /// when the font has no glyph metrics for it.</summary>
        public static (float Top, float Height) Extent(SpriteFont font, string text)
        {
            text ??= "";
            Dictionary<string, (float, float)> cache = InkByFont.GetValue(font, _ => new Dictionary<string, (float, float)>());
            if (cache.TryGetValue(text, out (float, float) known)) return known;

            // GetGlyphs builds a new dictionary each call, so it is read once per font.
            Dictionary<char, SpriteFont.Glyph> glyphs = GlyphsByFont.GetValue(font, f => f.GetGlyphs());
            (float Top, float Height)? ink = TextCentering.InkExtent(text
                .Where(c => glyphs.ContainsKey(c))
                .Select(c => (glyphs[c].Cropping.Y, glyphs[c].BoundsInTexture.Height)));
            (float, float) result = ink ?? (0f, font.MeasureString(text).Y);
            cache[text] = result;
            return result;
        }
    }
}
