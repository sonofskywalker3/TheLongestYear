using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core;

/// <summary>
/// Produces the weekly 1-of-2 selection offer (spec §4). The offer is a pure, deterministic
/// function of the run's seed, the current week-of-year, and which themes are already selected
/// this month, so it is stable across re-queries within a week and across reloads.
///
/// Rule C (activity-themes spec 2026-08-28): when the caller can say how many goals each theme
/// could ask for this week, only themes with <see cref="MinAskableToOffer"/> or more qualify, the
/// two cards are drawn weighted by that count, and a short offer is padded from the not-picked
/// room themes in seed order (the legacy shuffle) that can still ask for at least one goal.
/// A theme that can ask nothing never pads a card (Jeff, 2026-08-28) -- the offer can come back
/// short, even a single card, rather than hand out a free drawback lift for nothing.
/// </summary>
public static class SelectionService
{
    private const int WeekSaltPrime = 7919;

    /// <summary>The number of themes offered each week.</summary>
    public const int OfferSize = 2;

    /// <summary>Rule C: a theme needs at least this many askable goals to be on a card.</summary>
    public const int MinAskableToOffer = 2;

    /// <summary>Rule C padding: a theme needs at least this many askable goals to pad a short
    /// offer. Lower than <see cref="MinAskableToOffer"/> on purpose (Jeff, 2026-08-28): a padded
    /// card may be thin, but a theme that can ask for nothing at all never pads.</summary>
    private const int MinAskableToPad = 1;

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
    /// <paramref name="askableFor"/> null = the legacy shuffle over every theme.
    /// </summary>
    public static IReadOnlyList<Theme> OfferForWeek(
        int seed, int weekOfYear, IReadOnlyCollection<Theme> alreadySelectedThisMonth,
        Func<Theme, int>? askableFor = null)
    {
        var selected = alreadySelectedThisMonth ?? Array.Empty<Theme>();
        var selectedSet = new HashSet<Theme>(selected);
        var rng = new Random(seed ^ (weekOfYear * WeekSaltPrime));

        if (askableFor == null)
        {
            List<Theme> all = Enum.GetValues(typeof(Theme)).Cast<Theme>()
                .Where(t => !selectedSet.Contains(t))
                .OrderBy(t => (int)t)
                .ToList();
            Shuffle(all, rng);
            return all.Take(OfferSize).ToList();
        }

        List<(Theme Theme, int Weight)> qualified = Enum.GetValues(typeof(Theme)).Cast<Theme>()
            .Where(t => !selectedSet.Contains(t))
            .OrderBy(t => (int)t)
            .Select(t => (t, askableFor(t)))
            .Where(pair => pair.Item2 >= MinAskableToOffer)
            .ToList();

        var offer = new List<Theme>(OfferSize);
        while (offer.Count < OfferSize && qualified.Count > 0)
        {
            int total = qualified.Sum(q => q.Weight);
            int roll = rng.Next(total);
            int cum = 0;
            for (int i = 0; i < qualified.Count; i++)
            {
                cum += qualified[i].Weight;
                if (roll >= cum) continue;
                offer.Add(qualified[i].Theme);
                qualified.RemoveAt(i);
                break;
            }
        }

        if (offer.Count < OfferSize)
        {
            List<Theme> fallback = ThemeDomains.RoomThemes
                .Where(t => !selectedSet.Contains(t) && !offer.Contains(t) && askableFor(t) >= MinAskableToPad)
                .OrderBy(t => (int)t)
                .ToList();
            Shuffle(fallback, rng);
            offer.AddRange(fallback.Take(OfferSize - offer.Count));
        }
        return offer;
    }

    /// <summary>The themes a re-roll may shuffle among: the qualified ones, or the not-picked room
    /// themes when fewer than <see cref="OfferSize"/> qualify.</summary>
    public static IReadOnlyList<Theme> Candidates(
        IReadOnlyCollection<Theme> alreadySelectedThisMonth, Func<Theme, int> askableFor)
    {
        if (askableFor == null) throw new ArgumentNullException(nameof(askableFor));
        var selectedSet = new HashSet<Theme>(alreadySelectedThisMonth ?? Array.Empty<Theme>());
        List<Theme> qualified = Enum.GetValues(typeof(Theme)).Cast<Theme>()
            .Where(t => !selectedSet.Contains(t) && askableFor(t) >= MinAskableToOffer)
            .OrderBy(t => (int)t)
            .ToList();
        if (qualified.Count >= OfferSize) return qualified;
        return qualified
            .Concat(ThemeDomains.RoomThemes.Where(t => !selectedSet.Contains(t) && !qualified.Contains(t) && askableFor(t) >= MinAskableToPad))
            .ToList();
    }

    private static void Shuffle(List<Theme> list, Random rng)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
