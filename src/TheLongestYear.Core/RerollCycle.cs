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

    /// <summary>True when <see cref="Next"/> can return a pair other than <paramref name="current"/>:
    /// some offer from <paramref name="candidates"/> has a different <see cref="PairKey"/>. A paid
    /// reroll that cannot change the offer must not charge (final review T2).</summary>
    public static bool CanChange(
        IReadOnlyList<Theme> candidates, IReadOnlyList<Theme> current, IReadOnlyCollection<Theme>? picked = null)
    {
        List<Theme> pool = (candidates ?? Array.Empty<Theme>()).Distinct().ToList();
        string onScreen = PairKey(current ?? Array.Empty<Theme>());
        if (pool.Count < SelectionService.OfferSize) return PairKey(pool) != onScreen;
        return Pairs(pool, picked).Any(p => PairKey(p) != onScreen);
    }

    /// <summary>
    /// The next offer from <paramref name="candidates"/>. <paramref name="seen"/> holds the pair
    /// keys shown this week and is updated in place: cleared when the cycle starts over, and the
    /// returned pair's key added. Fewer than <see cref="SelectionService.OfferSize"/> candidates
    /// return what exists; a single possible pair is returned every time. When fewer than a pair
    /// of the candidates are outside <paramref name="picked"/> (this month's picks, refilled into
    /// the pool), every pair keeps those unpicked ones: a re-roll never hides an unpicked theme
    /// behind two reused ones, the same order the weekly offer deals in.
    /// </summary>
    public static IReadOnlyList<Theme> Next(
        IReadOnlyList<Theme> candidates, ICollection<string> seen, IReadOnlyList<Theme> current, Random rng,
        IReadOnlyCollection<Theme>? picked = null)
    {
        if (seen == null) throw new ArgumentNullException(nameof(seen));
        if (rng == null) throw new ArgumentNullException(nameof(rng));
        List<Theme> pool = (candidates ?? Array.Empty<Theme>()).Distinct().ToList();
        if (pool.Count < SelectionService.OfferSize) return pool;

        List<Theme[]> pairs = Pairs(pool, picked);

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

    /// <summary>Every pair of <paramref name="pool"/>, in pool order. When some but fewer than a
    /// pair of the pool are not in <paramref name="picked"/>, only the pairs holding all of them.</summary>
    private static List<Theme[]> Pairs(List<Theme> pool, IReadOnlyCollection<Theme>? picked)
    {
        List<Theme> unpicked = picked == null ? pool : pool.Where(t => !picked.Contains(t)).ToList();
        bool keepUnpicked = unpicked.Count > 0 && unpicked.Count < SelectionService.OfferSize;
        var pairs = new List<Theme[]>();
        for (int i = 0; i < pool.Count; i++)
            for (int j = i + 1; j < pool.Count; j++)
                if (!keepUnpicked || unpicked.All(t => t == pool[i] || t == pool[j]))
                    pairs.Add(new[] { pool[i], pool[j] });
        return pairs;
    }
}