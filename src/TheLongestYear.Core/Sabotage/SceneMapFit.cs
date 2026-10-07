using System;

namespace TheLongestYear.Core.Sabotage
{
    /// <summary>Where the tamper scene's world map, and the frame round it, land on the screen
    /// (spec 2026-09-21, Scene 4). Positions are in map pixels (the art times four, as
    /// <c>MapRegion</c> works) going in, and in the paint space the scene draws in coming out.
    ///
    /// Two fits, and the scene picks one with a single switch:
    /// <list type="bullet">
    /// <item><see cref="MapTab"/>, the default (Jeff, 2026-10-07: the blown-up map "looks bad"):
    /// the size the in-game map tab draws it, four screen pixels per art pixel times the UI scale,
    /// centred, inside the map tab's own frame (<see cref="Frame"/>).</item>
    /// <item><see cref="Fill"/>, the earlier version: the largest scale at which the whole map fits
    /// the screen. Kept so it can come back with one line.</item>
    /// </list></summary>
    public static class SceneMapFit
    {
        /// <summary>A whole-number scale is kept by <see cref="Fill"/> when it fills at least this
        /// share of the fractional fit, so the art stays on a clean pixel grid; below it the
        /// fractional fit is used so the map still fills the screen.</summary>
        public const double IntegerFillShare = 0.9;

        /// <summary>The map tab's frame, read off <c>MapPage.drawMap</c> and
        /// <c>Game1.drawDialogueBox</c> in the 1.6 decompile: it sits this many UI pixels outside the
        /// map on every side, and its corner and edge pieces are this many UI pixels square.</summary>
        public const int FrameMargin = 32;
        public const int FramePiece = 64;

        /// <summary>The frame's filled middle starts this many UI pixels in from its outer edge
        /// (<c>drawDialogueBox</c> draws it at x + 28 with the width less 64).</summary>
        public const int FrameFillInset = 28;

        /// <summary>A rectangle in the world map's own art pixels, as <c>Data/WorldMap</c> writes
        /// one, scaled to map pixels (times four, as <c>MapRegion</c> does).</summary>
        public static (int X, int Y, int Width, int Height) ArtToMap(int x, int y, int width, int height, int artScale)
            => (x * artScale, y * artScale, width * artScale, height * artScale);

        /// <summary>The map at the size the in-game map tab draws it, on a screen
        /// <paramref name="viewWidth"/> by <paramref name="viewHeight"/> paint pixels. The map tab
        /// draws <paramref name="artScale"/> UI pixels per art pixel (MapRegion's four), and the
        /// scene paints in the zoomed world layer, so one UI pixel is
        /// <paramref name="uiToPaint"/> paint pixels (the UI scale over the zoom). That product is
        /// rounded to a whole number, at least one, so the art is never scaled by a fraction. The
        /// map is centred the way <c>Utility.getTopLeftPositionForCenteringOnScreen</c> centres it.
        /// Returns the scale from map pixels to paint pixels and the map's top left corner.</summary>
        public static (double Scale, int OriginX, int OriginY) MapTab(int viewWidth, int viewHeight, int mapWidth, int mapHeight, int artScale, double uiToPaint)
        {
            if (mapWidth <= 0 || mapHeight <= 0 || artScale <= 0) throw new ArgumentOutOfRangeException(nameof(mapWidth));
            int perArt = Math.Max(1, (int)Math.Round(artScale * uiToPaint));
            int artWidth = mapWidth / artScale, artHeight = mapHeight / artScale;
            int drawnWidth = artWidth * perArt, drawnHeight = artHeight * perArt;
            return (perArt / (double)artScale, viewWidth / 2 - drawnWidth / 2, viewHeight / 2 - drawnHeight / 2);
        }

        /// <summary>The largest scale at which the whole map fits a screen <paramref name="viewWidth"/>
        /// by <paramref name="viewHeight"/> paint pixels, aspect kept and centred on whole pixels; a
        /// whole number of screen pixels per art pixel when that still fills well. Not the default
        /// since 2026-10-07 (see the class summary).</summary>
        public static (double Scale, int OriginX, int OriginY) Fill(int viewWidth, int viewHeight, int mapWidth, int mapHeight, int artScale)
        {
            if (mapWidth <= 0 || mapHeight <= 0 || artScale <= 0) throw new ArgumentOutOfRangeException(nameof(mapWidth));
            double artWidth = mapWidth / (double)artScale, artHeight = mapHeight / (double)artScale;
            double fit = Math.Min(viewWidth / artWidth, viewHeight / artHeight);
            double whole = Math.Floor(fit);
            double perArt = whole >= 1 && whole / fit >= IntegerFillShare ? whole : fit;
            int originX = (int)Math.Floor((viewWidth - artWidth * perArt) / 2);
            int originY = (int)Math.Floor((viewHeight - artHeight * perArt) / 2);
            return (perArt / artScale, originX, originY);
        }

        /// <summary>A rectangle in map pixels to the screen space the scene paints in: scaled by the
        /// fit's <paramref name="scale"/> and offset by the map's top left corner on screen, with
        /// both edges rounded so neighbouring rectangles share an edge.</summary>
        public static (int X, int Y, int Width, int Height) MapToPaint(int x, int y, int width, int height, int originX, int originY, double scale)
        {
            int left = originX + (int)Math.Round(x * scale);
            int top = originY + (int)Math.Round(y * scale);
            int right = originX + (int)Math.Round((x + width) * scale);
            int bottom = originY + (int)Math.Round((y + height) * scale);
            return (left, top, right - left, bottom - top);
        }

        /// <summary>The map tab's frame round a map drawn at the given paint rectangle: its outer
        /// rectangle and the size of one corner piece, both in paint pixels.
        /// <paramref name="uiToPaint"/> is the same UI to paint factor the map used.</summary>
        public static (int X, int Y, int Width, int Height, int Piece) Frame(int mapX, int mapY, int mapWidth, int mapHeight, double uiToPaint)
        {
            int margin = (int)Math.Round(FrameMargin * uiToPaint);
            int piece = Math.Max(1, (int)Math.Round(FramePiece * uiToPaint));
            return (mapX - margin, mapY - margin, mapWidth + 2 * margin, mapHeight + 2 * margin, piece);
        }

        /// <summary>The whole screen as a rectangle in map pixels, for a fit that put the map's top
        /// left corner at (<paramref name="originX"/>, <paramref name="originY"/>) at
        /// <paramref name="scale"/> paint pixels per map pixel.</summary>
        public static (double X, double Y, double Width, double Height) ScreenInMap(int viewWidth, int viewHeight, int originX, int originY, double scale)
        {
            if (scale <= 0) throw new ArgumentOutOfRangeException(nameof(scale));
            return (-originX / scale, -originY / scale, viewWidth / scale, viewHeight / scale);
        }
    }
}
