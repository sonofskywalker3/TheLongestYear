using System;

namespace TheLongestYear.Core;

/// <summary>The entire difficulty balance table, as one pure function.
///
/// Everything the ten modifiers mean lives here and nowhere else, which is what makes the numbers
/// retunable in a later release without touching a single consumer, and what makes the whole ramp
/// unit-testable without the game.
///
/// The load-bearing guarantee: <c>Resolve(new DifficultySettings(), config)</c> reproduces
/// <paramref name="config"/>'s own values exactly. Every existing save depends on it, and
/// DifficultyResolverTests asserts it field by field.
///
/// Spec 2026-08-26 difficulty-modifiers, section 2.</summary>
public static class DifficultyResolver
{
    // Ask-side ramps (spec section 2.1).
    private const double StackEasy = 0.75, StackNormal = 1.0, StackHard = 1.5, StackExtreme = 2.0;
    private const double QualityEasy = 0.5, QualityNormal = 1.0, QualityHard = 2.0, QualityExtreme = 3.0;
    private const double RarityEasy = 0.5, RarityNormal = 1.0, RarityHard = 1.6, RarityExtreme = 2.4;

    // Economy ramps (spec section 2.2).
    private const double JpEasy = 1.5, JpNormal = 1.0, JpHard = 0.75, JpExtreme = 0.5;
    private const double PriceEasy = 0.75, PriceNormal = 1.0, PriceHard = 1.25, PriceExtreme = 1.5;
    private const double GoldEasy = 2.0, GoldNormal = 1.0, GoldHard = 0.5, GoldExtreme = 0.0;
    private const double HoldEasy = 0.5, HoldNormal = 1.0, HoldHard = 2.0, HoldExtreme = 4.0;

    /// <summary>Traveling Cart items shown with no Cart Stall upgrade owned. Hard and Extreme are
    /// deliberately identical: Normal is 1 and the floor is 0, so the ramp bottoms out at Hard and
    /// Extreme has nothing further to take.</summary>
    private const int CartEasy = 3, CartNormal = 1, CartHard = 0, CartExtreme = 0;

    // Season pity: factors over the config baselines, so config.json remains the Normal definition.
    private const double PityThresholdEasy = 0.6, PityThresholdHard = 1.6;
    private const double PityStepEasy = 1.5, PityStepHard = 0.5;
    private const double PityTrimEasy = 1.5, PityTrimHard = 0.5;

    /// <summary>How far the quota FLOOR moves, expressed as a fraction of its distance from 1.0.
    /// Easy 1.2 makes a 0.50 floor 0.40 (eases further); Hard 0.5 makes it 0.75 (eases less).</summary>
    private const double PityFloorSeverityEasy = 1.2, PityFloorSeverityHard = 0.5;

    public static DifficultyProfile Resolve(DifficultySettings settings, GameplayConfig config)
    {
        if (settings == null) throw new ArgumentNullException(nameof(settings));
        if (config == null) throw new ArgumentNullException(nameof(config));

        return new DifficultyProfile
        {
            StackFactor = Pick(settings.StackSize, StackEasy, StackNormal, StackHard, StackExtreme),
            QualityFactor = Pick(settings.QualityAsks, QualityEasy, QualityNormal, QualityHard, QualityExtreme),
            AskBandLow = AskBands.For(settings.StackSize).Low,
            AskBandHigh = AskBands.For(settings.StackSize).High,
            RequiredSlotsDelta = settings.RequiredSlots switch
            {
                DifficultyStep.Easy => -1,
                DifficultyStep.Hard => 1,
                _ => 0,   // Normal, and Extreme which uses RequireAllSlots instead
            },
            RequireAllSlots = settings.RequiredSlots == DifficultyStep.Extreme,
            RarityBias = Pick(settings.ItemRarity, RarityEasy, RarityNormal, RarityHard, RarityExtreme),

            JpEarnedFactor = Pick(settings.JpEarned, JpEasy, JpNormal, JpHard, JpExtreme),
            ShrinePriceFactor = Pick(settings.ShrinePrices, PriceEasy, PriceNormal, PriceHard, PriceExtreme),
            StartingGold = ScaleGold(config.StartingMoney,
                Pick(settings.StartingGold, GoldEasy, GoldNormal, GoldHard, GoldExtreme)),
            StartingCartSlots = Pick(settings.CartSlots, CartEasy, CartNormal, CartHard, CartExtreme),
            HoldPriceFactor = Pick(settings.HoldPrices, HoldEasy, HoldNormal, HoldHard, HoldExtreme),

            Pity = ResolvePity(settings.SeasonPity, config),
            Steps = settings.Clone(),
        };
    }

    /// <summary>Extreme disables easing outright but keeps the baselines intact, so a player who
    /// drops back to Normal gets the same curve he would have had. Config's own
    /// <see cref="GameplayConfig.PityEnabled"/> still wins: a step can turn pity off, never on.</summary>
    private static PityProfile ResolvePity(DifficultyStep step, GameplayConfig config)
    {
        double thresholdFactor = Pick(step, PityThresholdEasy, 1.0, PityThresholdHard, 1.0);
        double stepFactor = Pick(step, PityStepEasy, 1.0, PityStepHard, 1.0);
        double trimFactor = Pick(step, PityTrimEasy, 1.0, PityTrimHard, 1.0);
        double floorSeverity = Pick(step, PityFloorSeverityEasy, 1.0, PityFloorSeverityHard, 1.0);

        return new PityProfile
        {
            Enabled = config.PityEnabled && step != DifficultyStep.Extreme,
            Threshold = Math.Max(0, RoundToInt(config.PityThreshold * thresholdFactor)),
            QuotaStep = config.PityQuotaStep * stepFactor,
            QuotaFloor = Math.Clamp(1.0 - (1.0 - config.PityQuotaFloor) * floorSeverity, 0.0, 1.0),
            TrimPerStep = Math.Max(1, RoundToInt(config.PityTrimPerStep * trimFactor)),
        };
    }

    /// <summary>Extreme's 0.0x is an exact zero, not a rounding artefact, so the player really
    /// does start a loop with nothing.</summary>
    private static int ScaleGold(int baseGold, double factor)
        => factor <= 0.0 ? 0 : Math.Max(0, RoundToInt(baseGold * factor));

    private static T Pick<T>(DifficultyStep step, T easy, T normal, T hard, T extreme) => step switch
    {
        DifficultyStep.Easy => easy,
        DifficultyStep.Hard => hard,
        DifficultyStep.Extreme => extreme,
        _ => normal,
    };

    private static int RoundToInt(double value)
        => (int)Math.Round(value, MidpointRounding.AwayFromZero);
}
