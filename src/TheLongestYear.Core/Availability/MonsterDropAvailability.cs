using System;
using System.Collections.Generic;

namespace TheLongestYear.Core.Availability;

/// <summary>Effort for a monster drop: the shallowest mine area the dropping monster spawns in,
/// plus a rarity step from its drop chance (Data/Monsters field 6), minimum over every monster
/// that drops the item. Spawn floors are CODE facts (MineShaft.getMonsterForThisLevel, decompile
/// MineShaft.cs:4033, plus the constructors that pick a name by floor: GreenSlime, Bat, RockCrab).
/// The Slime Hutch is not counted (it needs the Wizard); a monster this table does not know
/// (modded, or hard-mode-only names) is skipped, and an item only they drop yields null.</summary>
public static class MonsterDropAvailability
{
    private const double FrequentChance = 0.5;
    private const double OccasionalChance = 0.1;
    private const double RareDropChance = 0.05;

    // Volcano Dungeon and Skull Cavern's Dangerous Mines monsters are post-Community-Center
    // content (spec 2026-08-28-obtainable-board section 6) and are not in this table: Blue Squid,
    // Haunted Skull, Tiger Slime, Lava Lurk, Hot Head, Magma Sprite, Magma Sparker, Magma Duggy,
    // False Magma Cap, Dwarvish Sentry, Putrid Ghost, Shadow Sniper, Skeleton Mage, Spider, Stick
    // Bug, Fireball, Spiker.
    private static readonly IReadOnlyDictionary<string, int> SpawnArea =
        new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["Green Slime"] = MineAreas.Area0, ["Duggy"] = MineAreas.Area0, ["Rock Crab"] = MineAreas.Area0,
            ["Bug"] = MineAreas.Area0, ["Grub"] = MineAreas.Area0, ["Fly"] = MineAreas.Area0,
            ["Stone Golem"] = MineAreas.Area0, ["Bat"] = MineAreas.Area0, ["Big Slime"] = MineAreas.Area0,

            ["Dust Spirit"] = MineAreas.Area40, ["Frost Bat"] = MineAreas.Area40, ["Frost Jelly"] = MineAreas.Area40,
            ["Ghost"] = MineAreas.Area40, ["Skeleton"] = MineAreas.Area40,

            ["Lava Bat"] = MineAreas.Area80, ["Sludge"] = MineAreas.Area80, ["Shadow Brute"] = MineAreas.Area80,
            ["Shadow Shaman"] = MineAreas.Area80, ["Metal Head"] = MineAreas.Area80, ["Lava Crab"] = MineAreas.Area80,
            ["Squid Kid"] = MineAreas.Area80,

            ["Serpent"] = MineAreas.SkullCavern, ["Royal Serpent"] = MineAreas.SkullCavern, ["Mummy"] = MineAreas.SkullCavern,
            ["Carbon Ghost"] = MineAreas.SkullCavern, ["Iridium Bat"] = MineAreas.SkullCavern,
            ["Iridium Crab"] = MineAreas.SkullCavern, ["Pepper Rex"] = MineAreas.SkullCavern, ["Armored Bug"] = MineAreas.SkullCavern,
            ["Assassin Bug"] = MineAreas.SkullCavern,
        };

    public static int ChanceStep(double chance)
        => chance >= FrequentChance ? 0 : chance >= OccasionalChance ? 1 : 2;

    public static int? SpawnAreaFor(string monsterName)
        => monsterName != null && SpawnArea.TryGetValue(monsterName, out int area) ? area : null;

    public static ItemEffort? Derive(string qualifiedId, IReadOnlyList<RawMonsterDrop> drops)
    {
        if (drops == null) throw new ArgumentNullException(nameof(drops));
        ItemEffort? best = null;
        foreach (RawMonsterDrop drop in drops)
        {
            if (drop.ItemId != qualifiedId) continue;
            int? area = SpawnAreaFor(drop.MonsterName);
            if (area == null) continue;
            int step = ChanceStep(drop.Chance);
            int effort = MineAreas.Effort(area.Value) + step;
            int monsterWeek = MineAreas.Week(area.Value);
            bool rare = drop.Chance < RareDropChance;
            int week = rare ? AvailabilityWeeks.UnknownWeek : monsterWeek;
            bool better = best == null || week < best.EarliestWeek || (week == best.EarliestWeek && effort < best.Effort);
            if (better)
                best = new ItemEffort(effort,
                    $"monster drop, {drop.MonsterName} ({MineAreas.Label(area.Value)}) at {drop.Chance:0.##} (+{step}), week {week}, effort {effort}"
                        + (rare ? $", rare drop: pacing Winter, hard week {MineAreas.HardWeek(area.Value)}" : ""),
                    week, rare ? Season.Winter : MineAreas.GateSeason(area.Value), HardWeek: MineAreas.HardWeek(area.Value));
        }
        return best;
    }
}
