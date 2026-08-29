using System;
using System.Collections.Generic;

namespace TheLongestYear.Core.Availability;

/// <summary>Weeks for unlocks that are not a skill level (spec 2026-08-28-obtainable-board,
/// section 6, tables adopted by Jeff): a friendship recipe by its hearts, a bought recipe by its
/// price. Hearts run about one a week with two loved gifts and daily talk; the cost bands assume
/// the 500g start.</summary>
public static class UnlockWeeks
{
    private static readonly (int Hearts, int Week)[] HeartWeeks =
        { (2, 2), (3, 3), (4, 4), (5, 5), (6, 6), (7, 8), (8, 9), (9, 10), (10, 12) };

    private static readonly (int MaxGold, int Week)[] CostWeeks =
        { (1000, 1), (3000, 2), (5000, 3), (10000, 5), (25000, 7), (50000, 10) };

    private const int OverCostWeek = 13;

    /// <summary>Villagers the player cannot befriend from week 1; null means not in year 1
    /// (Kent returns in Spring of year 2, Leo lives on Ginger Island).</summary>
    private static readonly IReadOnlyDictionary<string, int?> FirstWeeks =
        new Dictionary<string, int?>(StringComparer.OrdinalIgnoreCase)
        {
            ["Sandy"] = AvailabilityWeeks.SkullCavernWeek,
            ["Krobus"] = AvailabilityWeeks.SewerWeek,
            ["Kent"] = null,
            ["Leo"] = null,
        };

    public static int ForHearts(int hearts)
    {
        int week = 1;
        foreach ((int h, int w) in HeartWeeks)
            if (hearts >= h) week = w;
        return week;
    }

    public static int ForCost(int gold)
    {
        foreach ((int max, int week) in CostWeeks)
            if (gold <= max) return week;
        return OverCostWeek;
    }

    public static int? VillagerFirstWeek(string villager)
        => villager != null && FirstWeeks.TryGetValue(villager, out int? week) ? week : 1;

    public static int? ForFriendship(string villager, int hearts)
    {
        int? first = VillagerFirstWeek(villager);
        return first == null ? null : Math.Max(first.Value, ForHearts(hearts));
    }
}
