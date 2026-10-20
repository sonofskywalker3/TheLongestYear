using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;

namespace TheLongestYear.UI
{
    /// <summary>"10 (13)" in the item stack count's own pixel digits (<see cref="Utility.drawTinyDigits"/>),
    /// for a goal the theme week lowered: the lowered count, then the full ask in brackets. The game's
    /// digit strip has no brackets, so they are built once from the "0" glyph's own colours.</summary>
    internal static class TinyCount
    {
        private const int DigitStripX = 368;
        private const int DigitStripY = 56;
        private const int DigitWidth = 5;
        private const int GlyphHeight = 7;
        private const int BracketWidth = 5;
        /// <summary>Blank pixels between the count and its bracket, in glyph pixels.</summary>
        private const int SpaceWidth = 3;

        // 'o' outline, 'f' fill, '.' clear; same 7-pixel height as the digits. The clear inner
        // column keeps the bracket off the digits it encloses (glyphs overlap by one pixel).
        private static readonly string[] OpenBracket =
        {
            "..oo.",
            ".ofo.",
            "ofo..",
            "ofo..",
            "ofo..",
            ".ofo.",
            "..oo.",
        };

        private static Texture2D _open;
        private static Texture2D _close;

        /// <summary>Drawn width of "<paramref name="count"/> (<paramref name="full"/>)" at <paramref name="scale"/>.</summary>
        public static float Width(int count, int full, float scale)
        {
            float width = 0f;
            float lastWidth = 0f;
            float lastAdvance = 0f;
            foreach (int glyph in Glyphs(count, full))
            {
                lastWidth = GlyphWidth(glyph) * scale;
                lastAdvance = Advance(glyph, scale);
                width += lastAdvance;
            }
            return width - lastAdvance + lastWidth;
        }

        /// <summary>Draws "<paramref name="count"/> (<paramref name="full"/>)" with its top-left at <paramref name="position"/>.</summary>
        public static void Draw(SpriteBatch b, int count, int full, Vector2 position, float scale, float layerDepth, Color color)
        {
            EnsureBrackets();
            float x = 0f;
            foreach (int glyph in Glyphs(count, full))
            {
                Vector2 at = position + new Vector2(x, 0f);
                if (glyph >= 0)
                    b.Draw(Game1.mouseCursors, at, new Rectangle(DigitStripX + glyph * DigitWidth, DigitStripY, DigitWidth, GlyphHeight),
                        color, 0f, Vector2.Zero, scale, SpriteEffects.None, layerDepth);
                else if (glyph == Open && _open != null)
                    b.Draw(_open, at, null, color, 0f, Vector2.Zero, scale, SpriteEffects.None, layerDepth);
                else if (glyph == Close && _close != null)
                    b.Draw(_close, at, null, color, 0f, Vector2.Zero, scale, SpriteEffects.None, layerDepth);
                x += Advance(glyph, scale);
            }
        }

        private const int Space = -1;
        private const int Open = -2;
        private const int Close = -3;

        private static IEnumerable<int> Glyphs(int count, int full)
        {
            foreach (char c in count.ToString()) yield return c - '0';
            yield return Space;
            yield return Open;
            foreach (char c in full.ToString()) yield return c - '0';
            yield return Close;
        }

        private static int GlyphWidth(int glyph) => glyph switch
        {
            Space => SpaceWidth,
            Open or Close => BracketWidth,
            _ => DigitWidth,
        };

        /// <summary>Same step as drawTinyDigits: glyph width at scale less one pixel, so outlines touch.</summary>
        private static float Advance(int glyph, float scale) => (int)(GlyphWidth(glyph) * scale) - 1;

        private static void EnsureBrackets()
        {
            if (_open != null && !_open.IsDisposed && _close != null && !_close.IsDisposed) return;
            (Color fill, Color outline) = ZeroColours();
            _open = Build(OpenBracket, fill, outline, mirror: false);
            _close = Build(OpenBracket, fill, outline, mirror: true);
        }

        /// <summary>The "0" glyph's brightest pixel (fill) and darkest opaque pixel (outline).</summary>
        private static (Color Fill, Color Outline) ZeroColours()
        {
            var pixels = new Color[DigitWidth * GlyphHeight];
            Game1.mouseCursors.GetData(0, new Rectangle(DigitStripX, DigitStripY, DigitWidth, GlyphHeight), pixels, 0, pixels.Length);
            Color fill = Color.White, outline = Color.Transparent;
            int brightest = -1, darkest = int.MaxValue;
            foreach (Color p in pixels)
            {
                if (p.A == 0) continue;
                int lum = p.R + p.G + p.B;
                if (lum > brightest) { brightest = lum; fill = p; }
                if (lum < darkest) { darkest = lum; outline = p; }
            }
            return (fill, darkest == brightest ? Color.Transparent : outline);
        }

        private static Texture2D Build(string[] rows, Color fill, Color outline, bool mirror)
        {
            var data = new Color[BracketWidth * GlyphHeight];
            for (int y = 0; y < GlyphHeight; y++)
                for (int x = 0; x < BracketWidth; x++)
                {
                    char c = rows[y][mirror ? BracketWidth - 1 - x : x];
                    data[y * BracketWidth + x] = c == 'f' ? fill : c == 'o' ? outline : Color.Transparent;
                }
            var texture = new Texture2D(Game1.graphics.GraphicsDevice, BracketWidth, GlyphHeight);
            texture.SetData(data);
            return texture;
        }
    }
}
