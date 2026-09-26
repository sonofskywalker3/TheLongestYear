using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core.Joja;

/// <summary>Seeded, irregular scatter for the bad ending's litter (dead fish, driftwood, trash) and
/// the animals' starting spots: no rows, no grid, never two picks closer than a minimum spacing,
/// and the same layout every run for the same seed and candidates (Jeff, 2026-09-25: "neglected,
/// not a junkyard").</summary>
public static class JojaScatter
{
    /// <summary>Up to <paramref name="count"/> tiles from <paramref name="candidates"/>, each at least
    /// <paramref name="spacing"/> tiles (straight-line) from every other pick. The candidates are
    /// sorted first, so their order never changes the result.</summary>
    public static IReadOnlyList<(int X, int Y)> Pick(IEnumerable<(int X, int Y)> candidates, int count, double spacing, int seed)
    {
        if (candidates == null) throw new ArgumentNullException(nameof(candidates));
        var pool = candidates.Distinct().OrderBy(c => c.Y).ThenBy(c => c.X).ToList();
        var rng = new Random(seed);
        // Fisher-Yates with the seeded generator.
        for (int i = pool.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (pool[i], pool[j]) = (pool[j], pool[i]);
        }
        double min2 = spacing * spacing;
        var picks = new List<(int X, int Y)>();
        foreach (var c in pool)
        {
            if (picks.Count >= count) break;
            bool clear = true;
            foreach (var p in picks)
            {
                double dx = p.X - c.X, dy = p.Y - c.Y;
                if (dx * dx + dy * dy < min2) { clear = false; break; }
            }
            if (clear) picks.Add(c);
        }
        return picks;
    }
}
