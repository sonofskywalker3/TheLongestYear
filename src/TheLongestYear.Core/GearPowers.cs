using System;
using System.Collections.Generic;

namespace TheLongestYear.Core;

/// <summary>What kind of tool or weapon an item is, for deciding which Gear chains apply to it.</summary>
public enum ToolKind { Hoe, Pickaxe, Axe, WateringCan, FishingRod, MeleeWeapon, Scythe, Other }

/// <summary>
/// The Gear tab (spec 2026-10-26): nine upgrade chains that give the effect of a Forge enchantment
/// (or, for Heavy and Reach, of Powerful and Reaching) to every tool or weapon of a kind, from owning
/// the upgrade. Nothing here is written onto an item. A tool that already has the real enchantment
/// gets the better of the two effects, never both. Each tier includes the one before.
/// </summary>
public static class GearPowers
{
    public const string Swift = "gear_swift";
    public const string Light = "gear_light";
    public const string Heavy = "gear_heavy";
    public const string Reach = "gear_reach";
    public const string Can = "gear_can";
    public const string Line = "gear_line";
    public const string Edge = "gear_edge";
    public const string Quick = "gear_quick";
    public const string Guard = "gear_guard";

    public const int SwiftTiers = 3;
    public const int LightTiers = 2;
    public const int HeavyTiers = 2;
    public const int ReachTiers = 2;
    public const int CanTiers = 2;
    public const int LineTiers = 2;
    public const int EdgeTiers = 3;
    public const int QuickTiers = 3;
    public const int GuardTiers = 3;

    private static readonly int[] ThreeTierCosts = { 250, 750, 1500 };
    private static readonly int[] TwoTierCosts = { 250, 1000 };

    /// <summary>Largest charge power an enchanted iridium tool reaches (iridium 4 plus Reaching 1).</summary>
    public const int MaxEnchantedChargePower = 5;

    /// <summary>Chance a cast keeps its bait and tackle once Steady Line I is owned.</summary>
    public const double LineSaveChance = 0.5;

    /// <summary>Axe power is worth twice pickaxe power per level, as vanilla Powerful has it.</summary>
    private const int AxePowerPerLevel = 2;

    /// <summary>Share of weapon damage one Ruby level adds (+10% base damage per level).</summary>
    private const float DamagePerRubyLevel = 0.10f;

    /// <summary>
    /// What one Emerald level is worth in WeaponSpeedMultiplier terms. Emerald adds 5 to the weapon's
    /// speed per level and a swing takes (400 - speed * 40) ms (MeleeWeapon.setFarmerAnimating), so one
    /// level cuts 200 ms. The multiplier cuts swipeSpeed * multiplier, so 200 ms is 0.50 for a speed-0
    /// weapon and more for faster ones. 0.50 is the floor, which keeps the Junimo bonus from ever
    /// out-stacking the gem.
    /// </summary>
    private const float EmeraldInSpeedMultiplier = 0.50f;

    /// <summary>Quickness tiers 0..3 as WeaponSpeedMultiplier. Tier 3 (0.45) sits just under one Emerald (0.50).</summary>
    private static readonly float[] QuickSteps = { 0f, 0.15f, 0.30f, 0.45f };

    private static readonly float[] Swing = { 1.0f, 0.90f, 0.80f, 0.70f };
    private static readonly float[] Refund = { 0f, 0.25f, 0.50f };

    public static IEnumerable<UpgradeDefinition> Rows()
    {
        foreach (var r in Chain(Swift, SwiftTiers, ThreeTierCosts)) yield return r;
        foreach (var r in Chain(Light, LightTiers, TwoTierCosts)) yield return r;
        foreach (var r in Chain(Heavy, HeavyTiers, TwoTierCosts)) yield return r;
        foreach (var r in Chain(Reach, ReachTiers, TwoTierCosts)) yield return r;
        foreach (var r in Chain(Can, CanTiers, TwoTierCosts)) yield return r;
        foreach (var r in Chain(Line, LineTiers, TwoTierCosts)) yield return r;
        foreach (var r in Chain(Edge, EdgeTiers, ThreeTierCosts)) yield return r;
        foreach (var r in Chain(Quick, QuickTiers, ThreeTierCosts)) yield return r;
        foreach (var r in Chain(Guard, GuardTiers, ThreeTierCosts)) yield return r;
    }

    private static IEnumerable<UpgradeDefinition> Chain(string prefix, int tiers, int[] costs)
    {
        for (int tier = 1; tier <= tiers; tier++)
            yield return new UpgradeDefinition(prefix + "_" + tier, UpgradeCategory.Gear, costs[tier - 1],
                tier > 1 ? prefix + "_" + (tier - 1) : null);
    }

    /// <summary>Animation speed factor for a Swift Hands tier (smaller is faster).</summary>
    public static float SwingFactor(int tier) => Swing[Math.Clamp(tier, 0, SwiftTiers)];

    /// <summary>The faster of the tool's own factor and the tier's, so a real Swift (0.66) is never slowed.</summary>
    public static float BetterSwing(float current, int tier) => Math.Min(current, SwingFactor(tier));

    /// <summary>Share of a swing's energy that Light Touch gives back.</summary>
    public static float EnergyRefund(int tier) => Refund[Math.Clamp(tier, 0, LightTiers)];

    public static float RefundFor(float staminaBefore, float staminaAfter, int tier)
        => Math.Max(0f, staminaBefore - staminaAfter) * EnergyRefund(tier);

    /// <summary>Power Heavy Hitter adds beyond what the tool's Powerful enchantment already gives.</summary>
    public static int ExtraPower(int tier, bool isAxe, int powerfulLevel)
    {
        int perLevel = isAxe ? AxePowerPerLevel : 1;
        return Math.Max(0, perLevel * tier - perLevel * powerfulLevel);
    }

    public static int MaxChargePower(int upgradeLevel, bool hasReaching, int tier)
        => Math.Min(MaxEnchantedChargePower, upgradeLevel + Math.Max(tier, hasReaching ? 1 : 0));

    public static int CanCapacity(int vanillaMax, int tier) => tier >= 1 ? vanillaMax * 2 : vanillaMax;
    public static bool CanBottomless(int tier) => tier >= 2;

    public static bool LineSavesBait(int tier) => tier >= 1;
    public static bool LineAutoHook(int tier) => tier >= 2;

    public static float ExtraAttack(int tier, int rubyLevel)
        => Math.Max(0f, DamagePerRubyLevel * tier - DamagePerRubyLevel * rubyLevel);

    public static float ExtraWeaponSpeed(int tier, int emeraldLevel)
        => Math.Max(0f, QuickSteps[Math.Clamp(tier, 0, QuickTiers)] - EmeraldInSpeedMultiplier * emeraldLevel);

    public static int ExtraDefense(int tier, int topazLevel) => Math.Max(0, tier - topazLevel);

    public static bool Fits(string chainPrefix, ToolKind kind)
    {
        switch (chainPrefix)
        {
            case Swift: return kind is ToolKind.Hoe or ToolKind.Pickaxe or ToolKind.Axe;
            case Light: return kind is ToolKind.Hoe or ToolKind.Pickaxe or ToolKind.Axe
                or ToolKind.WateringCan or ToolKind.FishingRod;
            case Heavy: return kind is ToolKind.Pickaxe or ToolKind.Axe;
            case Reach: return kind is ToolKind.Hoe or ToolKind.WateringCan;
            case Can: return kind == ToolKind.WateringCan;
            case Line: return kind == ToolKind.FishingRod;
            case Edge:
            case Quick:
            case Guard: return kind == ToolKind.MeleeWeapon;
            default: return false;
        }
    }
}
