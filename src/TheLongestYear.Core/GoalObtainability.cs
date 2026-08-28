using System.Collections.Generic;

namespace TheLongestYear.Core;

/// <summary>Whether an item may be a weekly goal in a given week of the year. Two signals, both
/// must pass: the catalog's season set (crop, forage and fish spawn seasons; null = year-round)
/// for the week's season, and, for an item the availability model has PLACED (a Phase 1 fish,
/// crab-pot or metal floor, a Phase 2 rule with a week, or an accepted override), the item's
/// first week (spec 2026-08-28-even-year). An id nothing placed is NOT floored at Winter the way
/// a gate would be: that default is the safe direction for a deadline, and the wrong one for a
/// goal, which would otherwise never name an unknown item before Winter; the unknown list is
/// how those get placed.
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
        if (availability != null && availability.IsPlaced(itemId)
            && availability.For(itemId).Week > weekOfYear)
            return false;
        return true;
    }

    /// <summary>Season form: obtainable by the season's last week (the day-28 hub's preview of
    /// next season's pool, and the gate audit).</summary>
    public static bool IsObtainable(
        IReadOnlySet<Season>? catalogSeasons, ItemAvailabilityModel? availability,
        string itemId, Season season)
        => IsObtainable(catalogSeasons, availability, itemId, AvailabilityWeeks.LastWeekOf(season));
}
