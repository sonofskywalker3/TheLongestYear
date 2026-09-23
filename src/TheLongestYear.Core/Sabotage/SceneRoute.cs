using System;
using System.Collections.Generic;

namespace TheLongestYear.Core.Sabotage
{
    /// <summary>Cuts a walker's route down to the part a scene's camera can see (Jeff, 2026-09-23:
    /// Shane walks the town's own paths past the hall). The route itself comes from the game's
    /// pathfinder and can run the length of the map, and all a scene wants of it is the stretch in
    /// shot plus a few tiles out of shot at the open end, so the walker comes in over the edge of
    /// the frame and goes out over it rather than appearing and vanishing.
    ///
    /// The frame is the tiles WHOLLY in view. A tile the edge of the screen cuts through counts as
    /// out of shot, which is exactly why a few tiles past it are kept.</summary>
    public static class SceneRoute
    {
        /// <summary>The end of a route that finishes inside the frame: everything from
        /// <paramref name="lead"/> tiles before its last way in onward. A route that never leaves
        /// the frame, or that has fewer tiles out of shot than that, is kept from its start.</summary>
        public static IReadOnlyList<(int X, int Y)> IntoFrame(
            IReadOnlyList<(int X, int Y)> route, int left, int top, int width, int height, int lead)
        {
            if (route == null) throw new ArgumentNullException(nameof(route));
            if (lead < 1) throw new ArgumentOutOfRangeException(nameof(lead));
            int lastOutside = -1;
            for (int i = 0; i < route.Count; i++)
                if (!InFrame(route[i], left, top, width, height)) lastOutside = i;
            int start = lastOutside < 0 ? 0 : Math.Max(0, lastOutside - (lead - 1));
            return Slice(route, start, route.Count);
        }

        /// <summary>The start of a route that begins inside the frame: everything up to its first
        /// way out and <paramref name="tail"/> tiles beyond it. A route that never leaves the frame
        /// is kept whole.</summary>
        public static IReadOnlyList<(int X, int Y)> OutOfFrame(
            IReadOnlyList<(int X, int Y)> route, int left, int top, int width, int height, int tail)
        {
            if (route == null) throw new ArgumentNullException(nameof(route));
            if (tail < 1) throw new ArgumentOutOfRangeException(nameof(tail));
            for (int i = 0; i < route.Count; i++)
                if (!InFrame(route[i], left, top, width, height))
                    return Slice(route, 0, Math.Min(route.Count, i + tail));
            return Slice(route, 0, route.Count);
        }

        private static bool InFrame((int X, int Y) tile, int left, int top, int width, int height)
            => tile.X >= left && tile.X < left + width && tile.Y >= top && tile.Y < top + height;

        private static IReadOnlyList<(int X, int Y)> Slice(IReadOnlyList<(int X, int Y)> route, int from, int to)
        {
            var kept = new List<(int X, int Y)>(Math.Max(0, to - from));
            for (int i = from; i < to; i++) kept.Add(route[i]);
            return kept;
        }
    }
}
