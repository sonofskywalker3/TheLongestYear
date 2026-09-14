using System.Text.Json;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>The overall Difficulty lever (Jeff, 2026-09-14): picking a level sets all ten dials,
/// each dial can still be changed afterwards, and the lever itself never changes gameplay.</summary>
public class DifficultyLeverTests
{
    [Fact]
    public void Overall_defaults_to_normal()
        => Assert.Equal(DifficultyStep.Normal, new DifficultySettings().Overall);

    [Theory]
    [InlineData(DifficultyStep.Easy)]
    [InlineData(DifficultyStep.Hard)]
    [InlineData(DifficultyStep.Extreme)]
    public void SetAll_sets_the_lever_and_every_dial(DifficultyStep step)
    {
        var settings = new DifficultySettings();
        settings.SetAll(step);

        Assert.Equal(step, settings.Overall);
        Assert.All(
            new[]
            {
                settings.StackSize, settings.QualityAsks, settings.RequiredSlots, settings.ItemRarity,
                settings.JpEarned, settings.ShrinePrices, settings.StartingGold, settings.CartSlots,
                settings.HoldPrices,
            },
            dial => Assert.Equal(step, dial));
    }

    [Fact]
    public void A_dial_changed_after_the_lever_keeps_its_own_value()
    {
        var settings = new DifficultySettings();
        settings.SetAll(DifficultyStep.Hard);
        settings.StartingGold = DifficultyStep.Easy;

        Assert.Equal(DifficultyStep.Hard, settings.Overall);
        Assert.Equal(DifficultyStep.Easy, settings.StartingGold);
        Assert.Equal(DifficultyStep.Hard, settings.StackSize);
    }

    [Fact]
    public void The_lever_alone_never_counts_as_a_changed_difficulty()
    {
        var settings = new DifficultySettings { Overall = DifficultyStep.Extreme };

        Assert.True(settings.IsAllNormal());
        Assert.Equal(
            DifficultyResolver.Resolve(new DifficultySettings(), new GameplayConfig()).StackFactor,
            DifficultyResolver.Resolve(settings, new GameplayConfig()).StackFactor);
    }

    [Fact]
    public void Old_config_without_the_lever_loads_as_normal_and_keeps_its_dials()
    {
        var settings = JsonSerializer.Deserialize<DifficultySettings>("{\"StackSize\":2}")!;

        Assert.Equal(DifficultyStep.Normal, settings.Overall);
        Assert.Equal(DifficultyStep.Hard, settings.StackSize);
    }

    [Fact]
    public void Clone_carries_the_lever()
        => Assert.Equal(DifficultyStep.Hard, new DifficultySettings { Overall = DifficultyStep.Hard }.Clone().Overall);
}
