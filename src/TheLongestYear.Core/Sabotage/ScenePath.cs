using System;
using System.Collections.Generic;

namespace TheLongestYear.Core.Sabotage;

/// <summary>Finding a walk for a figure in an overnight strike scene (spec 2026-09-21): the thief
/// comes in to the chest the darkness picked, stops on the tile beside it, and leaves the same way.
///
/// Pure tile arithmetic over a passability grid, so no game types are involved and the walk a scene
/// chooses can be tested without a running farm. The grid is indexed <c>passable[x, y]</c> and the
/// caller decides what passable means: the scene's own rule is stricter than the game's, because a
/// thief who clips a fence post or treads on a crop is worse than a thief who walks in from
/// somewhere else.
///
/// The search runs OUTWARD from the tiles beside the target rather than inward from a door. One
/// breadth-first sweep then answers both questions a scene has: which of the ways in is nearest,
/// and, when they are all too far or walled off, where to put him so he is simply already there
/// when the scene opens.</summary>
public static class ScenePath
{
    /// <summary>Plan the walk in to <paramref name="target"/>. The result runs from the tile he
    /// starts on to the tile beside the target, both included, so the scene walks it forwards on
    /// the way in and backwards on the way out. Empty when there is no passable tile beside the
    /// target at all, and then the scene has nowhere to stage him.</summary>
    /// <param name="passable">Where he may stand. The target's own tile is never walked on.</param>
    /// <param name="target">The chest or machine he is coming for.</param>
    /// <param name="preferredStarts">The ways in, best first in the caller's own order: a door
    /// tile indoors, the map's edge tiles outdoors. The nearest one that can be reached wins.</param>
    /// <param name="maxPreferredDistance">How far a way in may be and still be used. Further than
    /// this and he is simply already inside, at <paramref name="fallbackDistance"/>.</param>
    /// <param name="fallbackDistance">How far from the target to start him when no way in is close
    /// enough or reachable. The nearest walk to that length is taken.</param>
    public static IReadOnlyList<(int X, int Y)> WalkTo(
        bool[,] passable,
        (int X, int Y) target,
        IReadOnlyList<(int X, int Y)> preferredStarts,
        int maxPreferredDistance,
        int fallbackDistance)
    {
        if (passable is null) throw new ArgumentNullException(nameof(passable));
        if (preferredStarts is null) throw new ArgumentNullException(nameof(preferredStarts));
        if (maxPreferredDistance < 0) throw new ArgumentOutOfRangeException(nameof(maxPreferredDistance));
        if (fallbackDistance < 0) throw new ArgumentOutOfRangeException(nameof(fallbackDistance));

        int width = passable.GetLength(0);
        int height = passable.GetLength(1);
        var distance = new Dictionary<(int X, int Y), int>();
        var parent = new Dictionary<(int X, int Y), (int X, int Y)>();
        var queue = new Queue<(int X, int Y)>();

        // The target's own tile is never walked on, whatever the grid says. A caller's grid answers
        // "may he stand here", and a chest or a machine tile can come back true for reasons of its
        // own, but a walk that goes straight over the thing being robbed is not a walk.
        bool Walkable((int X, int Y) tile)
            => tile != target && InBounds(tile, width, height) && passable[tile.X, tile.Y];

        // The seeds are the tiles beside the target, in a fixed order so an equally good walk is
        // always the same walk.
        foreach ((int X, int Y) beside in Neighbours(target))
        {
            if (!Walkable(beside) || distance.ContainsKey(beside)) continue;
            distance[beside] = 0;
            queue.Enqueue(beside);
        }
        if (distance.Count == 0) return Array.Empty<(int X, int Y)>();

        while (queue.Count > 0)
        {
            (int X, int Y) at = queue.Dequeue();
            int next = distance[at] + 1;
            foreach ((int X, int Y) step in Neighbours(at))
            {
                if (!Walkable(step) || distance.ContainsKey(step)) continue;
                distance[step] = next;
                parent[step] = at;
                queue.Enqueue(step);
            }
        }

        (int X, int Y) start = default;
        bool found = false;
        int best = int.MaxValue;
        foreach ((int X, int Y) candidate in preferredStarts)
        {
            if (!distance.TryGetValue(candidate, out int howFar) || howFar > maxPreferredDistance) continue;
            if (found && howFar >= best) continue;
            start = candidate;
            best = howFar;
            found = true;
        }
        if (!found)
        {
            int closest = int.MaxValue;
            foreach (KeyValuePair<(int X, int Y), int> pair in distance)
            {
                int off = Math.Abs(pair.Value - fallbackDistance);
                if (found && !Better(off, pair, closest, best, start)) continue;
                start = pair.Key;
                best = pair.Value;
                closest = off;
                found = true;
            }
        }
        if (!found) return Array.Empty<(int X, int Y)>();

        var walk = new List<(int X, int Y)> { start };
        (int X, int Y) on = start;
        while (parent.TryGetValue(on, out (int X, int Y) towards))
        {
            on = towards;
            walk.Add(on);
        }
        return walk;
    }

    /// <summary>Keep the walk to at most <paramref name="maxTiles"/> steps by dropping the far end
    /// of it: he starts closer instead of the scene running long. The tile beside the target is
    /// always kept, because that is where the scene needs him.</summary>
    public static IReadOnlyList<(int X, int Y)> Trim(IReadOnlyList<(int X, int Y)> walk, int maxTiles)
    {
        if (walk is null) throw new ArgumentNullException(nameof(walk));
        if (maxTiles < 0) throw new ArgumentOutOfRangeException(nameof(maxTiles));
        int keep = maxTiles + 1;
        if (walk.Count <= keep) return walk;
        var trimmed = new List<(int X, int Y)>(keep);
        for (int i = walk.Count - keep; i < walk.Count; i++) trimmed.Add(walk[i]);
        return trimmed;
    }

    /// <summary>The fallback tie-break, kept in one place: nearest to the wanted distance first,
    /// then the shorter walk, then the smaller tile. Deterministic, because a dictionary sweep is
    /// not.</summary>
    private static bool Better(int off, KeyValuePair<(int X, int Y), int> pair, int closest, int best, (int X, int Y) start)
    {
        if (off != closest) return off < closest;
        if (pair.Value != best) return pair.Value < best;
        if (pair.Key.X != start.X) return pair.Key.X < start.X;
        return pair.Key.Y < start.Y;
    }

    private static bool InBounds((int X, int Y) tile, int width, int height)
        => tile.X >= 0 && tile.Y >= 0 && tile.X < width && tile.Y < height;

    private static (int X, int Y)[] Neighbours((int X, int Y) tile) => new[]
    {
        (tile.X, tile.Y - 1),
        (tile.X + 1, tile.Y),
        (tile.X, tile.Y + 1),
        (tile.X - 1, tile.Y),
    };
}
