using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core;

/// <summary>
/// Picks the planning hub's next re-rolled pair (Nijah, Nexus 2026-09-28: the button kept showing
/// the same two themes). A re-roll never shows a pair already shown this week (the first offer
/// counts as shown) until every pair of the current candidates has been shown; then the cycle
/// starts over, still skipping the pair on screen whenever another pair exists. Pure and
/// deterministic for a given <see cref="Random"/>.
/// </summary>
public static class RerollCycle
{
    private const string PairSeparator = "|";

    /// <summary>The order-free key for a shown offer: the theme names sorted and joined,
    /// e.g. "Farming|Fishing". A one-card offer is just that theme's name.</summary>
    public static string PairKey(IEnumerable<Theme> pair)
        => string.Join(PairSeparator, (pair ?? Array.Empty<Theme>())
            .Select(t => t.ToString())
            .OrderBy(n => n, StringComparer.Ordinal));

    /// <summary>
    /// The next offer from <paramref name="candidates"/>. <paramref name="seen"/> holds the pair
    /// keys shown this week and is updated in place: cleared when the cycle starts over, and the
    /// returned pair's key added. Fewer than <see cref="SelectionService.OfferSize"/> candidates
    /// return what exists; a single possible pair is returned every time.
    /// </summary>
    public static IReadOnlyList<Theme> Next(
        IReadOnlyList<Theme> candidates, ICollection<string> seen, IReadOnlyList<Theme> current, Random rng)
    {
        if (seen == null) throw new ArgumentNullException(nameof(seen));
        if (rng == null) throw new ArgumentNullException(nameof(rng));
        List<Theme> pool = (candidates ?? Array.Empty<Theme>()).Distinct().ToList();
        if (pool.Count < SelectionService.OfferSize) return pool;

        var pairs = new List<Theme[]>();
        for (int i = 0; i < pool.Count; i++)
            for (int j = i + 1; j < pool.Count; j++)
                pairs.Add(new[] { pool[i], pool[j] });

        List<Theme[]> fresh = pairs.Where(p => !seen.Contains(PairKey(p))).ToList();
        if (fresh.Count == 0)
        {
            // Every pair has been shown: start over, counting the pair on screen as shown.
            seen.Clear();
            string onScreen = PairKey(current ?? Array.Empty<Theme>());
            seen.Add(onScreen);
            fresh = pairs.Where(p => PairKey(p) != onScreen).ToList();
            if (fresh.Count == 0) fresh = pairs;
        }

        Theme[] pick = fresh[rng.Next(fresh.Count)];
        if (rng.Next(2) == 1) pick = new[] { pick[1], pick[0] };
        string key = PairKey(pick);
        if (!seen.Contains(key)) seen.Add(key);
        return pick;
    }
}
