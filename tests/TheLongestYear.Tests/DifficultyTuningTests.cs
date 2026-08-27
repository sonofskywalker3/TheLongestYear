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

    /// <summary>Stack size moved out of the tuning block entirely (Jeff's ruling 2026-08-27): it
    /// only reached re-rolled bundles there. StackScaling owns it now, so the tuning must not
    /// react to the stack dial at all or the two would double-count.</summary>
    [Fact]
    public void The_Stack_Dial_No_Longer_Touches_The_Tuning_Block()
    {
        var t = new BundleGenerationTuning();

        Assert.Same(t, DifficultyTuning.Scale(t, Profile(
            new DifficultySettings { StackSize = DifficultyStep.Extreme })));
    }

    [Fact]
    public void Scaling_Quality_Leaves_Every_Stack_Number_Alone()
    {
        var t = new BundleGenerationTuning();
        var s = DifficultyTuning.Scale(t, Profile(
            new DifficultySettings { QualityAsks = DifficultyStep.Extreme }));

        Assert.Equal(t.QualityCropStack, s.QualityCropStack);
        Assert.Equal(t.CheapMinStack, s.CheapMinStack);
        Assert.Equal(t.CheapMaxStack, s.CheapMaxStack);
        Assert.Equal(t.LargeQuantityMinStack, s.LargeQuantityMinStack);
        Assert.Equal(t.LargeQuantityMaxStack, s.LargeQuantityMaxStack);
        Assert.Equal(t.LargeQuantityForageChance, s.LargeQuantityForageChance, 6);
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

    /// <summary>Scaling quality must not disturb the stack numbers.</summary>
    [Fact]
    public void The_Two_Modifiers_Do_Not_Bleed_Into_Each_Other()
    {
        var t = new BundleGenerationTuning();

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
            new DifficultySettings { QualityAsks = DifficultyStep.Hard }));

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
