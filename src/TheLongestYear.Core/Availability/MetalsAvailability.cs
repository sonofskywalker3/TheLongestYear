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
/// Season floors are judgements about a first-year run on a 500g start, leaning late on purpose:
/// a floor set too early permits an impossible deadline, a floor set too late is merely lenient.
/// Floor 41 is reachable in Spring by a player who commits to the mine. Floor 81 is not, so gold
/// floors at Summer. Skull Cavern needs the Vault bundle funded first, so iridium floors at Fall.</summary>
public static class MetalsAvailability
{
    private const int SmeltingCost = 2;

    private sealed record MetalRule(Season Floor, int Effort, string Basis);

    private static readonly IReadOnlyDictionary<string, MetalRule> Rules =
        new Dictionary<string, MetalRule>(StringComparer.Ordinal)
        {
            // Ore, straight off the mine floor.
            ["(O)378"] = new(Season.Spring, 1, "copper ore, mine area 0, floors 1 to 39"),
            ["(O)380"] = new(Season.Spring, 3, "iron ore, mine area 40, floors 41 to 79"),
            ["(O)384"] = new(Season.Summer, 5, "gold ore, mine area 80, floors 81 to 119"),
            ["(O)386"] = new(Season.Fall,   8, "iridium ore, mine area 121, Skull Cavern behind the bus repair"),

            // Bars: the ore, plus a furnace, plus the smelt.
            ["(O)334"] = new(Season.Spring, 1 + SmeltingCost, "copper bar, mine area 0 plus a furnace smelt"),
            ["(O)335"] = new(Season.Spring, 3 + SmeltingCost, "iron bar, mine area 40 plus a furnace smelt"),
            ["(O)336"] = new(Season.Summer, 5 + SmeltingCost, "gold bar, mine area 80 plus a furnace smelt"),
            ["(O)337"] = new(Season.Fall,   8 + SmeltingCost, "iridium bar, mine area 121 plus a furnace smelt"),

            // The rest of the Metals pool.
            ["(O)382"] = new(Season.Spring, 2, "coal, mine rocks and the occasional node, floors 1 to 39"),
            ["(O)338"] = new(Season.Spring, 3, "refined quartz, quartz from floor 1 plus a furnace smelt"),
            ["(O)881"] = new(Season.Summer, 4, "bone fragment, skeletons from mine area 80 and dig spots"),
        };

    /// <summary>Null means "not a metal this rule set knows", so the composer can try another
    /// domain or fall through to the unrecognised default.</summary>
    public static ItemAvailability? Derive(PoolItem item)
    {
        if (item == null) throw new ArgumentNullException(nameof(item));
        if (!Rules.TryGetValue(item.ItemId, out MetalRule? rule))
            return null;
        return new ItemAvailability(rule.Floor, rule.Effort,
            $"{rule.Basis}, earliest {rule.Floor}, effort {rule.Effort}");
    }
}
