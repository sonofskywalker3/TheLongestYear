using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class GameplayConfigFillerTests
{
    [Fact]
    public void Week_overrides_default_empty_and_caps_are_flat()
    {
        var config = new GameplayConfig();
        Assert.Empty(config.AvailabilityWeekOverrides);
        Assert.Equal(new[] { 5, 5, 5, 5 }, BonusItemSampler.DefaultMaxCountBySeason);
    }

    [Fact]
    public void Filler_allowance_defaults_to_the_spec_ramp()
    {
        var config = new GameplayConfig();
        Assert.Equal(new[] { 99, 99, 99, 99 }, config.ThemeFillerBySeason);
        Assert.Equal(GoalSamplingRules.UnlimitedFiller, config.FillerAllowanceFor(Season.Spring));
        Assert.Equal(GoalSamplingRules.UnlimitedFiller, config.FillerAllowanceFor(Season.Fall));
        Assert.Equal(GoalSamplingRules.UnlimitedFiller, config.FillerAllowanceFor(Season.Winter));
    }

    [Fact]
    public void A_short_or_missing_table_reads_as_unlimited()
    {
        var config = new GameplayConfig { ThemeFillerBySeason = new() { 0 } };
        Assert.Equal(GoalSamplingRules.UnlimitedFiller, config.FillerAllowanceFor(Season.Summer));
        config.ThemeFillerBySeason = null!;
        Assert.Equal(GoalSamplingRules.UnlimitedFiller, config.FillerAllowanceFor(Season.Spring));
    }

    /// <summary>Season pity was removed 2026-09-24. A config.json from before that still lists its
    /// keys (and a SeasonPity step under Difficulty); it must load with every other value intact.
    /// SMAPI reads config through Newtonsoft, which skips unknown keys the same way.</summary>
    [Fact]
    public void An_old_config_with_season_pity_keys_still_loads()
    {
        const string json = @"{
            ""StartingMoney"": 750,
            ""PityEnabled"": false,
            ""PityThreshold"": 3,
            ""PityQuotaStep"": 0.2,
            ""PityQuotaFloor"": 0.4,
            ""PityTrimPerStep"": 4,
            ""PityCosts"": [0, 10],
            ""Difficulty"": { ""HoldPrices"": 2, ""SeasonPity"": 0 }
        }";

        GameplayConfig config = System.Text.Json.JsonSerializer.Deserialize<GameplayConfig>(json)!;

        Assert.Equal(750, config.StartingMoney);
        Assert.Equal(DifficultyStep.Hard, config.Difficulty.HoldPrices);
        Assert.NotNull(DifficultyResolver.Resolve(config.Difficulty, config));
    }
}
