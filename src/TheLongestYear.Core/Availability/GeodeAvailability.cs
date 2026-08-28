using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core.Availability;

/// <summary>Effort for anything a geode can yield: the easiest geode that can drop it, plus a
/// rarity step from that geode's per-drop chance (Data/Objects GeodeDrops). The code-only half of
/// the table (Utility.getTreasureFromGeode, decompile Utility.cs:6368: stone, clay, coal, the ores
/// and the area crystal) is exposed by <see cref="DefaultTableDrops"/> so the glue can add it for
/// geodes flagged GeodeDropsDefaultItems. Cracking costs 25g at Clint, which is ignored.</summary>
public static class GeodeAvailability
{
    private const double CommonChance = 1.0 / 8;
    private const double UncommonChance = 1.0 / 20;
    private const double DefaultTableShare = 0.5;

    private sealed record GeodeRule(int Area, int Effort, string Label);

    private static readonly IReadOnlyDictionary<string, GeodeRule> Geodes =
        new Dictionary<string, GeodeRule>(StringComparer.Ordinal)
        {
            ["(O)535"] = new(MineAreas.Area0, 1, "Geode, floors 1 to 39"),
            ["(O)536"] = new(MineAreas.Area40, 3, "Frozen Geode, floors 41 to 79"),
            ["(O)537"] = new(MineAreas.Area80, 5, "Magma Geode, floors 81 to 119"),
            ["(O)749"] = new(MineAreas.Area40, 4, "Omni Geode, any floor at low odds, Skull Cavern reliably"),
        };

    private static readonly IReadOnlyDictionary<string, string[]> DefaultTable =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["(O)535"] = new[] { "(O)390", "(O)330", "(O)382", "(O)378", "(O)380", "(O)86" },
            ["(O)536"] = new[] { "(O)390", "(O)330", "(O)382", "(O)378", "(O)380", "(O)384", "(O)84" },
            ["(O)537"] = new[] { "(O)390", "(O)330", "(O)382", "(O)378", "(O)380", "(O)384", "(O)386", "(O)82" },
            ["(O)749"] = new[] { "(O)390", "(O)330", "(O)382", "(O)378", "(O)380", "(O)384", "(O)386", "(O)82", "(O)84", "(O)86" },
        };

    public static int ChanceStep(double chance)
        => chance >= CommonChance ? 0 : chance >= UncommonChance ? 1 : 2;

    public static IReadOnlyList<RawGeodeDrop> DefaultTableDrops(string geodeQualifiedId)
        => geodeQualifiedId != null && DefaultTable.TryGetValue(geodeQualifiedId, out string[]? ids)
            ? ids.Select(id => new RawGeodeDrop(geodeQualifiedId, id, DefaultTableShare / ids.Length)).ToList()
            : Array.Empty<RawGeodeDrop>();

    public static ItemEffort? Derive(string qualifiedId, IReadOnlyList<RawGeodeDrop> drops)
    {
        if (drops == null) throw new ArgumentNullException(nameof(drops));
        ItemEffort? best = null;
        foreach (RawGeodeDrop drop in drops)
        {
            if (drop.ItemId != qualifiedId || !Geodes.TryGetValue(drop.GeodeItemId, out GeodeRule? geode))
                continue;
            int step = ChanceStep(drop.Chance);
            int effort = geode.Effort + step;
            int week = MineAreas.Week(geode.Area);
            bool better = best == null || week < best.EarliestWeek || (week == best.EarliestWeek && effort < best.Effort);
            if (better)
                best = new ItemEffort(effort,
                    $"geode, {geode.Label}, chance {drop.Chance:0.###} (+{step}), week {week}, effort {effort}",
                    week, MineAreas.GateSeason(geode.Area));
        }
        return best;
    }
}
