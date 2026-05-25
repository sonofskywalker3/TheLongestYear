using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core;

/// <summary>
/// Produces the weekly 1-of-2 champion offer (spec §4). The offer is a pure, deterministic function of
/// the run's seed, the current week-of-year, and which themes are already championed this month — so it
/// is stable across re-queries within a week and across reloads.
/// </summary>
public static class ChampionService
{
    private const int WeekSaltPrime = 7919;

    /// <summary>The number of themes offered each week.</summary>
    public const int OfferSize = 2;

    /// <summary>
    /// Up to <see cref="OfferSize"/> distinct themes not yet championed this month, seeded-deterministic.
    /// (Plan 06 will further restrict to themes completable that week.)
    /// </summary>
    public static IReadOnlyList<Theme> OfferForWeek(RunState run)
    {
        if (run is null)
            throw new ArgumentNullException(nameof(run));

        // Stable candidate order, then a seeded shuffle keyed by (seed, week).
        List<Theme> candidates = Enum.GetValues(typeof(Theme))
            .Cast<Theme>()
            .Where(t => !run.IsChampioned(t))
            .OrderBy(t => (int)t)
            .ToList();

        var rng = new Random(run.Seed ^ (run.WeekOfYear * WeekSaltPrime));
        for (int i = candidates.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
        }

        return candidates.Take(OfferSize).ToList();
    }
}
