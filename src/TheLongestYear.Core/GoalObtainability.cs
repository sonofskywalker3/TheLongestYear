using System.Collections.Generic;

namespace TheLongestYear.Core;

/// <summary>Whether an item may be a weekly goal in a season. Two signals, both must pass:
/// the catalog's season set (crop, forage and fish spawn seasons; null = year-round), and, for
/// an item the availability model actually derived (fish, crab-pot, metals), the earliest season
/// the model allows, which carries the location floors (Desert and Skull Cavern in Fall, the
/// mines and the Sewer in Summer). An id the model did not derive is NOT floored at Winter the
/// way a gate would be: that default is the safe direction for a deadline, and the wrong one
/// for a goal, which would otherwise never name a crop or a forage item before Winter.
///
/// Bundle-loop audit 2026-08-29: Master Fisher's offered Scorpion Carp as a Summer goal.</summary>
public static class GoalObtainability
{
    public static bool IsObtainable(
        IReadOnlySet<Season>? catalogSeasons, ItemAvailabilityModel? availability,
        string itemId, Season season)
    {
        if (catalogSeasons != null && !catalogSeasons.Contains(season))
            return false;
        if (availability != null && availability.IsDerived(itemId)
            && availability.For(itemId).EarliestSeason > season)
            return false;
        return true;
    }
}
