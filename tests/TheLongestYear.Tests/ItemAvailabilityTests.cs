using System.Collections.Generic;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class ItemAvailabilityTests
{
    private static ItemAvailabilityModel Model(
        Dictionary<string, ItemAvailability>? derived = null,
        Dictionary<string, Season>? seasonOverrides = null,
        Dictionary<string, int>? effortOverrides = null)
        => new ItemAvailabilityModel(
            derived ?? new Dictionary<string, ItemAvailability>(),
            seasonOverrides, effortOverrides);

    [Fact]
    public void A_Derived_Item_Comes_Back_As_Derived()
    {
        var model = Model(new Dictionary<string, ItemAvailability>
        {
            ["(O)128"] = new ItemAvailability(Season.Summer, 7, "summer-only fish"),
        });

        ItemAvailability result = model.For("(O)128");

        Assert.Equal(Season.Summer, result.EarliestSeason);
        Assert.Equal(7, result.Effort);
        Assert.Equal("summer-only fish", result.Basis);
    }

    /// <summary>An unrecognised item floors at WINTER, not Spring. Deadlines clamp UPWARD to the
    /// floor, so a floor guessed too early permits an impossible gate, which bricks a run. Late is
    /// merely lenient. Spec section 3.1.</summary>
    [Fact]
    public void An_Unknown_Item_Floors_At_Winter_And_Is_Recorded()
    {
        var model = Model();

        ItemAvailability result = model.For("(O)9999");

        Assert.Equal(Season.Winter, result.EarliestSeason);
        Assert.Equal(ItemAvailabilityModel.UnrecognisedEffort, result.Effort);
        Assert.Contains("no derivation rule", result.Basis);
        Assert.Contains("(O)9999", model.UnrecognisedIds);
    }

    [Fact]
    public void A_Season_Override_Replaces_The_Derived_Floor_And_Says_So()
    {
        var model = Model(
            new Dictionary<string, ItemAvailability>
            {
                ["(O)128"] = new ItemAvailability(Season.Summer, 7, "summer-only fish"),
            },
            seasonOverrides: new Dictionary<string, Season> { ["(O)128"] = Season.Fall });

        ItemAvailability result = model.For("(O)128");

        Assert.Equal(Season.Fall, result.EarliestSeason);
        Assert.Equal(7, result.Effort);
        Assert.Contains("override", result.Basis);
        Assert.Contains("summer-only fish", result.Basis);
    }

    [Fact]
    public void An_Effort_Override_Replaces_Only_The_Effort()
    {
        var model = Model(
            new Dictionary<string, ItemAvailability>
            {
                ["(O)128"] = new ItemAvailability(Season.Summer, 7, "summer-only fish"),
            },
            effortOverrides: new Dictionary<string, int> { ["(O)128"] = 2 });

        ItemAvailability result = model.For("(O)128");

        Assert.Equal(Season.Summer, result.EarliestSeason);
        Assert.Equal(2, result.Effort);
    }

    [Fact]
    public void An_Override_Applies_To_An_Item_With_No_Derived_Entry()
    {
        var model = Model(
            seasonOverrides: new Dictionary<string, Season> { ["(O)9999"] = Season.Spring });

        ItemAvailability result = model.For("(O)9999");

        Assert.Equal(Season.Spring, result.EarliestSeason);
    }

    [Fact]
    public void Lookup_Is_Ordinal_Not_Case_Insensitive()
    {
        var model = Model(new Dictionary<string, ItemAvailability>
        {
            ["(O)Sunfish"] = new ItemAvailability(Season.Spring, 1, "test"),
        });

        Assert.Equal(Season.Winter, model.For("(o)sunfish").EarliestSeason);
    }
}
