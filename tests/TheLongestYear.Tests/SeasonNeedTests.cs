using System.Collections.Generic;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>Spec 2026-08-28-even-year, sim H: the weekly goals follow the gate exactly for a
/// pick-X-of-Y bundle, so its Winter share cannot be pulled forward.</summary>
public class SeasonNeedTests
{
    private static BundleRequirement Percentage()
        => BundleRequirement.CreatePercentage("Preserver's", Theme.Farming,
            new[] { "a", "b", "c", "d", "e", "f" }, numberOfSlots: 4,
            cumulativeRequiredBySeason: new[] { 1, 2, 3, 4 });

    [Theory]
    [InlineData(Season.Spring, 0, 1)]
    [InlineData(Season.Spring, 1, 0)]
    [InlineData(Season.Summer, 1, 1)]
    [InlineData(Season.Fall, 1, 2)]
    [InlineData(Season.Winter, 1, 3)]
    [InlineData(Season.Winter, 4, 0)]
    public void Percentage_bundle_need_follows_the_ramp(Season season, int completed, int expected)
        => Assert.Equal(expected, SeasonNeed.For(Percentage(), season, completed));

    [Fact]
    public void Per_item_bundle_need_is_required_minus_completed()
    {
        var req = BundleRequirement.CreatePerItem("Blacksmith's", Theme.Mining, new[] { "a", "b", "c" },
            new Dictionary<string, Season> { ["a"] = Season.Spring, ["b"] = Season.Summer, ["c"] = Season.Fall });
        Assert.Equal(2, SeasonNeed.For(req, Season.Spring, 1));
    }
}
