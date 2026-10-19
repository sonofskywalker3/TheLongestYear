using System;
using System.Collections.Generic;

namespace TheLongestYear.Core;

/// <summary>How far up a chain the player's world already is, as a rung number (0 = none).
/// Pure maths: the glue layer reports what it sees and these map it onto rungs.</summary>
public static class FocusWorldRungs
{
    private const string RodSlug = "fishing_rod";
    private const int MaxMetalToolLevel = 4;
    private const int RodSecondRungLevel = 2;
    private const int RodFirstRungMaxLevel = 1;
    private const int HouseTopLevel = 3;
    public const int BackpackTierTwoSize = 36;
    public const int BackpackTierOneSize = 24;

    private static readonly IReadOnlyDictionary<string, int> CoopRungs = new Dictionary<string, int>(StringComparer.Ordinal)
        { ["Coop"] = 1, ["Big Coop"] = 2, ["Deluxe Coop"] = 3 };
    private static readonly IReadOnlyDictionary<string, int> BarnRungs = new Dictionary<string, int>(StringComparer.Ordinal)
        { ["Barn"] = 1, ["Big Barn"] = 2, ["Deluxe Barn"] = 3 };
    private static readonly IReadOnlyDictionary<string, int> SiloRungs = new Dictionary<string, int>(StringComparer.Ordinal)
        { ["Silo"] = 1 };
    private static readonly IReadOnlyDictionary<string, int> FishPondRungs = new Dictionary<string, int>(StringComparer.Ordinal)
        { [FishPondKeep.BuildingType] = 1 };

    public static int ToolRung(string slug, int level)
    {
        if (slug == RodSlug)
        {
            if (level <= ToolTierPeaks.NoRod) return 0;
            if (level <= RodFirstRungMaxLevel) return 1;
            return level == RodSecondRungLevel ? 2 : 3;
        }
        return Math.Clamp(level, 0, MaxMetalToolLevel);
    }

    /// <summary>Highest rung among the given building type names for a building chain. The caller
    /// passes each building's finished type (the pending upgrade name, else its current type).</summary>
    public static int BuildingRung(string chainId, IEnumerable<string> buildingTypes)
    {
        IReadOnlyDictionary<string, int>? table = chainId switch
        {
            "coop" => CoopRungs,
            "barn" => BarnRungs,
            "silo" => SiloRungs,
            "fish_pond" => FishPondRungs,
            _ => null,
        };
        if (table == null || buildingTypes == null) return 0;
        int best = 0;
        foreach (string type in buildingTypes)
            if (type != null && table.TryGetValue(type, out int rung) && rung > best) best = rung;
        return best;
    }

    public static int HouseRung(int houseLevel, bool upgradePending)
    {
        int effective = houseLevel + (upgradePending ? 1 : 0);
        if (effective <= 0) return 0;
        return effective >= HouseTopLevel ? 2 : 1;
    }

    /// <summary>The bag size a backpack rung stands for: rung 1 is 24 slots, rung 2 (or more) is 36.</summary>
    public static int BackpackSizeForRung(int rung)
        => rung >= 2 ? BackpackTierTwoSize : BackpackTierOneSize;

    public static int BackpackRung(int maxItems)
        => maxItems >= BackpackTierTwoSize ? 2 : maxItems >= BackpackTierOneSize ? 1 : 0;
}
