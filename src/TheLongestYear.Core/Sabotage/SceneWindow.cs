using System;
using System.Collections.Generic;

namespace TheLongestYear.Core.Sabotage
{
    /// <summary>The pure arithmetic behind a lit window in an overnight strike scene (spec
    /// 2026-09-21, the hall): how bright the firelight is this instant, where a passing silhouette
    /// is, and how to cut that silhouette down to the pane it is crossing.
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

        /// <summary>A position pulled down onto the texel grid, so a shape moves a whole texel at a
        /// time like everything else in the game's art and its cut edge never lands mid-texel.</summary>
        public static int SnapToTexel(int pixels, int texel)
        {
            if (texel <= 0) throw new ArgumentOutOfRangeException(nameof(texel));
            int remainder = pixels % texel;
            return remainder < 0 ? pixels - remainder - texel : pixels - remainder;
        }

        /// <summary>The firelight's alpha for one window this instant. Each window is given its own
        /// offset so the row of them does not pulse in step.</summary>
        public static float Flicker(int elapsedMs, int windowIndex)
        {
            double phase = elapsedMs / FlickerPeriodMs + windowIndex;
            return GlowAlpha + GlowSwing * (float)Math.Sin(phase);
        }

        /// <summary>Where the figure in the window is this instant (Jeff, 2026-10-07: one of the
        /// mines' own shadow monsters, not a row of men). It stands out of sight at
        /// <paramref name="fromX"/> until <paramref name="enterAtMs"/>, steps across to
        /// <paramref name="toX"/> by <paramref name="arriveAtMs"/>, and stays there, at its work.
        /// <paramref name="walking"/> says whether it is between the two.</summary>
        public static int FigureX(int elapsedMs, int enterAtMs, int arriveAtMs, int fromX, int toX, out bool walking)
        {
            if (arriveAtMs <= enterAtMs) throw new ArgumentOutOfRangeException(nameof(arriveAtMs));
            walking = elapsedMs >= enterAtMs && elapsedMs < arriveAtMs;
            if (elapsedMs <= enterAtMs) return fromX;
            if (elapsedMs >= arriveAtMs) return toX;
            double across = (elapsedMs - enterAtMs) / (double)(arriveAtMs - enterAtMs);
            return fromX + (int)Math.Round((toX - fromX) * across);
        }

        /// <summary>Which of <paramref name="count"/> looping poses is showing this instant, each held
        /// <paramref name="frameMs"/>, counting from <paramref name="sinceMs"/>. Before that, the
        /// first.</summary>
        public static int Cycle(int elapsedMs, int sinceMs, int count, int frameMs)
        {
            if (count <= 0) throw new ArgumentOutOfRangeException(nameof(count));
            if (frameMs <= 0) throw new ArgumentOutOfRangeException(nameof(frameMs));
            if (elapsedMs <= sinceMs) return 0;
            return (elapsedMs - sinceMs) / frameMs % count;
        }

        /// <summary>A sprite frame's opaque texels with every enclosed hole filled in, indexed
        /// <c>[x, y]</c>. A monster's eyes and mouth are see-through pixels inside its outline,
        /// and a silhouette in a window is solid black (Jeff, 2026-10-07: "completely blacked
        /// out"), so anything the outside cannot reach without crossing the figure is figure.</summary>
        public static bool[,] FillHoles(bool[,] opaque)
        {
            if (opaque is null) throw new ArgumentNullException(nameof(opaque));
            int width = opaque.GetLength(0), height = opaque.GetLength(1);
            var outside = new bool[width, height];
            var queue = new Queue<(int X, int Y)>();
            void Seed(int x, int y)
            {
                if (opaque[x, y] || outside[x, y]) return;
                outside[x, y] = true;
                queue.Enqueue((x, y));
            }
            for (int x = 0; x < width; x++) { Seed(x, 0); Seed(x, height - 1); }
            for (int y = 0; y < height; y++) { Seed(0, y); Seed(width - 1, y); }
            while (queue.Count > 0)
            {
                (int x, int y) = queue.Dequeue();
                if (x > 0) Seed(x - 1, y);
                if (x < width - 1) Seed(x + 1, y);
                if (y > 0) Seed(x, y - 1);
                if (y < height - 1) Seed(x, y + 1);
            }
            var solid = new bool[width, height];
            for (int x = 0; x < width; x++)
                for (int y = 0; y < height; y++)
                    solid[x, y] = !outside[x, y];
            return solid;
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
