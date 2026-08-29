using System;
using System.Collections.Generic;

namespace TheLongestYear.Core.Availability;

/// <summary>Effort for gems and minerals mined straight from a node: the shallowest mine area the
/// node spawns in. Node floors are CODE facts, not data, verified against the decompiled Android
/// source (decompiled/StardewValley/StardewValley.Locations/MineShaft.cs):
/// getRandomGemRichStoneForThisLevel picks amethyst (66) and topaz (68) nodes before floor 40,
/// adds jade (70) and aquamarine (62) from area 40 and ruby (64) and emerald (60) from area 80;
/// getRandomItemForThisLevel lays Quartz (80) on any floor, Earth Crystal (86) in area 0, Frozen
/// Tear (84) in area 40 and Fire Quartz (82) in area 80; Diamond nodes are an area-80 find and
/// Prismatic Shards come from iridium nodes and mystic stones in the Skull Cavern. A gem that only
/// comes from geodes is not here; GeodeAvailability handles it.</summary>
public static class MineralNodeAvailability
{
    private sealed record NodeRule(int Area, string Note);

    private static readonly IReadOnlyDictionary<string, NodeRule> Rules =
        new Dictionary<string, NodeRule>(StringComparer.Ordinal)
        {
            ["(O)80"] = new(MineAreas.Area0, "Quartz, any floor"),
            ["(O)86"] = new(MineAreas.Area0, "Earth Crystal"),
            ["(O)66"] = new(MineAreas.Area0, "Amethyst node"),
            ["(O)68"] = new(MineAreas.Area0, "Topaz node"),
            ["(O)84"] = new(MineAreas.Area40, "Frozen Tear"),
            ["(O)70"] = new(MineAreas.Area40, "Jade node"),
            ["(O)62"] = new(MineAreas.Area40, "Aquamarine node"),
            ["(O)82"] = new(MineAreas.Area80, "Fire Quartz"),
            ["(O)64"] = new(MineAreas.Area80, "Ruby node"),
            ["(O)60"] = new(MineAreas.Area80, "Emerald node"),
            ["(O)72"] = new(MineAreas.Area80, "Diamond node"),
            ["(O)74"] = new(MineAreas.SkullCavern, "Prismatic Shard, iridium nodes and mystic stones"),
        };

    /// <summary>Null means "not a node item this rule set knows".</summary>
    public static ItemEffort? Derive(string qualifiedId)
    {
        if (qualifiedId == null || !Rules.TryGetValue(qualifiedId, out NodeRule? rule))
            return null;
        int effort = MineAreas.Effort(rule.Area);
        int week = MineAreas.Week(rule.Area);
        return new ItemEffort(effort,
            $"node, {rule.Note}, {MineAreas.Label(rule.Area)}, week {week}, effort {effort}",
            week, MineAreas.GateSeason(rule.Area), HardWeek: MineAreas.HardWeek(rule.Area));
    }
}
