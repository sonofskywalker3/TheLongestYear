using System;
using System.Collections.Generic;

namespace TheLongestYear.Core.Sabotage;

/// <summary>Tile by tile routes for a scene's walker, built from a designed list of corners and
/// checked against the map (Jeff, 2026-09-25: "You've gotta stop making up your own paths").
///
/// A scripted walk ignores collision, so a route is only as good as the tiles it names. The rule
/// this class serves is that every tile a walker steps on was read off the map's own data as a
/// path, and that a fallback route is held to the same rule: <see cref="Between"/> searches ONLY
/// the tiles the caller's grid calls path, so it can never cut across a lawn.</summary>
public static class TileRoute
{
    /// <summary>Expand a list of corners into every tile walked, first corner first. Each leg
    /// must be straight (same column or same row), because a diagonal leg would leave the tiles
    /// between its corners to whoever draws it.</summary>
    public static IReadOnlyList<(int X, int Y)> Expand(IReadOnlyList<(int X, int Y)> corners)
    {
        if (corners is null) throw new ArgumentNullException(nameof(corners));
        if (corners.Count == 0) throw new ArgumentException("A route needs at least one corner.", nameof(corners));
        var tiles = new List<(int X, int Y)> { corners[0] };
        for (int i = 1; i < corners.Count; i++)
        {
            (int x, int y) = tiles[tiles.Count - 1];
            (int X, int Y) to = corners[i];
            if (x != to.X && y != to.Y)
                throw new ArgumentException($"The leg from ({x},{y}) to ({to.X},{to.Y}) is diagonal.", nameof(corners));
            while (x != to.X || y != to.Y)
            {
                x += Math.Sign(to.X - x);
                y += Math.Sign(to.Y - y);
                tiles.Add((x, y));
            }
        }
        return tiles;
    }

    /// <summary>The index of the first tile that is not path, or -1 when every tile is.</summary>
    public static int FirstOffPath(IReadOnlyList<(int X, int Y)> route, Func<int, int, bool> isPath)
    {
        if (route is null) throw new ArgumentNullException(nameof(route));
        if (isPath is null) throw new ArgumentNullException(nameof(isPath));
        for (int i = 0; i < route.Count; i++)
            if (!isPath(route[i].X, route[i].Y)) return i;
        return -1;
    }

    /// <summary>The shortest walk from <paramref name="from"/> to <paramref name="to"/> that steps
    /// only on tiles <paramref name="path"/> marks true (indexed <c>[x, y]</c>), both ends
    /// included, in orthogonal steps. Empty when either end is not path or no such walk exists.
    /// Neighbours are tried in a fixed order so an equally short walk is always the same walk.</summary>
    public static IReadOnlyList<(int X, int Y)> Between(bool[,] path, (int X, int Y) from, (int X, int Y) to)
    {
        if (path is null) throw new ArgumentNullException(nameof(path));
        int width = path.GetLength(0);
        int height = path.GetLength(1);
        bool IsPath((int X, int Y) t) => t.X >= 0 && t.Y >= 0 && t.X < width && t.Y < height && path[t.X, t.Y];
        if (!IsPath(from) || !IsPath(to)) return Array.Empty<(int X, int Y)>();

        var parent = new Dictionary<(int X, int Y), (int X, int Y)> { [from] = from };
        var queue = new Queue<(int X, int Y)>();
        queue.Enqueue(from);
        while (queue.Count > 0)
        {
            (int X, int Y) at = queue.Dequeue();
            if (at == to) break;
            foreach ((int dx, int dy) in Steps)
            {
                (int X, int Y) next = (at.X + dx, at.Y + dy);
                if (!IsPath(next) || parent.ContainsKey(next)) continue;
                parent[next] = at;
                queue.Enqueue(next);
            }
        }
        if (!parent.ContainsKey(to)) return Array.Empty<(int X, int Y)>();

        var walk = new List<(int X, int Y)>();
        for ((int X, int Y) at = to; ; at = parent[at])
        {
            walk.Add(at);
            if (at == from) break;
        }
        walk.Reverse();
        return walk;
    }

    /// <summary>The last <paramref name="steps"/> steps of a route (so <paramref name="steps"/>
    /// plus one tiles), or the whole of it when it is that short already. A walker with less time
    /// than his route needs starts further along it rather than walking it faster.</summary>
    public static IReadOnlyList<(int X, int Y)> LastSteps(IReadOnlyList<(int X, int Y)> route, int steps)
    {
        if (route is null) throw new ArgumentNullException(nameof(route));
        if (steps < 0) throw new ArgumentOutOfRangeException(nameof(steps));
        int from = Math.Max(0, route.Count - 1 - steps);
        var kept = new List<(int X, int Y)>(route.Count - from);
        for (int i = from; i < route.Count; i++) kept.Add(route[i]);
        return kept;
    }

    private static readonly (int Dx, int Dy)[] Steps = { (0, -1), (1, 0), (0, 1), (-1, 0) };
}
