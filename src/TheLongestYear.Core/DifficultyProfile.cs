namespace TheLongestYear.Core;

/// <summary>The resolved season-pity dials for one loop. Derived from the config baselines by
/// <see cref="DifficultyResolver"/> so config.json stays the definition of Normal.</summary>
public sealed class PityProfile
{
    /// <summary>False turns easing off entirely. Counting still runs (that is
    /// <see cref="MetaState.SeasonFailCounts"/>), so re-enabling resumes where it left off.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Fails at one season played at standard difficulty before easing starts.</summary>
    public int Threshold { get; set; } = 5;

    /// <summary>Quota reduction per ease step on the KEEP path (0.10 = -10%).</summary>
    public double QuotaStep { get; set; } = 0.10;

    /// <summary>Lowest quota factor the keep-path easing can reach.</summary>
    public double QuotaFloor { get; set; } = 0.50;

    /// <summary>Hardest items removed per ease step on the RESHUFFLE path.</summary>
    public int TrimPerStep { get; set; } = 2;
}

/// <summary>The resolved effective values for one loop: what the ten
/// <see cref="DifficultySettings"/> steps actually mean in numbers.
///
/// This is what gets stamped into <see cref="MetaState.Difficulty"/> at reset, and RESOLVED
/// VALUES are stamped rather than the steps themselves. That matches the existing pity-stamp
/// idiom (BoardEaseSeason / BoardEaseSteps), which exists so a reload reproduces the reset
/// exactly. If the steps were stamped instead, a later release that retuned what "Hard" means
/// would silently change an in-flight run's economy the next time it loaded.
///
/// <see cref="Steps"/> travels alongside for diagnostics and display only; nothing reads a
/// gameplay decision off it except the two "all normal" fast paths.
/// Spec 2026-08-26 difficulty-modifiers.</summary>
public sealed class DifficultyProfile
{
    // ---- Ask-side (consumed once, at board generation) ----

    /// <summary>Multiplier on every stack ask. Money slots are never scaled.</summary>
    public double StackFactor { get; set; } = 1.0;

    /// <summary>Multiplier on the silver/gold roll chances.</summary>
    public double QualityFactor { get; set; } = 1.0;

    /// <summary>Change to a bundle's pick-X count. Ignored when
    /// <see cref="RequireAllSlots"/> is set.</summary>
    public int RequiredSlotsDelta { get; set; }

    /// <summary>Extreme: every shown slot must be donated.</summary>
    public bool RequireAllSlots { get; set; }

    /// <summary>Pool weight bias toward harder items. Engine board only.</summary>
    public double RarityBias { get; set; } = 1.0;

    // ---- Economy (read live from the stamp) ----

    /// <summary>Multiplier on every Junimo Point award.</summary>
    public double JpEarnedFactor { get; set; } = 1.0;

    /// <summary>Multiplier on shrine upgrade costs.</summary>
    public double ShrinePriceFactor { get; set; } = 1.0;

    /// <summary>Gold the farmer starts each loop with, already resolved from
    /// <see cref="GameplayConfig.StartingMoney"/>.</summary>
    public int StartingGold { get; set; } = 500;

    /// <summary>Traveling Cart items shown before any Cart Stall upgrade is owned.</summary>
    public int StartingCartSlots { get; set; } = CartSlotRules.MinSlots;

    /// <summary>Multiplier on the hold and pity price curves.</summary>
    public double HoldPriceFactor { get; set; } = 1.0;

    /// <summary>Resolved season-pity dials.</summary>
    public PityProfile Pity { get; set; } = new();

    /// <summary>The steps this profile was resolved from. Diagnostics and the two "all normal"
    /// fast paths only; never the source of a balance number.</summary>
    public DifficultySettings Steps { get; set; } = new();

    /// <summary>The all-Normal profile for a given config, i.e. today's shipping balance.</summary>
    public static DifficultyProfile Normal(GameplayConfig config)
        => DifficultyResolver.Resolve(new DifficultySettings(), config);
}
