using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core;

/// <summary>Where the Junimos stand in a porch scene (the Summer, Fall and Winter season turns and
/// the tamper scene): on the grass below the porch, facing the farmer on the step. Every mark is a
/// tile relative to the farm's door tile (the farmer stands one below it).
///
/// The marks used to be fixed offsets, so whatever the player or the mod put near the porch could
/// cover them: the planning shrine sits on (-2,3) on an auto-placed farm and hid a Junimo whole in
/// the Winter scene, and the Junimo Stash at (3,3) pressed one against its side (designer,
/// 2026-10-09). Now a mark must be clear: nothing on its tile or drawn over it, and nothing on the
/// tile above it either, so no Junimo stands under a chest or a statue. A Junimo's sprite is 48
/// pixels (16 at scale 0.75), inside its own tile at rest; its jumps lift it about a tile, into the
/// tile above, which is the other reason that tile must be free.
///
/// The choice, first that fits: the scene's own layout; then an even row centred on the farmer
/// (equal spacing, the same distance either side; designer, picky about it), trying the rows
/// below the porch nearest first and, in each row, a two-tile gap, three, one, then four; then,
/// for four Junimos, a triangle (three across under the farmer, one below him); then an even count's row half a tile off centre (equal gaps, Junimo 0 still under
/// the farmer); and only then each mark of the scene's layout moves to the nearest clear tile.</summary>
public static class PorchMarks
{
    /// <summary>The first row the Junimos may stand on: the grass just below the porch.</summary>
    public const int FirstRow = 3;

    /// <summary>The lowest row a Junimo may stand on. Row 5 is under the top of the speech box at
    /// the scene's viewport (live, 2026-10-09: the box cut the feet off a pair moved there).</summary>
    public const int LastRow = 4;

    /// <summary>The gaps between neighbours an even row tries, in order.</summary>
    private static readonly int[] Gaps = { 2, 3, 1, 4 };

    /// <summary>How far a single mark may move to find a clear tile in the last fallback.</summary>
    public const int FallbackRadius = 6;

    // The scene layouts (Jeff, 2026-09-07 and 2026-10-08): A on the path below the farmer, B and C
    // to either side, D out wide; a pair stands one tile either side of him.
    private static readonly (int X, int Y)[] Pair = { (1, 3), (-1, 3) };
    private static readonly (int X, int Y)[] Wide = { (0, 3), (-2, 3), (2, 3), (-4, 4) };

    /// <summary>The scene's own layout for this many Junimos, before anything is checked.</summary>
    public static IReadOnlyList<(int X, int Y)> Default(int count)
        => count == Pair.Length ? Pair : Wide.Take(Math.Max(0, Math.Min(count, Wide.Length))).ToArray();

    /// <summary>Can a Junimo stand here: nothing on the tile or drawn over it, and nothing on the
    /// tile above. <paramref name="blocked"/> is absolute tiles.</summary>
    public static bool IsClear(int x, int y, Func<int, int, bool> blocked)
        => !blocked(x, y) && !blocked(x, y - 1);

    /// <summary>The Junimos' absolute tiles, in Junimo order (0 is the green voice), for a scene
    /// staged at the door (<paramref name="doorX"/>, <paramref name="doorY"/>).</summary>
    public static IReadOnlyList<(int X, int Y)> Choose(int doorX, int doorY, int count, ICollection<(int X, int Y)> blocked)
        => Choose(doorX, doorY, count, (x, y) => blocked.Contains((x, y)));

    /// <inheritdoc cref="Choose(int,int,int,ICollection{ValueTuple{int,int}})"/>
    public static IReadOnlyList<(int X, int Y)> Choose(int doorX, int doorY, int count, Func<int, int, bool> blocked)
    {
        if (count <= 0) return Array.Empty<(int X, int Y)>();
        blocked ??= (_, _) => false;
        IReadOnlyList<(int X, int Y)> layout = Default(count);
        if (AllClear(layout, doorX, doorY, blocked)) return Absolute(layout, doorX, doorY);
        for (int row = FirstRow; row <= LastRow; row++)
            foreach (int gap in Gaps)
            {
                IReadOnlyList<(int X, int Y)> even = EvenRow(count, row, gap);
                if (even != null && AllClear(even, doorX, doorY, blocked)) return Absolute(even, doorX, doorY);
            }
        // Four Junimos that fit no even row: three across under the farmer and the fourth one row
        // lower, centred under him; symmetric on him with nothing moved (designer, 2026-10-09).
        foreach (int gap in TriangleGaps)
        {
            IReadOnlyList<(int X, int Y)> triangle = Triangle(count, gap);
            if (triangle != null && AllClear(triangle, doorX, doorY, blocked)) return Absolute(triangle, doorX, doorY);
        }
        // Four Junimos cannot stand centred shoulder to shoulder on whole tiles: an even count with
        // an odd gap stands half a tile off centre, Junimo 0 still under the farmer.
        for (int row = FirstRow; row <= LastRow; row++)
            foreach (int gap in NearEvenGaps)
                foreach (bool mirrored in new[] { false, true })
                {
                    IReadOnlyList<(int X, int Y)> near = NearEvenRow(count, row, gap, mirrored);
                    if (near != null && AllClear(near, doorX, doorY, blocked)) return Absolute(near, doorX, doorY);
                }
        return Nearest(layout, doorX, doorY, blocked);
    }

    /// <summary>The gaps the four-Junimo triangle tries, in order.</summary>
    private static readonly int[] TriangleGaps = { 1, 2 };

    /// <summary>Four Junimos as a triangle: Junimo 0 under the farmer on the first row, 1 and 2
    /// <paramref name="gap"/> to his left and right, 3 one row lower under him. Null for any other
    /// count.</summary>
    public static IReadOnlyList<(int X, int Y)> Triangle(int count, int gap)
    {
        const int TriangleCount = 4;
        if (count != TriangleCount || gap <= 0) return null;
        return new[] { (0, FirstRow), (-gap, FirstRow), (gap, FirstRow), (0, FirstRow + 1) };
    }

    /// <summary>The odd gaps a near-even row tries, in order.</summary>
    private static readonly int[] NearEvenGaps = { 1, 3 };

    /// <summary>An even count with an odd gap: the odd row one Junimo longer (Junimo 0 under the
    /// farmer) less its last, outermost Junimo, so the gaps stay equal and the row sits half a tile
    /// off centre; <paramref name="mirrored"/> takes the other side. Null for an odd count (it has
    /// <see cref="EvenRow"/>) or an even gap.</summary>
    public static IReadOnlyList<(int X, int Y)> NearEvenRow(int count, int row, int gap, bool mirrored)
    {
        if (count <= 0 || count % 2 == 1 || gap <= 0 || gap % 2 == 0) return null;
        IReadOnlyList<(int X, int Y)> longer = EvenRow(count + 1, row, gap);
        return longer.Take(count).Select(m => (mirrored ? -m.X : m.X, m.Y)).ToArray();
    }

    /// <summary>An even row of <paramref name="count"/> centred on the farmer's column, neighbours
    /// <paramref name="gap"/> apart, in Junimo order: an odd count puts Junimo 0 in the middle and the
    /// rest alternate left then right going out; an even count puts Junimo 0 just right of the
    /// middle and 1 just left (the pair). Null when the gap cannot be centred on whole tiles (an
    /// even count needs an even gap).</summary>
    public static IReadOnlyList<(int X, int Y)> EvenRow(int count, int row, int gap)
    {
        if (count <= 0 || gap <= 0) return null;
        var marks = new List<(int X, int Y)>();
        if (count % 2 == 1)
        {
            marks.Add((0, row));
            for (int k = 1; marks.Count < count; k++)
            {
                marks.Add((-k * gap, row));
                if (marks.Count < count) marks.Add((k * gap, row));
            }
            return marks;
        }
        if (gap % 2 != 0) return null;
        int half = gap / 2;
        for (int k = 0; marks.Count < count; k++)
        {
            int offset = half + k * gap;
            marks.Add((offset, row));
            marks.Add((-offset, row));
        }
        return marks;
    }

    private static bool AllClear(IReadOnlyList<(int X, int Y)> marks, int doorX, int doorY, Func<int, int, bool> blocked)
    {
        foreach ((int x, int y) in marks)
            if (!IsClear(doorX + x, doorY + y, blocked)) return false;
        return true;
    }

    private static IReadOnlyList<(int X, int Y)> Absolute(IReadOnlyList<(int X, int Y)> marks, int doorX, int doorY)
        => marks.Select(m => (doorX + m.X, doorY + m.Y)).ToArray();

    /// <summary>Last resort: each mark of the layout stays if it is clear, else moves to the nearest
    /// clear tile not taken, on the rows the Junimos may use (nearest by steps; ties go to the same row,
    /// then the nearer side of the farmer, then left). A mark with no clear tile in reach stays.</summary>
    private static IReadOnlyList<(int X, int Y)> Nearest(IReadOnlyList<(int X, int Y)> layout, int doorX, int doorY, Func<int, int, bool> blocked)
    {
        var taken = new HashSet<(int X, int Y)>();
        var result = new List<(int X, int Y)>();
        foreach ((int mx, int my) in layout)
        {
            (int X, int Y) mark = (doorX + mx, doorY + my);
            (int X, int Y)? best = null;
            int bestScore = int.MaxValue;
            for (int dy = -FallbackRadius; dy <= FallbackRadius; dy++)
                for (int dx = -FallbackRadius; dx <= FallbackRadius; dx++)
                {
                    (int X, int Y) t = (mark.X + dx, mark.Y + dy);
                    if (t.Y < doorY + FirstRow || t.Y > doorY + LastRow || taken.Contains(t) || !IsClear(t.X, t.Y, blocked)) continue;
                    int steps = Math.Abs(dx) + Math.Abs(dy);
                    if (steps > FallbackRadius) continue;
                    int score = steps * 10000 + Math.Abs(dy) * 1000 + Math.Abs(t.X - doorX) * 10 + (dx < 0 ? 0 : 1);
                    if (score < bestScore) { bestScore = score; best = t; }
                }
            (int X, int Y) chosen = best ?? mark;
            taken.Add(chosen);
            result.Add(chosen);
        }
        return result;
    }
}
