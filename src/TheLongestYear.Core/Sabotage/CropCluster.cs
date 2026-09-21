using System;
using System.Collections.Generic;

namespace TheLongestYear.Core.Sabotage;

/// <summary>Grouping the crop tiles a strike has already picked, so an overnight scene can point
/// its camera at the thickest patch instead of at a lone corner plant (spec 2026-09-21).
///
/// Pure tile arithmetic on plain integer pairs: no game types, so the choice a scene makes can be
/// tested without a running farm.</summary>
public static class CropCluster
{
    /// <summary>Group the tiles and return the biggest group. A tile joins a group when it is
    /// within <paramref name="joinDistance"/> tiles of the tile that OPENED that group, which is
    /// what the spec asks for and keeps a group from growing along a hedgerow forever. Tiles are
    /// offered to the groups in the order they arrive, and a tie is won by the group that opened
    /// first, so the same pick always gives the same frame.</summary>
    public static IReadOnlyList<(int X, int Y)> Largest(IReadOnlyList<(int X, int Y)> tiles, int joinDistance)
    {
        if (tiles is null) throw new ArgumentNullException(nameof(tiles));
        if (joinDistance < 0) throw new ArgumentOutOfRangeException(nameof(joinDistance));
        var groups = new List<List<(int X, int Y)>>();
        long reach = (long)joinDistance * joinDistance;
        foreach ((int X, int Y) tile in tiles)
        {
            bool joined = false;
            foreach (List<(int X, int Y)> group in groups)
            {
                if (SquaredDistance(group[0], tile) > reach) continue;
                group.Add(tile);
                joined = true;
                break;
            }
            if (!joined) groups.Add(new List<(int X, int Y)> { tile });
        }
        List<(int X, int Y)>? biggest = null;
        foreach (List<(int X, int Y)> group in groups)
            if (biggest is null || group.Count > biggest.Count) biggest = group;
        return biggest ?? (IReadOnlyList<(int X, int Y)>)Array.Empty<(int X, int Y)>();
    }

    /// <summary>The middle of a group of tiles, rounded to the nearest whole tile.</summary>
    public static (int X, int Y) Centroid(IReadOnlyList<(int X, int Y)> tiles)
    {
        if (tiles is null) throw new ArgumentNullException(nameof(tiles));
        if (tiles.Count == 0) throw new ArgumentException("No tiles to average.", nameof(tiles));
        long x = 0, y = 0;
        foreach ((int X, int Y) tile in tiles) { x += tile.X; y += tile.Y; }
        return ((int)Math.Round(x / (double)tiles.Count), (int)Math.Round(y / (double)tiles.Count));
    }

    /// <summary>The <paramref name="count"/> tiles closest to <paramref name="to"/>, nearest
    /// first. Equally distant tiles keep the order they came in.</summary>
    public static IReadOnlyList<(int X, int Y)> Nearest(IReadOnlyList<(int X, int Y)> tiles, (int X, int Y) to, int count)
    {
        if (tiles is null) throw new ArgumentNullException(nameof(tiles));
        if (count <= 0) return Array.Empty<(int X, int Y)>();
        var ranked = new List<(long Distance, int Order, (int X, int Y) Tile)>();
        for (int i = 0; i < tiles.Count; i++) ranked.Add((SquaredDistance(tiles[i], to), i, tiles[i]));
        ranked.Sort((a, b) => a.Distance != b.Distance ? a.Distance.CompareTo(b.Distance) : a.Order.CompareTo(b.Order));
        var picked = new List<(int X, int Y)>();
        foreach ((long _, int _, (int X, int Y) tile) in ranked)
        {
            if (picked.Count >= count) break;
            picked.Add(tile);
        }
        return picked;
    }

    private static long SquaredDistance((int X, int Y) a, (int X, int Y) b)
    {
        long dx = a.X - b.X;
        long dy = a.Y - b.Y;
        return dx * dx + dy * dy;
    }
}
