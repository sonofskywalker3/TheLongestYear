using TheLongestYear.Core;
using TheLongestYear.Core.Sabotage;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>Spec 2026-09-15 Part B, sections 2.4 to 2.6: the numbers keyed by the Darkness dial.</summary>
public class DarknessLevelsTests
{
    [Theory]
    [InlineData(DifficultyStep.Easy, 0.04, 8, 12)]
    [InlineData(DifficultyStep.Normal, 0.05, 10, 15)]
    [InlineData(DifficultyStep.Hard, 0.06, 12, 18)]
    [InlineData(DifficultyStep.Extreme, 0.07, 14, 21)]
    public void Blight_share_and_caps_by_level(DifficultyStep level, double share, int summerCap, int otherCap)
    {
        Assert.Equal(share, DarknessLevels.BlightShare(level));
        Assert.Equal(summerCap, DarknessLevels.BlightCap(level, Season.Summer));
        Assert.Equal(otherCap, DarknessLevels.BlightCap(level, Season.Fall));
        Assert.Equal(otherCap, DarknessLevels.BlightCap(level, Season.Winter));
    }

    [Theory]
    [InlineData(0, Season.Summer, DifficultyStep.Normal, 0)]
    [InlineData(1, Season.Summer, DifficultyStep.Normal, 1)]
    [InlineData(10, Season.Summer, DifficultyStep.Normal, 1)]     // 5% of 10 rounds up to 1
    [InlineData(100, Season.Summer, DifficultyStep.Normal, 5)]
    [InlineData(1000, Season.Summer, DifficultyStep.Normal, 10)]  // Summer cap
    [InlineData(1000, Season.Fall, DifficultyStep.Normal, 15)]    // Fall cap
    [InlineData(100, Season.Winter, DifficultyStep.Easy, 4)]
    [InlineData(1000, Season.Winter, DifficultyStep.Extreme, 21)]
    [InlineData(100, Season.Summer, DifficultyStep.Extreme, 7)]
    public void Crop_count_is_the_level_share_clamped(int crops, Season season, DifficultyStep level, int expected)
        => Assert.Equal(expected, BlightRule.Count(crops, season, level));

    [Theory]
    [InlineData(0, Season.Summer, DifficultyStep.Normal, 0)]
    [InlineData(1, Season.Summer, DifficultyStep.Normal, 1)]
    [InlineData(100, Season.Summer, DifficultyStep.Normal, 5)]
    [InlineData(10000, Season.Summer, DifficultyStep.Normal, 10)]
    [InlineData(10000, Season.Fall, DifficultyStep.Hard, 18)]
    public void Storage_count_uses_the_same_share_and_caps(int units, Season season, DifficultyStep level, int expected)
        => Assert.Equal(expected, BlightRule.SpoilCount(units, season, level));

    [Theory]
    [InlineData(DifficultyStep.Easy, 0.0)]
    [InlineData(DifficultyStep.Normal, 0.0)]
    [InlineData(DifficultyStep.Hard, 0.10)]
    [InlineData(DifficultyStep.Extreme, 0.30)]
    public void Unmoderated_chance_by_level(DifficultyStep level, double chance)
        => Assert.Equal(chance, DarknessLevels.UnmoderatedChance(level));

    [Fact]
    public void Only_extreme_reaches_tools_and_machines()
    {
        Assert.False(DarknessLevels.StorageReachesEverything(DifficultyStep.Hard));
        Assert.True(DarknessLevels.StorageReachesEverything(DifficultyStep.Extreme));
        Assert.Equal(3, DarknessLevels.BigCraftableUnits);
    }

    [Fact]
    public void A_legendary_fish_is_always_one()
        => Assert.Equal(1, TamperRule.MaxCount("(O)163", 40.0));   // Legend

    [Fact]
    public void No_basis_means_no_max_so_the_stack_rule_asks_for_one()
    {
        Assert.Equal(0, TamperRule.MaxCount("(O)24", null));
        Assert.Equal(1, TamperRule.Stack(0, 2, DifficultyStep.Extreme, new System.Random(1)));
    }

    [Fact]
    public void Max_count_is_the_boards_own_ceiling_of_the_basis()
        => Assert.Equal(8, TamperRule.MaxCount("(O)24", 10.0));     // ceil(10 * 0.8)

    [Theory]
    [InlineData(1, DifficultyStep.Extreme)]
    [InlineData(4, DifficultyStep.Easy)]
    public void A_legendary_stays_one_through_the_stack_roll(int week, DifficultyStep level)
    {
        for (int seed = 0; seed < 20; seed++)
            Assert.Equal(1, TamperRule.Stack(TamperRule.MaxCount("(O)775", 30.0), week, level, new System.Random(seed)));
    }
}
