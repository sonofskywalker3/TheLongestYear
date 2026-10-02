using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core;

/// <summary>
/// Where an item that has nowhere else to go lands on the ground (spec Addendum 2): the nearest
/// open tile to a start tile. Tiles are tried ring by ring (Chebyshev distance), and within a ring
/// by straight-line distance, so a side neighbour comes before a corner one. Ties keep a fixed
/// order (top to bottom, left to right) so the same farm always gives the same spot.
/// </summary>
public static class DropSpot
{
    /// <summary>The nearest tile to <paramref name="start"/> (itself included) for which
    /// <paramref name="isOpen"/> holds, at most <paramref name="radius"/> rings out; null when none.</summary>
    public static DecorTile? Nearest(DecorTile start, Func<int, int, bool> isOpen, int radius)
    {
        if (isOpen(start.X, start.Y))
            return start;
        for (int r = 1; r <= radius; r++)
            foreach (DecorTile t in Ring(start, r))
                if (isOpen(t.X, t.Y))
                    return t;
        return null;
    }

    /// <summary>A displaced decor piece: its own first open tile when it has one, else the nearest
    /// open tile to the tile that blocked it.</summary>
    public static DecorTile? ForDisplaced(IReadOnlyList<DecorTile> pieceTiles, DecorTile blocker,
        Func<int, int, bool> isOpen, int radius)
    {
        foreach (DecorTile t in pieceTiles)
            if (isOpen(t.X, t.Y))
                return t;
        return Nearest(blocker, isOpen, radius);
    }

    private static IEnumerable<DecorTile> Ring(DecorTile start, int r)
    {
        var ring = new List<DecorTile>();
        for (int dy = -r; dy <= r; dy++)
            for (int dx = -r; dx <= r; dx++)
                if (Math.Max(Math.Abs(dx), Math.Abs(dy)) == r)
                    ring.Add(new DecorTile(start.X + dx, start.Y + dy));
        return ring.OrderBy(t => DistanceSquared(start, t));
    }

    private static int DistanceSquared(DecorTile a, DecorTile b)
        => (a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y);
}
