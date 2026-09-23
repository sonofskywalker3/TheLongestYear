using System;
using System.Collections.Generic;

namespace TheLongestYear.Core.Sabotage
{
    /// <summary>The pure arithmetic behind a lit window in an overnight strike scene (spec
    /// 2026-09-21, the hall): how bright the firelight is this instant, where a passing silhouette
    /// has slid to, and how to cut that silhouette down to the pane it is crossing.
    ///
    /// It is here, and not beside the painter, because it is the only part of a lit window that can
    /// be checked without a graphics device. The painter itself is a handful of Draw calls.
    ///
    /// WHY A CLIPPED SOURCE RECTANGLE AND NOT A SCISSOR RECTANGLE. A scene paints inside a
    /// SpriteBatch the game opened (Game1.cs:13698), and scissor testing needs both a
    /// RasterizerState with ScissorTestEnable and a device write, neither of which can be changed
    /// without ending that batch and reopening one with different state. Cutting the destination
    /// rectangle down and taking the matching slice of the source texture gives the same picture
    /// with no device state touched at all.</summary>
    public static class SceneWindow
    {
        /// <summary>The steady part of the firelight's alpha. The brief said 0.55, and Jeff found it
        /// too bright on seeing it (2026-09-23), so it is turned down.</summary>
        private const float GlowAlpha = 0.35f;

        /// <summary>How far the flicker swings either side of <see cref="GlowAlpha"/>.</summary>
        private const float GlowSwing = 0.10f;

        /// <summary>The flicker's period, in milliseconds of the scene's own clock.</summary>
        private const double FlickerPeriodMs = 180.0;

        /// <summary>How fast each silhouette crosses the glass, in pixels a second. Three different
        /// speeds, so they never read as one object moving.</summary>
        private static readonly int[] ShapeSpeeds = { 58, 34, 81 };

        /// <summary>Where each silhouette starts, as a share of its whole journey, so they are not
        /// all at the left edge when the scene opens.</summary>
        private static readonly double[] ShapePhases = { 0.0, 0.42, 0.77 };

        /// <summary>How many silhouettes there are to ask about.</summary>
        public static int ShapeCount => ShapeSpeeds.Length;

        /// <summary>How long one step of a silhouette's walk is, in milliseconds, and how far apart
        /// the three are in their stride so they do not bob together.</summary>
        private const int StrideMs = 260;
        private static readonly int[] StridePhasesMs = { 0, 110, 190 };

        /// <summary>A man seen through the glass, one character per texel, <c>#</c> filled and
        /// <c>.</c> clear (Jeff, 2026-09-23: the shapes were too big and read as blobs, and should
        /// read as a man's silhouette). A head, a neck, shoulders and a body, drawn at the game's own
        /// four pixels a texel so it sits on the same grid as the window art. The legs are left to
        /// the sill, which hides them.</summary>
        public static readonly IReadOnlyList<string> Silhouette = new[]
        {
            "..###..",
            ".#####.",
            ".#####.",
            "..###..",
            "...#...",
            ".#####.",
            "#######",
            "#######",
            "#######",
            "#######",
            ".#####.",
            ".#####.",
            ".#####.",
            ".#####.",
            ".#####.",
            ".#####.",
            ".#####.",
            ".#####.",
        };

        /// <summary>The silhouette's size in texels.</summary>
        public static int SilhouetteWidth => Silhouette[0].Length;
        public static int SilhouetteHeight => Silhouette.Count;

        /// <summary>A position pulled down onto the texel grid, so a shape moves a whole texel at a
        /// time like everything else in the game's art and its cut edge never lands mid-texel.</summary>
        public static int SnapToTexel(int pixels, int texel)
        {
            if (texel <= 0) throw new ArgumentOutOfRangeException(nameof(texel));
            int remainder = pixels % texel;
            return remainder < 0 ? pixels - remainder - texel : pixels - remainder;
        }

        /// <summary>How many texels a walking silhouette is lifted this instant: nothing and one,
        /// step by step, which is the rise and fall of a man walking.</summary>
        public static int StrideBob(int elapsedMs, int shapeIndex)
        {
            if (shapeIndex < 0 || shapeIndex >= StridePhasesMs.Length) throw new ArgumentOutOfRangeException(nameof(shapeIndex));
            int at = Math.Max(0, elapsedMs) + StridePhasesMs[shapeIndex];
            return (at / StrideMs) % 2;
        }

        /// <summary>The firelight's alpha for one window this instant. Each window is given its own
        /// offset so the row of them does not pulse in step.</summary>
        public static float Flicker(int elapsedMs, int windowIndex)
        {
            double phase = elapsedMs / FlickerPeriodMs + windowIndex;
            return GlowAlpha + GlowSwing * (float)Math.Sin(phase);
        }

        /// <summary>The left edge of silhouette <paramref name="shapeIndex"/> this instant, in the
        /// same pixel space as <paramref name="spanLeft"/>.
        ///
        /// It starts entirely off the left end of the span and finishes entirely off the right end,
        /// then wraps, so a shape is never half born in the middle of a pane.</summary>
        /// <param name="spanLeft">The left edge of the glass the shapes cross.</param>
        /// <param name="spanWidth">How wide that glass is in all.</param>
        /// <param name="shapeWidth">How wide one silhouette is.</param>
        public static int SlideX(int elapsedMs, int shapeIndex, int spanLeft, int spanWidth, int shapeWidth)
        {
            if (shapeIndex < 0 || shapeIndex >= ShapeSpeeds.Length) throw new ArgumentOutOfRangeException(nameof(shapeIndex));
            if (shapeWidth <= 0) throw new ArgumentOutOfRangeException(nameof(shapeWidth));
            int travel = Math.Max(1, spanWidth + shapeWidth * 2);
            double gone = Math.Max(0, elapsedMs) / 1000.0 * ShapeSpeeds[shapeIndex] + ShapePhases[shapeIndex] * travel;
            double along = gone % travel;
            return spanLeft - shapeWidth + (int)along;
        }

        /// <summary>One draw of a silhouette, already cut down to the pane it is crossing.</summary>
        public readonly struct ClippedDraw
        {
            public ClippedDraw(int destX, int destY, int destWidth, int destHeight, int sourceX, int sourceY, int sourceWidth, int sourceHeight)
            {
                DestX = destX;
                DestY = destY;
                DestWidth = destWidth;
                DestHeight = destHeight;
                SourceX = sourceX;
                SourceY = sourceY;
                SourceWidth = sourceWidth;
                SourceHeight = sourceHeight;
            }

            public int DestX { get; }
            public int DestY { get; }
            public int DestWidth { get; }
            public int DestHeight { get; }
            public int SourceX { get; }
            public int SourceY { get; }
            public int SourceWidth { get; }
            public int SourceHeight { get; }
        }

        /// <summary>Cut a silhouette down to the part of it that is inside one pane, and work out
        /// which slice of its texture that part is. False when the shape misses the pane entirely,
        /// and then nothing should be drawn.</summary>
        /// <param name="sourceWidth">The silhouette texture's own width.</param>
        /// <param name="sourceHeight">The silhouette texture's own height.</param>
        public static bool Clip(
            int shapeX, int shapeY, int shapeWidth, int shapeHeight,
            int windowX, int windowY, int windowWidth, int windowHeight,
            int sourceWidth, int sourceHeight,
            out ClippedDraw draw)
        {
            draw = default;
            if (shapeWidth <= 0 || shapeHeight <= 0) return false;
            if (windowWidth <= 0 || windowHeight <= 0) return false;
            if (sourceWidth <= 0 || sourceHeight <= 0) return false;

            int left = Math.Max(shapeX, windowX);
            int top = Math.Max(shapeY, windowY);
            int right = Math.Min(shapeX + shapeWidth, windowX + windowWidth);
            int bottom = Math.Min(shapeY + shapeHeight, windowY + windowHeight);
            if (right <= left || bottom <= top) return false;

            // The visible slice of the shape, as a share of the whole shape, is the same share of
            // the texture. Each edge is worked out on its own so rounding can never push the slice
            // off the texture.
            int sx = (left - shapeX) * sourceWidth / shapeWidth;
            int sRight = (right - shapeX) * sourceWidth / shapeWidth;
            int sy = (top - shapeY) * sourceHeight / shapeHeight;
            int sBottom = (bottom - shapeY) * sourceHeight / shapeHeight;
            sx = Math.Min(sx, sourceWidth - 1);
            sy = Math.Min(sy, sourceHeight - 1);
            int sw = Math.Max(1, Math.Min(sourceWidth - sx, sRight - sx));
            int sh = Math.Max(1, Math.Min(sourceHeight - sy, sBottom - sy));

            draw = new ClippedDraw(left, top, right - left, bottom - top, sx, sy, sw, sh);
            return true;
        }
    }
}
