using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core.Availability;

/// <summary>Tapper goods from Data/WildTrees TapItems. The Tapper is Foraging 4 in 1.6
/// (Data/CraftingRecipes "s Foraging 4"); the good is ready Days nights later. The artisan rule
/// used to reach these through the Wood Chipper at week 9 (review 2026-08-28).
///
/// The tree has to exist too, and the tap only starts once the player has both the Tapper and the
/// tree, so the wait counts from the later of the two. The tree's week is the first map where a
/// full-grown one stands. A tree that can never be planned on gets the last week of the year: the
/// green rain trees (ids "10" to "12", Summer only, gone the next day), the
/// Mystic Tree (id "13", planted from the Foraging Mastery reward) and
/// the Mushroom Tree (id "7", a farm tree turning at 5% a Fall night, Farm.cs). A tree that drops
/// its own seed needs no spot: spots are read from the live world, and chopping every oak must not
/// move Oak Resin. JazzDaxx, 1.2.1: Fiddlehead Fern at week 4 put a Four Seasons Sampler line on the
/// Spring gate with nothing to tap.</summary>
public static class TapperAvailability
{
    public const int TapperSkillLevel = 4;
    private const int BaseEffort = 2;
    private const int SlowDays = 7;

    /// <summary>Tree.cs greenRainTreeBushy, greenRainTreeLeafy, greenRainTreeFern.</summary>
    private static readonly IReadOnlySet<string> GreenRainTreeIds = new HashSet<string>(StringComparer.Ordinal) { "10", "11", "12" };

    public static ItemEffort? Derive(string qualifiedId, EffortData data, LocationWeeks? weeks, WeekMode mode = WeekMode.Pacing)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));
        ItemEffort? best = null;
        foreach (RawTapItem tap in data.TapItems)
        {
            if (tap.ItemId != qualifiedId) continue;
            int skillWeek = AvailabilityWeeks.MachineLevelWeek(TapperSkillLevel);
            int wait = tap.Days / Calendar.DaysPerWeek;
            PlaceWeek? place = WildTreeAvailability.EarliestPlace(tap.TreeId, data.WildTreeSpots, weeks, out string location, out _);
            bool neverPlanned = place == null && !GrowsFromItsOwnSeed(tap.TreeId, data);
            int week = neverPlanned ? Calendar.WeeksPerYear : TapWeek(skillWeek, place?.Week, wait);
            int hard = neverPlanned ? Calendar.WeeksPerYear : TapWeek(skillWeek, place?.HardFor(mode), wait);
            int effort = BaseEffort + (tap.Days >= SlowDays ? 1 : 0);
            string where = place != null ? location : neverPlanned ? "no such tree to plan on" : "grown from its seed";
            if (best == null || week < best.EarliestWeek || (week == best.EarliestWeek && effort < best.Effort))
                best = new ItemEffort(effort,
                    $"tapper, tree {tap.TreeId}, {where}, {tap.Days} nights, Foraging {TapperSkillLevel}, week {week}, effort {effort}",
                    week, AvailabilityWeeks.SeasonOf(week), HardWeek: hard);
        }
        return best;
    }

    private static int TapWeek(int skillWeek, int? treeWeek, int wait)
        => Math.Min(Calendar.WeeksPerYear, Math.Max(skillWeek, treeWeek ?? skillWeek) + wait);

    private static bool GrowsFromItsOwnSeed(string treeId, EffortData data)
        => !GreenRainTreeIds.Contains(treeId)
           && data.WildTrees.Any(t => t != null && t.TreeId == treeId && (t.SeedOnShakeChance > 0 || t.SeedOnChopChance > 0));
}
