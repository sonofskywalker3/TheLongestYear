using System.Collections.Generic;

namespace TheLongestYear.Core;

/// <summary>Whether an item may be a weekly goal in a given week of the year. Two signals, both
/// must pass: the catalog's season set (crop, forage and fish spawn seasons; null = year-round)
/// for the week's season, and, when an availability model is given, that the item is one the
/// model has PLACED (a Phase 1 fish, crab-pot or metal floor, a Phase 2 rule with a week, or an
/// accepted override) and has reached its mode's goal week (spec 2026-08-28-obtainable-board-1,
/// section on GoalWeek). An id nothing placed is not a goal at all, in either mode: naming an
/// item the model cannot vouch for risks a card the run cannot satisfy, and the unknown list
/// (ItemAvailabilityModel.UnknownIds) is where those surface instead, for Jeff to place by hand.
/// A null model (legacy callers with no availability data) skips this signal and reads the
/// catalog's season set alone.
///
/// Bundle-loop audit 2026-08-29: Master Fisher's offered Scorpion Carp as a Summer goal.</summary>
public static class GoalObtainability
{
    public static bool IsObtainable(
        IReadOnlySet<Season>? catalogSeasons, ItemAvailabilityModel? availability,
        string itemId, int weekOfYear)
    {
        Season season = AvailabilityWeeks.SeasonOf(weekOfYear);
        if (catalogSeasons != null && !catalogSeasons.Contains(season))
            return false;
        if (availability != null)
        {
            if (!availability.IsPlaced(itemId)) return false;
            if (availability.For(itemId).GoalWeek > weekOfYear) return false;
        }
        return true;
    }

    /// <summary>Season form: obtainable by the season's last week (the day-28 hub's preview of
    /// next season's pool, and the gate audit).</summary>
    public static bool IsObtainable(
        IReadOnlySet<Season>? catalogSeasons, ItemAvailabilityModel? availability,
        string itemId, Season season)
        => IsObtainable(catalogSeasons, availability, itemId, AvailabilityWeeks.LastWeekOf(season));
}
