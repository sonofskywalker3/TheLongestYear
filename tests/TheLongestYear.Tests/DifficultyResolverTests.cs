using TheLongestYear.Core;

namespace TheLongestYear.Tests;

public class DifficultyResolverTests
{
    /// <summary>The load-bearing guarantee of the whole feature: an untouched Difficulty section
    /// reproduces today's shipping balance field for field. Every existing save depends on this.</summary>
    [Fact]
    public void Normal_Resolves_To_Todays_Config_Values()
    {
        var cfg = new GameplayConfig();
        var p = DifficultyResolver.Resolve(new DifficultySettings(), cfg);

        Assert.Equal(1.0, p.StackFactor);
        Assert.Equal(1.0, p.QualityFactor);
        Assert.Equal(0, p.RequiredSlotsDelta);
        Assert.False(p.RequireAllSlots);
        Assert.Equal(1.0, p.RarityBias);
        Assert.Equal(1.0, p.JpEarnedFactor);
        Assert.Equal(1.0, p.ShrinePriceFactor);
        Assert.Equal(cfg.StartingMoney, p.StartingGold);
        Assert.Equal(CartSlotRules.MinSlots, p.StartingCartSlots);
        Assert.Equal(1.0, p.HoldPriceFactor);
    }

    [Theory]
    [InlineData(DifficultyStep.Easy, 1000)]
    [InlineData(DifficultyStep.Normal, 500)]
    [InlineData(DifficultyStep.Hard, 250)]
    [InlineData(DifficultyStep.Extreme, 0)]
    public void StartingGold_Scales_From_Config(DifficultyStep step, int expected)
    {
        var p = DifficultyResolver.Resolve(
            new DifficultySettings { StartingGold = step }, new GameplayConfig());

        Assert.Equal(expected, p.StartingGold);
    }

    /// <summary>The step scales whatever config.json says, rather than replacing it, so a player
    /// who hand-tuned StartingMoney keeps his baseline.</summary>
    [Fact]
    public void StartingGold_Scales_A_Hand_Tuned_Baseline()
    {
        var p = DifficultyResolver.Resolve(
            new DifficultySettings { StartingGold = DifficultyStep.Hard },
            new GameplayConfig { StartingMoney = 3000 });

        Assert.Equal(1500, p.StartingGold);
    }

    [Theory]
    [InlineData(DifficultyStep.Easy, 3)]
    [InlineData(DifficultyStep.Normal, 1)]
    [InlineData(DifficultyStep.Hard, 0)]
    [InlineData(DifficultyStep.Extreme, 0)]
    public void CartSlots_Ramp_Bottoms_Out_At_Hard(DifficultyStep step, int expected)
    {
        var p = DifficultyResolver.Resolve(
            new DifficultySettings { CartSlots = step }, new GameplayConfig());

        Assert.Equal(expected, p.StartingCartSlots);
    }

    [Theory]
    [InlineData(DifficultyStep.Easy, -1, false)]
    [InlineData(DifficultyStep.Normal, 0, false)]
    [InlineData(DifficultyStep.Hard, 1, false)]
    [InlineData(DifficultyStep.Extreme, 0, true)]
    public void RequiredSlots_Ramp(DifficultyStep step, int expectedDelta, bool expectedRequireAll)
    {
        var p = DifficultyResolver.Resolve(
            new DifficultySettings { RequiredSlots = step }, new GameplayConfig());

        Assert.Equal(expectedDelta, p.RequiredSlotsDelta);
        Assert.Equal(expectedRequireAll, p.RequireAllSlots);
    }

    [Theory]
    [InlineData(DifficultyStep.Easy, 1.5)]
    [InlineData(DifficultyStep.Normal, 1.0)]
    [InlineData(DifficultyStep.Hard, 0.75)]
    [InlineData(DifficultyStep.Extreme, 0.5)]
    public void JpEarned_Ramp_Runs_Downward(DifficultyStep step, double expected)
    {
        var p = DifficultyResolver.Resolve(
            new DifficultySettings { JpEarned = step }, new GameplayConfig());

        Assert.Equal(expected, p.JpEarnedFactor, 6);
    }

    [Fact]
    public void Steps_Are_Stamped_Alongside_The_Resolved_Values()
    {
        var settings = new DifficultySettings { ItemRarity = DifficultyStep.Extreme };
        var p = DifficultyResolver.Resolve(settings, new GameplayConfig());

        Assert.Equal(DifficultyStep.Extreme, p.Steps.ItemRarity);
    }

    /// <summary>A stamped profile must not alias the live config object: editing GMCM mid-run
    /// would otherwise mutate the stamp the current loop is reading.</summary>
    [Fact]
    public void The_Stamped_Steps_Are_A_Copy_Not_The_Live_Settings()
    {
        var settings = new DifficultySettings();
        var p = DifficultyResolver.Resolve(settings, new GameplayConfig());

        settings.JpEarned = DifficultyStep.Extreme;

        Assert.NotSame(settings, p.Steps);
        Assert.Equal(DifficultyStep.Normal, p.Steps.JpEarned);
    }

    [Fact]
    public void AsksAllNormal_Ignores_Economy_Steps()
    {
        var s = new DifficultySettings { JpEarned = DifficultyStep.Extreme };

        Assert.True(s.AsksAllNormal());
        Assert.False(s.IsAllNormal());
    }

    /// <summary>Item rarity cannot apply to a vanilla board, so it must not drag the Vanilla
    /// post-pass into running.</summary>
    [Fact]
    public void AsksAllNormal_Ignores_ItemRarity()
    {
        var s = new DifficultySettings { ItemRarity = DifficultyStep.Extreme };

        Assert.True(s.AsksAllNormal());
    }

    [Fact]
    public void AsksAllNormal_Is_False_When_An_Ask_Side_Step_Moves()
    {
        Assert.False(new DifficultySettings { StackSize = DifficultyStep.Hard }.AsksAllNormal());
        Assert.False(new DifficultySettings { QualityAsks = DifficultyStep.Easy }.AsksAllNormal());
        Assert.False(new DifficultySettings { RequiredSlots = DifficultyStep.Extreme }.AsksAllNormal());
    }

    [Fact]
    public void A_Default_Settings_Object_Is_All_Normal()
    {
        Assert.True(new DifficultySettings().IsAllNormal());
    }

    [Fact]
    public void An_Unknown_Step_Name_Parses_To_Normal()
    {
        Assert.Equal(DifficultyStep.Normal, DifficultySteps.Parse("Nightmare"));
        Assert.Equal(DifficultyStep.Normal, DifficultySteps.Parse(null));
        Assert.Equal(DifficultyStep.Normal, DifficultySteps.Parse(""));
        Assert.Equal(DifficultyStep.Hard, DifficultySteps.Parse("hard"));
    }
}
