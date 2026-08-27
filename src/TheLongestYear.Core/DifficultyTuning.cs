using System;

namespace TheLongestYear.Core;

/// <summary>Applies the stack-size and quality-asks modifiers by producing a SCALED CLONE of the
/// generation tuning block, rather than by changing how generation works.
///
/// This is deliberate: <see cref="BundleSlotFiller"/> and <see cref="AuthoredBundleComposer"/>
/// already read every stack number and quality chance off a <see cref="BundleGenerationTuning"/>,
/// so handing them a scaled one applies two of the four ask-side modifiers with zero edits to the
/// generator and zero risk to the existing generation tests.
///
/// Spec 2026-08-26 difficulty-modifiers, sections 3.1 and 3.2.</summary>
public static class DifficultyTuning
{
    /// <summary>A bundle slot asking for more than one inventory stack of a 99-cap item reads as
    /// a bug rather than as difficulty.</summary>
    private const int MaxStack = 99;

    private const int MinStack = 1;

    /// <summary>Silver and gold chances together may never exceed this, so a plain ask stays
    /// possible at every step. Without it, Extreme over a hand-tuned config could make every
    /// single slot carry a star.</summary>
    private const double MaxCombinedQualityChance = 0.90;

    /// <summary>Returns a clone with stack numbers scaled by <c>profile.StackFactor</c> and
    /// quality chances by <c>profile.QualityFactor</c>. Returns the SAME reference when both are
    /// 1.0, so the default path allocates nothing and cannot drift.</summary>
    public static BundleGenerationTuning Scale(BundleGenerationTuning tuning, DifficultyProfile profile)
    {
        if (tuning == null) throw new ArgumentNullException(nameof(tuning));
        if (profile == null) throw new ArgumentNullException(nameof(profile));

        if (profile.StackFactor == 1.0 && profile.QualityFactor == 1.0)
            return tuning;

        double stack = profile.StackFactor;
        (double silver, double gold) = ClampQuality(
            tuning.SilverQualityChance * profile.QualityFactor,
            tuning.GoldQualityChance * profile.QualityFactor);

        return new BundleGenerationTuning
        {
            // Sampling weights and the exclude/addition lists are identity, not difficulty. The
            // collections are shared by reference because nothing downstream mutates them.
            VanillaItemWeight = tuning.VanillaItemWeight,
            ModdedItemWeight = tuning.ModdedItemWeight,
            RareRollWeights = tuning.RareRollWeights,
            ExcludedItemIds = tuning.ExcludedItemIds,
            ExcludedLocationMarkers = tuning.ExcludedLocationMarkers,
            QualityIneligibleItemIds = tuning.QualityIneligibleItemIds,
            SeasonalForageAdditions = tuning.SeasonalForageAdditions,
            CropPoolAdditions = tuning.CropPoolAdditions,
            VaultAmountMultiplier = tuning.VaultAmountMultiplier,
            TrophyShownCount = tuning.TrophyShownCount,
            TrophyRequiredCount = tuning.TrophyRequiredCount,

            // Price BANDS decide which stack range a monster drop falls into. They are prices,
            // not stacks, so scaling them would silently reclassify items instead of changing
            // how many are asked for.
            CheapPriceCeiling = tuning.CheapPriceCeiling,
            MidPriceCeiling = tuning.MidPriceCeiling,

            // ---- Stack size ----
            QualityCropStack = ScaleStack(tuning.QualityCropStack, stack),
            CheapMinStack = ScaleStack(tuning.CheapMinStack, stack),
            CheapMaxStack = ScaleStack(tuning.CheapMaxStack, stack),
            MidMinStack = ScaleStack(tuning.MidMinStack, stack),
            MidMaxStack = ScaleStack(tuning.MidMaxStack, stack),
            DearMinStack = ScaleStack(tuning.DearMinStack, stack),
            DearMaxStack = ScaleStack(tuning.DearMaxStack, stack),
            LargeQuantityMinStack = ScaleStack(tuning.LargeQuantityMinStack, stack),
            LargeQuantityMaxStack = ScaleStack(tuning.LargeQuantityMaxStack, stack),
            LargeQuantityForageChance =
                Math.Clamp(tuning.LargeQuantityForageChance * stack, 0.0, 1.0),

            // ---- Quality asks ----
            SilverQualityChance = silver,
            GoldQualityChance = gold,
        };
    }

    private static int ScaleStack(int value, double factor)
        => Math.Clamp((int)Math.Round(value * factor, MidpointRounding.AwayFromZero), MinStack, MaxStack);

    /// <summary>Scales both chances down proportionally when their sum overflows the cap, so the
    /// silver/gold RATIO the config author chose survives the clamp.</summary>
    private static (double Silver, double Gold) ClampQuality(double silver, double gold)
    {
        // Order matters. Clamping each value to 1.0 BEFORE the sum check would flatten the ratio
        // whenever one of the two overflowed on its own (0.6 and 0.3 at Extreme become 1.0 and
        // 0.9, a ratio of 1.1 rather than 2.0). Shrink against the raw sum first, then clamp.
        silver = Math.Max(0.0, silver);
        gold = Math.Max(0.0, gold);

        double sum = silver + gold;
        if (sum > MaxCombinedQualityChance && sum > 0.0)
        {
            double shrink = MaxCombinedQualityChance / sum;
            silver *= shrink;
            gold *= shrink;
        }

        return (Math.Clamp(silver, 0.0, 1.0), Math.Clamp(gold, 0.0, 1.0));
    }
}
