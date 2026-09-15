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
    NightMarket, Trash, GarbageCan, FishingTreasure, Guild, Other,
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

/// <summary>A step the player must do first, with the game's own days figure ("building:Coop" 3,
/// "animal:Chicken" 1, "sapling" 28). Informational in the blind model (spec decision 3): the
/// comparison assumes it is done; a consumer adds the days for what the real farm lacks.</summary>
public sealed record SetupStep(string Name, int Days);

/// <summary>One way to obtain an item: its kind, the start-to-landing table, how dependable it is,
/// what it needs, its setup steps, and a short origin note for the debug output.</summary>
public sealed record ObtainSource(
    SourceKind Kind, DayTable Lands, Reliability Reliability, ObtainConditions Conditions, string Detail)
{
    public IReadOnlyList<SetupStep> Setup { get; init; } = Array.Empty<SetupStep>();

    /// <summary>The items this route is made from, as groups where any member of a group serves (a
    /// machine taking several inputs) and every group is needed (a recipe's ingredients); empty for
    /// a route that needs no item. A derived table already holds WHEN an input lands, but not WHAT
    /// it was, so a consumer that checks a route against a real save needs this to ask the same
    /// question of the input (a Cheese route needs a cow, not just a Cheese Press).
    /// <para>Deliberately outside <see cref="Equals(ObtainSource?)"/> and
    /// <see cref="GetHashCode"/>: two routes that differ only in which input fed them are still one
    /// route to every existing consumer, and Distinct() keeps merging them as it always has.</para></summary>
    public IReadOnlyList<IReadOnlyList<string>> Inputs { get; init; } = Array.Empty<IReadOnlyList<string>>();

    public bool Equals(ObtainSource? other)
        => other is not null && Kind == other.Kind && Lands.Equals(other.Lands) && Reliability == other.Reliability
           && Conditions.Equals(other.Conditions) && Detail == other.Detail && Setup.SequenceEqual(other.Setup);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Kind); hash.Add(Lands); hash.Add(Reliability); hash.Add(Conditions); hash.Add(Detail);
        foreach (SetupStep s in Setup) hash.Add(s);
        return hash.ToHashCode();
    }
}

/// <summary>Emits one dependable source and one chance source for the starts luck alone serves,
/// so reliability survives derivation.</summary>
public static class SourcePair
{
    public static IEnumerable<ObtainSource> Of(
        SourceKind kind, DayTable dependable, DayTable any, ObtainConditions conditions, string detail,
        IReadOnlyList<SetupStep>? setup = null)
    {
        IReadOnlyList<SetupStep> steps = setup ?? Array.Empty<SetupStep>();
        if (!dependable.IsEmpty)
            yield return new ObtainSource(kind, dependable, Reliability.Dependable, conditions, detail) { Setup = steps };
        DayTable luckOnly = any.Except(dependable);
        if (!luckOnly.IsEmpty)
            yield return new ObtainSource(kind, luckOnly, Reliability.Chance, conditions, detail) { Setup = steps };
    }
}
