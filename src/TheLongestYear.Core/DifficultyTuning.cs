using System;

namespace TheLongestYear.Core;

/// <summary>Applies the quality-asks modifier by producing a SCALED CLONE of the generation
/// tuning block, rather than by changing how generation works.
///
/// This is deliberate: <see cref="BundleSlotFiller"/> and <see cref="AuthoredBundleComposer"/>
/// already read every stack number and quality chance off a <see cref="BundleGenerationTuning"/>,
/// so handing them a scaled one applies the quality modifier with zero edits to the generator.
///
/// STACK SIZE DELIBERATELY DOES NOT LIVE HERE ANY MORE. Scaling the tuning only moves bundles the
/// engine actually re-rolls, and the engine keeps every unthemed bundle exactly as vanilla wrote
/// it, so the dial reached barely half the board (measured: three bundles scaled, six did not).
/// It now runs over every finished slot through <see cref="StackScaling"/>, on both board
/// sources. Jeff's ruling 2026-08-27.
///
/// Spec 2026-08-26 difficulty-modifiers, section 3.2 (and 3.1 as amended).</summary>
public static class DifficultyTuning
{
    /// <summary>Silver and gold chances together may never exceed this, so a plain ask stays
    /// possible at every step. Without it, Extreme over a hand-tuned config could make every
    /// single slot carry a star.</summary>
    private const double MaxCombinedQualityChance = 0.90;

    /// <summary>Returns a clone with the quality chances scaled by <c>profile.QualityFactor</c>.
    /// Returns the SAME reference at 1.0, so the default path allocates nothing.</summary>
    public static BundleGenerationTuning Scale(BundleGenerationTuning tuning, DifficultyProfile profile)
    {
        if (tuning == null) throw new ArgumentNullException(nameof(tuning));
        if (profile == null) throw new ArgumentNullException(nameof(profile));

        if (profile.QualityFactor == 1.0)
            return tuning;

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

            // Stack numbers pass through untouched: StackScaling now owns the stack modifier and
            // applies it to the finished slots, so scaling them here as well would double-count.
            CheapPriceCeiling = tuning.CheapPriceCeiling,
            MidPriceCeiling = tuning.MidPriceCeiling,
            QualityCropStack = tuning.QualityCropStack,
            CheapMinStack = tuning.CheapMinStack,
            CheapMaxStack = tuning.CheapMaxStack,
            MidMinStack = tuning.MidMinStack,
            MidMaxStack = tuning.MidMaxStack,
            DearMinStack = tuning.DearMinStack,
            DearMaxStack = tuning.DearMaxStack,
            LargeQuantityMinStack = tuning.LargeQuantityMinStack,
            LargeQuantityMaxStack = tuning.LargeQuantityMaxStack,
            LargeQuantityForageChance = tuning.LargeQuantityForageChance,

            // ---- Quality asks ----
            SilverQualityChance = silver,
            GoldQualityChance = gold,
        };
    }

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
