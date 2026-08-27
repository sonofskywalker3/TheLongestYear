using TheLongestYear.Core;

namespace TheLongestYear.Tests;

public class DifficultyTuningTests
{
    private static DifficultyProfile Profile(DifficultySettings settings)
        => DifficultyResolver.Resolve(settings, new GameplayConfig());

    [Fact]
    public void Normal_Returns_The_Same_Instance()
    {
        var t = new BundleGenerationTuning();

        Assert.Same(t, DifficultyTuning.Scale(t, Profile(new DifficultySettings())));
    }

    [Fact]
    public void Hard_Scales_Stacks_And_Leaves_Price_Bands_Alone()
    {
        var t = new BundleGenerationTuning();
        var s = DifficultyTuning.Scale(t, Profile(
            new DifficultySettings { StackSize = DifficultyStep.Hard }));

        Assert.Equal(8, s.QualityCropStack);   // 5 * 1.5 = 7.5, away from zero
        Assert.Equal(30, s.CheapMinStack);     // 20 * 1.5
        Assert.Equal(99, s.CheapMaxStack);     // 99 * 1.5, capped
        Assert.Equal(8, s.MidMinStack);        // 5 * 1.5 = 7.5
        Assert.Equal(30, s.MidMaxStack);       // 20 * 1.5

        Assert.Equal(t.CheapPriceCeiling, s.CheapPriceCeiling);
        Assert.Equal(t.MidPriceCeiling, s.MidPriceCeiling);
    }

    [Fact]
    public void Stacks_Never_Fall_Below_One()
    {
        var t = new BundleGenerationTuning { DearMinStack = 1 };
        var s = DifficultyTuning.Scale(t, Profile(
            new DifficultySettings { StackSize = DifficultyStep.Easy }));

        Assert.Equal(1, s.DearMinStack);
    }

    [Fact]
    public void Stacks_Are_Capped_At_Ninety_Nine()
    {
        var t = new BundleGenerationTuning { LargeQuantityMaxStack = 99 };
        var s = DifficultyTuning.Scale(t, Profile(
            new DifficultySettings { StackSize = DifficultyStep.Extreme }));

        Assert.Equal(99, s.LargeQuantityMaxStack);
    }

    [Fact]
    public void The_Big_Forage_Ask_Gets_More_Likely_On_Hard()
    {
        var t = new BundleGenerationTuning();
        var s = DifficultyTuning.Scale(t, Profile(
            new DifficultySettings { StackSize = DifficultyStep.Hard }));

        Assert.Equal(0.30, s.LargeQuantityForageChance, 6);   // 0.20 * 1.5
    }

    [Fact]
    public void The_Big_Forage_Chance_Can_Never_Exceed_Certainty()
    {
        var t = new BundleGenerationTuning { LargeQuantityForageChance = 0.8 };
        var s = DifficultyTuning.Scale(t, Profile(
            new DifficultySettings { StackSize = DifficultyStep.Extreme }));

        Assert.Equal(1.0, s.LargeQuantityForageChance, 6);
    }

    [Fact]
    public void Hard_Quality_Doubles_The_Default_Chances()
    {
        var s = DifficultyTuning.Scale(new BundleGenerationTuning(), Profile(
            new DifficultySettings { QualityAsks = DifficultyStep.Hard }));

        Assert.Equal(0.20, s.SilverQualityChance, 6);
        Assert.Equal(0.10, s.GoldQualityChance, 6);
    }

    [Fact]
    public void Easy_Quality_Halves_The_Default_Chances()
    {
        var s = DifficultyTuning.Scale(new BundleGenerationTuning(), Profile(
            new DifficultySettings { QualityAsks = DifficultyStep.Easy }));

        Assert.Equal(0.05, s.SilverQualityChance, 6);
        Assert.Equal(0.025, s.GoldQualityChance, 6);
    }

    /// <summary>A plain ask has to stay reachable at every step, or Extreme over a hand-tuned
    /// config would star every slot on the board.</summary>
    [Fact]
    public void Extreme_Quality_Is_Clamped_So_A_Plain_Ask_Stays_Possible()
    {
        var t = new BundleGenerationTuning { SilverQualityChance = 0.5, GoldQualityChance = 0.5 };
        var s = DifficultyTuning.Scale(t, Profile(
            new DifficultySettings { QualityAsks = DifficultyStep.Extreme }));

        Assert.Equal(0.90, s.SilverQualityChance + s.GoldQualityChance, 6);
    }

    /// <summary>The clamp scales both down proportionally, so the ratio the config author chose
    /// survives it.</summary>
    [Fact]
    public void The_Quality_Clamp_Preserves_The_Silver_To_Gold_Ratio()
    {
        var t = new BundleGenerationTuning { SilverQualityChance = 0.6, GoldQualityChance = 0.3 };
        var s = DifficultyTuning.Scale(t, Profile(
            new DifficultySettings { QualityAsks = DifficultyStep.Extreme }));

        Assert.Equal(2.0, s.SilverQualityChance / s.GoldQualityChance, 6);
    }

    /// <summary>Scaling stacks alone must not disturb the quality chances, and vice versa.</summary>
    [Fact]
    public void The_Two_Modifiers_Do_Not_Bleed_Into_Each_Other()
    {
        var t = new BundleGenerationTuning();

        var stacksOnly = DifficultyTuning.Scale(t, Profile(
            new DifficultySettings { StackSize = DifficultyStep.Extreme }));
        Assert.Equal(t.SilverQualityChance, stacksOnly.SilverQualityChance, 6);
        Assert.Equal(t.GoldQualityChance, stacksOnly.GoldQualityChance, 6);

        var qualityOnly = DifficultyTuning.Scale(t, Profile(
            new DifficultySettings { QualityAsks = DifficultyStep.Extreme }));
        Assert.Equal(t.QualityCropStack, qualityOnly.QualityCropStack);
        Assert.Equal(t.CheapMinStack, qualityOnly.CheapMinStack);
    }

    /// <summary>The exclude lists and curated pool additions are identity, not difficulty: losing
    /// them would reintroduce island saplings and gold-star algae.</summary>
    [Fact]
    public void Identity_Collections_Survive_The_Clone()
    {
        var t = new BundleGenerationTuning();
        var s = DifficultyTuning.Scale(t, Profile(
            new DifficultySettings { StackSize = DifficultyStep.Hard }));

        Assert.Same(t.ExcludedItemIds, s.ExcludedItemIds);
        Assert.Same(t.ExcludedLocationMarkers, s.ExcludedLocationMarkers);
        Assert.Same(t.QualityIneligibleItemIds, s.QualityIneligibleItemIds);
        Assert.Same(t.SeasonalForageAdditions, s.SeasonalForageAdditions);
        Assert.Same(t.CropPoolAdditions, s.CropPoolAdditions);
        Assert.Same(t.RareRollWeights, s.RareRollWeights);
        Assert.Equal(t.VaultAmountMultiplier, s.VaultAmountMultiplier);
        Assert.Equal(t.TrophyShownCount, s.TrophyShownCount);
        Assert.Equal(t.TrophyRequiredCount, s.TrophyRequiredCount);
        Assert.Equal(t.VanillaItemWeight, s.VanillaItemWeight);
        Assert.Equal(t.ModdedItemWeight, s.ModdedItemWeight);
    }
}
