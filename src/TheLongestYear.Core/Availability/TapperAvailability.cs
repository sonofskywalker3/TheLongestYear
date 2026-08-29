using System;

namespace TheLongestYear.Core.Availability;

/// <summary>Tapper goods from Data/WildTrees TapItems. The Tapper is Foraging 4 in 1.6
/// (Data/CraftingRecipes "s Foraging 4"); the good is ready Days nights later. The artisan rule
/// used to reach these through the Wood Chipper at week 9 (review 2026-08-28).</summary>
public static class TapperAvailability
{
    public const int TapperSkillLevel = 4;
    private const int BaseEffort = 2;
    private const int SlowDays = 7;

    public static ItemEffort? Derive(string qualifiedId, EffortData data)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));
        ItemEffort? best = null;
        foreach (RawTapItem tap in data.TapItems)
        {
            if (tap.ItemId != qualifiedId) continue;
            int week = Math.Min(Calendar.WeeksPerYear,
                AvailabilityWeeks.MachineLevelWeek(TapperSkillLevel) + tap.Days / Calendar.DaysPerWeek);
            int effort = BaseEffort + (tap.Days >= SlowDays ? 1 : 0);
            if (best == null || week < best.EarliestWeek || (week == best.EarliestWeek && effort < best.Effort))
                best = new ItemEffort(effort, $"tapper, tree {tap.TreeId}, {tap.Days} nights, Foraging {TapperSkillLevel}, week {week}, effort {effort}",
                    week, AvailabilityWeeks.SeasonOf(week), week);
        }
        return best;
    }
}
