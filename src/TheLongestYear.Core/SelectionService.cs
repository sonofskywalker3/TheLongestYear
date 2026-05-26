using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core;

/// <summary>
/// Produces the weekly 1-of-2 selection offer (spec §4). The offer is a pure, deterministic
/// function of the run's seed, the current week-of-year, and which themes are already selected
/// this month — so it is stable across re-queries within a week and across reloads.
/// </summary>
public static class SelectionService
{
    private const int WeekSaltPrime = 7919;

    /// <summary>The number of themes offered each week.</summary>
    public const int OfferSize = 2;

    /// <summary>
    /// Up to <see cref="OfferSize"/> distinct themes not yet selected this month,
    /// seeded-deterministic. Convenience overload that reads (seed, week, selections) from the run.
    /// </summary>
    public static IReadOnlyList<Theme> OfferForWeek(RunState run)
    {
        if (run is null) throw new ArgumentNullException(nameof(run));
        return OfferForWeek(run.Seed, run.WeekOfYear, run.SelectedThemesThisMonth);
    }

    /// <summary>
    /// Explicit form for the Sunday-night cross-month case: the caller can pass
    /// <c>weekOfYear + 1</c> and an empty <paramref name="alreadySelectedThisMonth"/> so the
    /// offer for next week of next month is fresh (no current-month exclusions).
    /// </summary>
    public static IReadOnlyList<Theme> OfferForWeek(
        int seed, int weekOfYear, IReadOnlyCollection<Theme> alreadySelectedThisMonth)
    {
        var selected = alreadySelectedThisMonth ?? Array.Empty<Theme>();
        var selectedSet = new HashSet<Theme>(selected);

        List<Theme> candidates = Enum.GetValues(typeof(Theme))
            .Cast<Theme>()
            .Where(t => !selectedSet.Contains(t))
            .OrderBy(t => (int)t)
            .ToList();

        var rng = new Random(seed ^ (weekOfYear * WeekSaltPrime));
        for (int i = candidates.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
        }

        return candidates.Take(OfferSize).ToList();
    }
}
