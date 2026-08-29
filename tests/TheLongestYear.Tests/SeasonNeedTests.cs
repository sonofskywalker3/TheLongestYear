using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>Jeff's rule (2026-08-28): goals follow the gate exactly, no look-ahead. A bundle may
/// be asked for only what the current season's gate already demands; a player ahead of the gate
/// sees quiet cards until the next season opens more of the bundle up, by design.</summary>
public class SeasonNeedTests
{
    [Fact]
    public void A_percentage_bundle_may_be_asked_only_for_this_seasons_share()
    {
        BundleRequirement req = BundleRequirement.CreatePercentage("Recycler's", Theme.Mixed,
            new[] { "(O)168", "(O)169", "(O)170", "(O)171", "(O)172", "(O)338" }, numberOfSlots: 4,
            cumulativeRequiredBySeason: new[] { 1, 2, 3, 4 });
        Assert.Equal(1, SeasonNeed.For(req, Season.Spring, completed: 0));
        Assert.Equal(0, SeasonNeed.For(req, Season.Spring, completed: 1));
        Assert.Equal(1, SeasonNeed.For(req, Season.Fall, completed: 2));
        Assert.Equal(4, SeasonNeed.For(req, Season.Winter, completed: 0));
    }

    [Fact]
    public void A_per_item_bundle_may_be_asked_only_for_items_due_by_now()
    {
        var pins = new Dictionary<string, Season> { ["(O)153"] = Season.Spring, ["(O)700"] = Season.Fall, ["(O)140"] = Season.Winter, ["(O)141"] = Season.Winter };
        BundleRequirement req = BundleRequirement.CreatePerItem("Lake Fish", Theme.Fishing, pins.Keys.ToList(), pins);
        Assert.Equal(1, SeasonNeed.For(req, Season.Spring, 0));
        Assert.Equal(1, SeasonNeed.For(req, Season.Summer, 0));
        Assert.Equal(2, SeasonNeed.For(req, Season.Fall, 0));
        Assert.Equal(4, SeasonNeed.For(req, Season.Winter, 0));
    }
}
