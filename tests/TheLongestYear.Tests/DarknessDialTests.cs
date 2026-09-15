using System.Text.Json;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>The eleventh dial, Darkness (spec 2026-09-15 Part B, section 2.3): set by the overall
/// lever with the other ten, stamped like the rest, and migrated to the LOWEST of the ten existing
/// dials when a config or a stamp predates it.</summary>
public class DarknessDialTests
{
    [Fact]
    public void A_fresh_config_reads_darkness_as_normal_until_it_is_set()
    {
        // The JSON key is absent on a fresh config too, so the property is null and the
        // accessor derives Normal from nine Normal dials; the first migration pins it.
        var settings = new DifficultySettings();
        Assert.Null(settings.Darkness);
        Assert.Equal(DifficultyStep.Normal, settings.DarknessOrLowest);
        Assert.True(settings.MigrateDarkness());
        Assert.Equal(DifficultyStep.Normal, settings.Darkness);
    }

    [Theory]
    [InlineData(DifficultyStep.Easy)]
    [InlineData(DifficultyStep.Extreme)]
    public void SetAll_sets_darkness_too(DifficultyStep step)
    {
        var settings = new DifficultySettings();
        settings.SetAll(step);
        Assert.Equal(step, settings.Darkness);
    }

    [Fact]
    public void Clone_carries_darkness()
        => Assert.Equal(DifficultyStep.Hard, new DifficultySettings { Darkness = DifficultyStep.Hard }.Clone().Darkness);

    [Fact]
    public void Lowest_dial_is_the_minimum_of_the_ten_and_ignores_the_lever_and_darkness()
    {
        var settings = new DifficultySettings { Overall = DifficultyStep.Extreme, Darkness = DifficultyStep.Extreme };
        settings.SetAll(DifficultyStep.Hard);
        settings.CartSlots = DifficultyStep.Easy;
        settings.Darkness = DifficultyStep.Extreme;
        Assert.Equal(DifficultyStep.Easy, settings.LowestDial());
    }

    [Fact]
    public void An_old_config_without_the_dial_migrates_to_the_lowest_dial_and_moves_the_lever()
    {
        var settings = JsonSerializer.Deserialize<DifficultySettings>(
            "{\"Overall\":3,\"StackSize\":3,\"QualityAsks\":3,\"RequiredSlots\":1,\"ItemRarity\":3}")!;
        Assert.Null(settings.Darkness);

        Assert.True(settings.MigrateDarkness());

        Assert.Equal(DifficultyStep.Normal, settings.Darkness);
        Assert.Equal(DifficultyStep.Normal, settings.Overall);
        Assert.False(settings.MigrateDarkness());
    }

    [Fact]
    public void A_config_that_already_has_the_dial_is_left_alone()
    {
        var settings = new DifficultySettings { Overall = DifficultyStep.Hard, Darkness = DifficultyStep.Extreme };
        Assert.False(settings.MigrateDarkness());
        Assert.Equal(DifficultyStep.Extreme, settings.Darkness);
        Assert.Equal(DifficultyStep.Hard, settings.Overall);
    }

    [Fact]
    public void The_profile_stamps_the_darkness_step()
    {
        var settings = new DifficultySettings { Darkness = DifficultyStep.Hard };
        DifficultyProfile profile = DifficultyResolver.Resolve(settings, new GameplayConfig());
        Assert.Equal(DifficultyStep.Hard, profile.DarknessStep);
        Assert.Equal(DifficultyStep.Hard, profile.Darkness);
    }

    [Fact]
    public void An_old_stamp_without_the_step_derives_the_lowest_dial()
    {
        var profile = JsonSerializer.Deserialize<DifficultyProfile>(
            "{\"StackFactor\":1.5,\"Steps\":{\"StackSize\":2,\"QualityAsks\":2,\"RequiredSlots\":2,\"ItemRarity\":2,\"JpEarned\":2,\"ShrinePrices\":2,\"StartingGold\":0,\"CartSlots\":2,\"HoldPrices\":2}}")!;
        Assert.Null(profile.DarknessStep);
        Assert.Equal(DifficultyStep.Easy, profile.Darkness);
    }

    [Fact]
    public void Darkness_off_normal_counts_as_a_changed_difficulty()
        => Assert.False(new DifficultySettings { Darkness = DifficultyStep.Hard }.IsAllNormal());
}
