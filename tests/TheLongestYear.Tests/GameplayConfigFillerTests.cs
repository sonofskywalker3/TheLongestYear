using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class GameplayConfigFillerTests
{
    [Fact]
    public void Filler_allowance_defaults_to_the_spec_ramp()
    {
        var config = new GameplayConfig();
        Assert.Equal(new[] { 0, 1, 2, 99 }, config.ThemeFillerBySeason);
        Assert.Equal(0, config.FillerAllowanceFor(Season.Spring));
        Assert.Equal(2, config.FillerAllowanceFor(Season.Fall));
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
