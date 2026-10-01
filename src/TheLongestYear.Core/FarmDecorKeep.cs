using System;
using System.Collections.Generic;

namespace TheLongestYear.Core;

/// <summary>
/// Keep Farm Decor (spec 2026-10-01): paths and flooring, fences and gates, lamp-posts, torches,
/// braziers, signs, outdoor furniture and placed decorations come back on the same farm tiles.
/// Never kept: machines, crops, sprinklers, scarecrows, chests. The glue is
/// FarmDecorCarryoverService; the conflict rules are FarmDecorPlanner.
/// </summary>
public static class FarmDecorKeep
{
    public const string UpgradeId = "keep_farm_decor";
    public const long Cost = 500;
    public const string HardwoodId = "(O)709";

    // Vanilla ResourceClump indices (ResourceClump.cs constants).
    public const int StumpIndex = 600;
    public const int HollowLogIndex = 602;
    public const int MeteoriteIndex = 622;
    public const int BoulderIndex = 672;
    public const int QuarryBoulderIndex = 148;

    // Tool tiers as Tool.UpgradeLevel (RunBaseline.ToolTiers): 1 copper, 2 steel, 3 gold.
    private const int Copper = 1, Steel = 2, Gold = 3, Basic = 0;

    // Hardwood from ResourceClump.destroy without the Lumberjack bonus; boulders give no stone (Jeff).
    private const int StumpHardwood = 2, HollowLogHardwood = 8, NoDrop = 0;

    private static readonly IReadOnlyDictionary<int, ClumpRule> ClumpRules = new Dictionary<int, ClumpRule>
    {
        [StumpIndex] = new(DecorTool.Axe, Copper, StumpHardwood),
        [HollowLogIndex] = new(DecorTool.Axe, Steel, HollowLogHardwood),
        [BoulderIndex] = new(DecorTool.Pickaxe, Steel, NoDrop),
        [MeteoriteIndex] = new(DecorTool.Pickaxe, Gold, NoDrop),
        [QuarryBoulderIndex] = new(DecorTool.Pickaxe, Gold, NoDrop),
        [752] = new(DecorTool.Pickaxe, Basic, NoDrop),
        [754] = new(DecorTool.Pickaxe, Basic, NoDrop),
        [756] = new(DecorTool.Pickaxe, Basic, NoDrop),
        [758] = new(DecorTool.Pickaxe, Basic, NoDrop),
    };

    /// <summary>Decorative big craftables (Data/BigCraftables, 1.6, checked against the game's
    /// patch export): no machine data, no crop, no scarecrow, no storage, no warp or shop.</summary>
    private static readonly HashSet<string> DecorBigCraftableIds = new(StringComparer.Ordinal)
    {
        "(BC)0", "(BC)1", "(BC)2", "(BC)3", "(BC)4", "(BC)5", "(BC)6", "(BC)7",   // house plants
        "(BC)22", "(BC)23", "(BC)26", "(BC)27", "(BC)28", "(BC)29", "(BC)31", "(BC)32", "(BC)33",
        "(BC)34", "(BC)35", "(BC)36", "(BC)40", "(BC)41", "(BC)42", "(BC)43", "(BC)44", "(BC)45",
        "(BC)46", "(BC)47", "(BC)48", "(BC)52", "(BC)53", "(BC)54", "(BC)55", "(BC)56",
        "(BC)64", "(BC)65", "(BC)66", "(BC)67", "(BC)68", "(BC)69", "(BC)70", "(BC)72", "(BC)73",
        "(BC)74", "(BC)75", "(BC)76", "(BC)78", "(BC)79", "(BC)80", "(BC)83", "(BC)84", "(BC)85",
        "(BC)86", "(BC)87", "(BC)88", "(BC)89", "(BC)94", "(BC)95", "(BC)98", "(BC)107", "(BC)108",
        "(BC)111", "(BC)112", "(BC)116", "(BC)117", "(BC)118", "(BC)119", "(BC)120", "(BC)121",
        "(BC)122", "(BC)123", "(BC)124", "(BC)125", "(BC)141", "(BC)152", "(BC)153", "(BC)155",
        "(BC)159", "(BC)161", "(BC)162", "(BC)164", "(BC)174", "(BC)175", "(BC)184", "(BC)188",
        "(BC)192", "(BC)196", "(BC)200", "(BC)204", "(BC)219", "(BC)262", "(BC)263", "(BC)TextSign",
    };

    public static ClumpRule? RuleFor(int clumpIndex)
        => ClumpRules.TryGetValue(clumpIndex, out ClumpRule? rule) ? rule : null;

    public static bool CanBreak(int clumpIndex, int axeTier, int pickaxeTier)
    {
        ClumpRule? rule = RuleFor(clumpIndex);
        if (rule == null)
            return false;
        int tier = rule.Tool == DecorTool.Axe ? axeTier : pickaxeTier;
        return tier >= rule.MinTier;
    }

    public static bool IsKeptDecor(FarmThingKind kind, string qualifiedItemId) => kind switch
    {
        FarmThingKind.Flooring or FarmThingKind.Fence or FarmThingKind.Torch or FarmThingKind.Sign => true,
        FarmThingKind.Furniture => !IsOwnItem(qualifiedItemId),
        FarmThingKind.BigCraftable => DecorBigCraftableIds.Contains(qualifiedItemId ?? ""),
        _ => false,
    };

    private static bool IsOwnItem(string? qualifiedItemId)
        => qualifiedItemId != null && qualifiedItemId.Contains(StashNesting.ModItemPrefix, StringComparison.Ordinal);
}
