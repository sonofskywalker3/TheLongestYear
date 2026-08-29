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
}
