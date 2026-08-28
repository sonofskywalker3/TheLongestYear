using System.Collections.Generic;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>Jeff's rule (2026-08-28): the goals may run half a season ahead of the gate, never
/// further, never quiet while the board still needs lines.</summary>
public class SeasonNeedTests
{
    private static BundleRequirement Percentage()
        => BundleRequirement.CreatePercentage("Preserver's", Theme.Farming,
            new[] { "a", "b", "c", "d", "e", "f" }, numberOfSlots: 4,
            cumulativeRequiredBySeason: new[] { 1, 2, 3, 4 });

    [Theory]
    [InlineData(Season.Spring, 0, 2)]   // 1 due + half of the 1 Summer adds
    [InlineData(Season.Spring, 2, 0)]
    [InlineData(Season.Summer, 1, 2)]   // 2 + half of 1 = 3, minus 1 in
    [InlineData(Season.Fall, 1, 3)]     // 3 + half of 1 = 4, minus 1
    [InlineData(Season.Winter, 1, 3)]
    [InlineData(Season.Winter, 4, 0)]
    public void Percentage_bundle_runs_half_a_season_ahead(Season season, int completed, int expected)
        => Assert.Equal(expected, SeasonNeed.For(Percentage(), season, completed));

    [Fact]
    public void Per_item_bundle_counts_pins_by_season()
    {
        var req = BundleRequirement.CreatePerItem("Blacksmith's", Theme.Mining, new[] { "a", "b", "c", "d" },
            new Dictionary<string, Season> { ["a"] = Season.Spring, ["b"] = Season.Summer, ["c"] = Season.Summer, ["d"] = Season.Winter });
        Assert.Equal(2, SeasonNeed.For(req, Season.Spring, 0));   // a, plus half of (b, c)
        Assert.Equal(2, SeasonNeed.For(req, Season.Summer, 1));   // a, b, c minus one in
        Assert.Equal(3, SeasonNeed.For(req, Season.Fall, 1));     // a, b, c + half of d
        Assert.Equal(3, SeasonNeed.For(req, Season.Winter, 1));
    }
}
