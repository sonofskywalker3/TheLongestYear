using System;
using System.Collections.Generic;

namespace TheLongestYear.Core.Availability;

/// <summary>Earliest season and effort for ore, bars and the other Metals pool items.
///
/// Mine depth is a CODE fact, not a data fact: MineShaft.getAppropriateOre switches on
/// getMineArea, giving copper in areas 0 and 10, iron in area 40, gold in area 80 and iridium in
/// area 121 (Skull Cavern, which is behind the 40,000g bus repair). Verified against the
/// decompiled Android source, decompiled/StardewValley/StardewValley.Locations/MineShaft.cs
/// around line 1729. There is no data table to read this from, so it lives here as a rule.
///
/// Weeks and gate seasons come from the mine area (AvailabilityWeeks: 30 floors a week for the
/// goals, floors 80 and deeper a Summer gate, Skull Cavern a Fall gate behind the bus).</summary>
public static class MetalsAvailability
{
    private const int SmeltingCost = 2;

    private sealed record MetalRule(int Area, int Effort, string Basis);

    private static readonly IReadOnlyDictionary<string, MetalRule> Rules =
        new Dictionary<string, MetalRule>(StringComparer.Ordinal)
        {
            // Ore, straight off the mine floor.
            ["(O)378"] = new(MineAreas.Area0, 1, "copper ore, mine area 0, floors 1 to 39"),
            ["(O)380"] = new(MineAreas.Area40, 3, "iron ore, mine area 40, floors 41 to 79"),
            ["(O)384"] = new(MineAreas.Area80, 5, "gold ore, mine area 80, floors 81 to 119"),
            ["(O)386"] = new(MineAreas.SkullCavern, 8, "iridium ore, mine area 121, Skull Cavern behind the bus repair"),

            // Bars: the ore, plus a furnace, plus the smelt.
            ["(O)334"] = new(MineAreas.Area0, 1 + SmeltingCost, "copper bar, mine area 0 plus a furnace smelt"),
            ["(O)335"] = new(MineAreas.Area40, 3 + SmeltingCost, "iron bar, mine area 40 plus a furnace smelt"),
            ["(O)336"] = new(MineAreas.Area80, 5 + SmeltingCost, "gold bar, mine area 80 plus a furnace smelt"),
            ["(O)337"] = new(MineAreas.SkullCavern, 8 + SmeltingCost, "iridium bar, mine area 121 plus a furnace smelt"),

            // The rest of the Metals pool.
            ["(O)382"] = new(MineAreas.Area0, 2, "coal, mine rocks and the occasional node, floors 1 to 39"),
            ["(O)338"] = new(MineAreas.Area0, 3, "refined quartz, quartz from floor 1 plus a furnace smelt"),
            ["(O)881"] = new(MineAreas.Area80, 4, "bone fragment, skeletons from mine area 80 and dig spots"),
        };

    /// <summary>Null means "not a metal this rule set knows", so the composer can try another
    /// domain or fall through to the unrecognised default.</summary>
    public static ItemAvailability? Derive(PoolItem item)
    {
        if (item == null) throw new ArgumentNullException(nameof(item));
        if (!Rules.TryGetValue(item.ItemId, out MetalRule? rule))
            return null;
        int week = MineAreas.Week(rule.Area);
        Season gate = MineAreas.GateSeason(rule.Area);
        return new ItemAvailability(AvailabilityWeeks.SeasonOf(week), rule.Effort,
            $"{rule.Basis}, week {week}, gate {gate}, effort {rule.Effort}", EffortSource.Derived, week, gate);
    }
}
