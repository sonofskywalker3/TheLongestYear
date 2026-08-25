using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class SeasonEaseTests
{
    private static BundleRequirement Percentage(int[] ramp) => BundleRequirement.CreatePercentage(
        "Artisan", Theme.Farming, new[] { "(O)1", "(O)2", "(O)3", "(O)4", "(O)5", "(O)6", "(O)7" }, 6, ramp);

    [Fact]
    public void Percentage_only_the_eased_season_drops_and_ramp_stays_monotonic()
    {
        var req = Percentage(new[] { 3, 4, 5, 6 });
        var eased = SeasonEase.Apply(req, new SeasonEase(Season.Summer, 2, 0.8));
        // ceil(4 * 0.8) = 4 -> no change at 4; use 5 to see it: ceil(5*0.8)=4
        Assert.Equal(new[] { 3, 4, 5, 6 }, eased.CumulativeRequiredBySeason);

        var req2 = Percentage(new[] { 5, 5, 5, 6 });
        var eased2 = SeasonEase.Apply(req2, new SeasonEase(Season.Spring, 3, 0.7));
        Assert.Equal(new[] { 4, 5, 5, 6 }, eased2.CumulativeRequiredBySeason);   // ceil(5*0.7)=4, later seasons untouched
    }

    [Fact]
    public void Percentage_winter_still_demands_completion_and_floor_applies()
    {
        var req = Percentage(new[] { 6, 6, 6, 6 });
        var eased = SeasonEase.Apply(req, new SeasonEase(Season.Winter, 9, 0.5));
        Assert.Equal(new[] { 6, 6, 6, 6 }, eased.CumulativeRequiredBySeason);   // Winter never eases
    }

    [Fact]
    public void Percentage_zero_stays_zero_and_nonzero_stays_at_least_one()
    {
        var req = Percentage(new[] { 0, 1, 3, 6 });
        var eased = SeasonEase.Apply(req, new SeasonEase(Season.Summer, 5, 0.5));
        Assert.Equal(new[] { 0, 1, 3, 6 }, eased.CumulativeRequiredBySeason);
    }

    [Fact]
    public void PerItem_pins_due_in_eased_season_slide_one_per_step_capped_at_winter()
    {
        var req = BundleRequirement.CreatePerItem("Blacksmith", Theme.Mining, new Dictionary<string, Season>
        {
            ["(O)334"] = Season.Spring, ["(O)335"] = Season.Summer, ["(O)336"] = Season.Fall,
        });
        var eased = SeasonEase.Apply(req, new SeasonEase(Season.Spring, 2, 0.8));
        Assert.Equal(Season.Fall, eased.ItemSeasonPins!["(O)334"]);
        Assert.Equal(Season.Summer, eased.ItemSeasonPins["(O)335"]);
        var capped = SeasonEase.Apply(req, new SeasonEase(Season.Fall, 5, 0.5));
        Assert.Equal(Season.Winter, capped.ItemSeasonPins!["(O)336"]);
        Assert.Equal(Season.Spring, capped.ItemSeasonPins["(O)334"]);
    }

    [Fact]
    public void Seasonal_due_season_slides_like_per_item()
    {
        var req = BundleRequirement.CreateSeasonal("Spring Crops", Theme.Farming, new[] { "(O)24" }, Season.Spring);
        var eased = SeasonEase.Apply(req, new SeasonEase(Season.Spring, 1, 0.9));
        Assert.Equal(Season.Summer, eased.SeasonalSeason);
        Assert.Same(req, SeasonEase.Apply(req, new SeasonEase(Season.Summer, 1, 0.9)));   // other seasons untouched
    }

    [Fact]
    public void Zero_steps_returns_same_instance()
    {
        var req = Percentage(new[] { 3, 4, 5, 6 });
        Assert.Same(req, SeasonEase.Apply(req, new SeasonEase(Season.Spring, 0, 1.0)));
    }

    [Fact]
    public void BuildRequirements_applies_ease_after_clamp()
    {
        var set = new GeneratedBundleSet(new[]
        {
            new BundleSpec("Pantry", 1, "Totally Unknown Bundle", "Totally Unknown Bundle", "O 495 30", 0, 2,
                new[] { "(O)24", "(O)188", "(O)190" }.Select(id => new BundleSlotSpec(id, 1, 0)).ToList()),
        });
        var plain = set.BuildRequirements(new Dictionary<string, Season>(), GameplayConfig.DefaultBundleQuotas);
        var eased = set.BuildRequirements(new Dictionary<string, Season>(), GameplayConfig.DefaultBundleQuotas,
            new SeasonEase(Season.Spring, 5, 0.5));
        int plainSpring = plain[0].CumulativeRequiredBySeason![0];
        int easedSpring = eased[0].CumulativeRequiredBySeason![0];
        Assert.True(easedSpring <= plainSpring);
        Assert.Equal(plain[0].CumulativeRequiredBySeason![3], eased[0].CumulativeRequiredBySeason![3]);
    }
}
