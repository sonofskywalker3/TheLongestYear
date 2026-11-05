using System;

namespace TheLongestYear.Core.Availability;

/// <summary>Tapper goods from Data/WildTrees TapItems. The Tapper is Foraging 4 in 1.6
/// (Data/CraftingRecipes "s Foraging 4"); the good is ready Days nights later. The artisan rule
/// used to reach these through the Wood Chipper at week 9 (review 2026-08-28).
///
/// The tree has to exist too. The week is no earlier than the first map where a full-grown tree of
/// that type stands. A type standing nowhere when the world loads gets the last week of the year:
/// the green rain fern tree (id "12", Summer only, gone again the next day), and the Mystic Tree
/// (id "13", planted from the Foraging Mastery reward), and the Mushroom Tree (id "7", a farm tree
/// turning at 5% a Fall night, Farm.cs, so nothing to plan on). JazzDaxx, 1.2.1: Fiddlehead Fern at
/// week 4 put a Four Seasons Sampler line on the Spring gate with nothing to tap.</summary>
public static class TapperAvailability
{
    public const int TapperSkillLevel = 4;
    private const int BaseEffort = 2;
    private const int SlowDays = 7;

    public static ItemEffort? Derive(string qualifiedId, EffortData data, LocationWeeks? weeks, WeekMode mode = WeekMode.Pacing)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));
        ItemEffort? best = null;
        foreach (RawTapItem tap in data.TapItems)
        {
            if (tap.ItemId != qualifiedId) continue;
            int tapperWeek = Math.Min(Calendar.WeeksPerYear,
                AvailabilityWeeks.MachineLevelWeek(TapperSkillLevel) + tap.Days / Calendar.DaysPerWeek);
            PlaceWeek? place = WildTreeAvailability.EarliestPlace(tap.TreeId, data.WildTreeSpots, weeks, out string location, out _);
            int week = place == null ? Calendar.WeeksPerYear : Math.Max(tapperWeek, place.Week);
            int hard = place == null ? Calendar.WeeksPerYear : Math.Max(tapperWeek, place.HardFor(mode));
            int effort = BaseEffort + (tap.Days >= SlowDays ? 1 : 0);
            string where = place == null ? "no such tree standing at the start of the year" : location;
            if (best == null || week < best.EarliestWeek || (week == best.EarliestWeek && effort < best.Effort))
                best = new ItemEffort(effort,
                    $"tapper, tree {tap.TreeId}, {where}, {tap.Days} nights, Foraging {TapperSkillLevel}, week {week}, effort {effort}",
                    week, AvailabilityWeeks.SeasonOf(week), HardWeek: hard);
        }
        return best;
    }
}
