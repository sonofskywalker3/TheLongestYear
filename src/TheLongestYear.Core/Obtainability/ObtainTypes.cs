using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core.Obtainability;

/// <summary>How an item is obtained. <see cref="Other"/> is a source the model knows exists but cannot
/// read (an unsupported item query, a machine output method): recorded so nothing silently vanishes.</summary>
public enum SourceKind
{
    Forage, ArtifactSpot, Fish, CrabPot, Crop, GreenhouseCrop, FruitTree, Shop, Cart, Machine,
    Cooking, Crafting, Animal, FishPond, MineNode, MonsterDrop, Geode, Tapper, Festival,
    NightMarket, Trash, GarbageCan, FishingTreasure, Other,
}

/// <summary>Dependable sources yield on purpose (a spawn, a shop row, a machine); chance sources
/// depend on luck (the cart, drops, geodes, treasure). The model tags, the consuming rule decides
/// (Jeff, 2026-09-14, option A).</summary>
public enum Reliability { Dependable, Chance }

/// <summary>What a source needs. <see cref="Requires"/> is informational ("location:Desert",
/// "machine:(BC)12", "mail:ccPantry"); <see cref="YearTwo"/>, <see cref="GingerIsland"/> and
/// <see cref="Unresolved"/> change what filters count. Equality compares <see cref="Requires"/> by
/// content, so the same source built twice is one source.</summary>
public sealed record ObtainConditions
{
    public static readonly ObtainConditions None = new();

    public string? Skill { get; init; }
    public int SkillLevel { get; init; }
    public IReadOnlyList<string> Requires { get; init; } = Array.Empty<string>();
    public bool RainOnly { get; init; }
    public bool FewDays { get; init; }
    public bool YearTwo { get; init; }
    public bool GingerIsland { get; init; }
    public int CatchLimit { get; init; }
    /// <summary>A condition or query the model could not read; the weeks are a guess, not a fact.</summary>
    public bool Unresolved { get; init; }

    public bool Equals(ObtainConditions? other)
        => other is not null
           && Skill == other.Skill && SkillLevel == other.SkillLevel
           && RainOnly == other.RainOnly && FewDays == other.FewDays && YearTwo == other.YearTwo
           && GingerIsland == other.GingerIsland && CatchLimit == other.CatchLimit && Unresolved == other.Unresolved
           && Requires.SequenceEqual(other.Requires);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Skill);
        hash.Add(SkillLevel);
        hash.Add(RainOnly);
        hash.Add(FewDays);
        hash.Add(YearTwo);
        hash.Add(GingerIsland);
        hash.Add(CatchLimit);
        hash.Add(Unresolved);
        foreach (string r in Requires) hash.Add(r);
        return hash.ToHashCode();
    }
}

/// <summary>One way to obtain an item: its kind, the weeks it works, how dependable it is, what it
/// needs, and a short origin note for the debug output.</summary>
public sealed record ObtainSource(
    SourceKind Kind, WeekMask Weeks, Reliability Reliability, ObtainConditions Conditions, string Detail);

/// <summary>Emits one dependable source for the dependable weeks and one chance source for weeks
/// reachable only by luck, so reliability survives derivation.</summary>
public static class SourcePair
{
    public static IEnumerable<ObtainSource> Of(
        SourceKind kind, WeekMask dependable, WeekMask any, ObtainConditions conditions, string detail)
    {
        if (!dependable.IsEmpty)
            yield return new ObtainSource(kind, dependable, Reliability.Dependable, conditions, detail);
        WeekMask luckOnly = any.Except(dependable);
        if (!luckOnly.IsEmpty)
            yield return new ObtainSource(kind, luckOnly, Reliability.Chance, conditions, detail);
    }
}
