using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core;

/// <summary>
/// Per-(run, week, theme) random sample of "items in play this week" from the theme's bundles —
/// the bonus list that earns the 1.5× ChampionBonusMultiplier when donated. Pure and
/// seed-deterministic, so the planning hub's per-card preview and the championing-time commit
/// produce the same list for the same (seed, week, theme).
///
/// Sampling rule (per bundle, see <see cref="BundleRequirement.InPlayItemsFor"/>):
/// <list type="bullet">
///   <item>Seasonal: all ingredients when the season matches; nothing otherwise.</item>
///   <item>PerItem: ingredients pinned to this season.</item>
///   <item>Percentage: ingredients that pass the obtainability predicate (CcItem.IsObtainableIn).</item>
/// </list>
/// </summary>
public static class BonusItemSampler
{
    private const int WeekSaltPrime = 7919;
    private const int ThemeSaltPrime = 1031;

    /// <summary>
    /// Sample up to <paramref name="maxCount"/> bonus items for <paramref name="theme"/> in
    /// <paramref name="currentSeason"/>. Returns a stable order for a given (seed, week, theme).
    /// If the pool is smaller than <paramref name="maxCount"/>, returns the whole pool (shuffled).
    /// </summary>
    public static IReadOnlyList<string> SampleForTheme(
        int runSeed, int weekOfYear,
        Theme theme,
        Season currentSeason,
        IReadOnlyList<BundleRequirement> bundles,
        Func<string, bool> isObtainableInSeason,
        int maxCount)
    {
        if (bundles is null) throw new ArgumentNullException(nameof(bundles));
        if (isObtainableInSeason is null) throw new ArgumentNullException(nameof(isObtainableInSeason));
        if (maxCount <= 0) return Array.Empty<string>();

        // Pool: distinct in-play items across the theme's bundles.
        HashSet<string> pool = new HashSet<string>();
        foreach (BundleRequirement b in bundles)
        {
            if (b.Theme != theme) continue;
            foreach (string id in b.InPlayItemsFor(currentSeason, isObtainableInSeason))
                pool.Add(id);
        }

        if (pool.Count == 0) return Array.Empty<string>();

        // Deterministic shuffle keyed by (runSeed, week, theme). Sorting first gives a stable
        // pre-shuffle order so the seeded shuffle reproduces across runs of the same input.
        List<string> ordered = pool.OrderBy(s => s, StringComparer.Ordinal).ToList();
        Random rng = new Random(runSeed ^ (weekOfYear * WeekSaltPrime) ^ ((int)theme * ThemeSaltPrime));
        for (int i = ordered.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (ordered[i], ordered[j]) = (ordered[j], ordered[i]);
        }

        return ordered.Take(maxCount).ToList();
    }
}
