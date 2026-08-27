using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class BundleDeadlinesTests
{
    private static ItemAvailabilityModel Model(params (string Id, Season Floor, int Effort)[] items)
        => new ItemAvailabilityModel(
            items.ToDictionary(
                i => i.Id,
                i => new ItemAvailability(i.Floor, i.Effort, "test"),
                System.StringComparer.Ordinal));

    [Fact]
    public void Four_Easy_Items_Spread_One_Per_Checkpoint_Easiest_First()
    {
        var model = Model(
            ("(O)a", Season.Spring, 1),
            ("(O)b", Season.Spring, 2),
            ("(O)c", Season.Spring, 3),
            ("(O)d", Season.Spring, 4));

        var result = BundleDeadlines.For(
            new List<string> { "(O)d", "(O)c", "(O)b", "(O)a" }, model);

        Assert.Equal(Season.Spring, result["(O)a"]);
        Assert.Equal(Season.Summer, result["(O)b"]);
        Assert.Equal(Season.Fall, result["(O)c"]);
        Assert.Equal(Season.Winter, result["(O)d"]);
    }

    /// <summary>Helper's has two ingredients. A two item bundle backs against Winter rather than
    /// starting at Spring, so it asks at Fall and Winter.</summary>
    [Fact]
    public void Two_Items_Land_On_Fall_And_Winter()
    {
        var model = Model(
            ("(O)a", Season.Spring, 3),
            ("(O)b", Season.Spring, 4));

        var result = BundleDeadlines.For(new List<string> { "(O)a", "(O)b" }, model);

        Assert.Equal(Season.Fall, result["(O)a"]);
        Assert.Equal(Season.Winter, result["(O)b"]);
    }

    [Fact]
    public void Three_Items_Land_On_Summer_Fall_And_Winter()
    {
        var model = Model(
            ("(O)a", Season.Spring, 3),
            ("(O)b", Season.Spring, 4),
            ("(O)c", Season.Spring, 5));

        var result = BundleDeadlines.For(new List<string> { "(O)a", "(O)b", "(O)c" }, model);

        Assert.Equal(Season.Summer, result["(O)a"]);
        Assert.Equal(Season.Fall, result["(O)b"]);
        Assert.Equal(Season.Winter, result["(O)c"]);
    }

    [Fact]
    public void One_Item_Is_Due_At_Winter()
    {
        var model = Model(("(O)a", Season.Spring, 5));

        var result = BundleDeadlines.For(new List<string> { "(O)a" }, model);

        Assert.Equal(Season.Winter, result["(O)a"]);
    }

    [Fact]
    public void Six_Items_Spread_Proportionally_Across_The_Four_Checkpoints()
    {
        var model = Model(
            ("(O)a", Season.Spring, 1), ("(O)b", Season.Spring, 2), ("(O)c", Season.Spring, 3),
            ("(O)d", Season.Spring, 4), ("(O)e", Season.Spring, 5), ("(O)f", Season.Spring, 6));

        var result = BundleDeadlines.For(
            new List<string> { "(O)a", "(O)b", "(O)c", "(O)d", "(O)e", "(O)f" }, model);

        Assert.Equal(Season.Spring, result["(O)a"]);
        Assert.Equal(Season.Spring, result["(O)b"]);
        Assert.Equal(Season.Summer, result["(O)c"]);
        Assert.Equal(Season.Fall, result["(O)d"]);
        Assert.Equal(Season.Fall, result["(O)e"]);
        Assert.Equal(Season.Winter, result["(O)f"]);
    }

    /// <summary>The load-bearing safety property: a deadline may never precede the season in
    /// which the item can first exist. This is the invariant whose absence made a Fall Foraging
    /// bundle unsatisfiable at its own gate.</summary>
    [Fact]
    public void A_Deadline_Never_Precedes_The_Items_Floor()
    {
        var model = Model(
            ("(O)a", Season.Winter, 1),
            ("(O)b", Season.Spring, 2),
            ("(O)c", Season.Spring, 3),
            ("(O)d", Season.Spring, 4));

        var result = BundleDeadlines.For(
            new List<string> { "(O)a", "(O)b", "(O)c", "(O)d" }, model);

        Assert.Equal(Season.Winter, result["(O)a"]);
    }

    [Fact]
    public void A_High_Effort_Item_Slides_One_Checkpoint_Later()
    {
        var model = Model(
            ("(O)a", Season.Spring, 0),
            ("(O)b", Season.Spring, BundleDeadlines.HighEffortThreshold));

        var result = BundleDeadlines.For(new List<string> { "(O)a", "(O)b" }, model);

        // Base spread for two items is Fall then Winter; the easy one slides earlier and the
        // hard one is already at the last checkpoint.
        Assert.Equal(Season.Summer, result["(O)a"]);
        Assert.Equal(Season.Winter, result["(O)b"]);
    }

    [Fact]
    public void Every_Ingredient_Is_Due_By_Winter_At_The_Latest()
    {
        var model = Model(
            ("(O)a", Season.Winter, 99),
            ("(O)b", Season.Winter, 99));

        var result = BundleDeadlines.For(new List<string> { "(O)a", "(O)b" }, model);

        Assert.All(result.Values, s => Assert.True(s <= Season.Winter));
    }

    [Fact]
    public void The_Result_Is_Deterministic_Regardless_Of_Input_Order()
    {
        var model = Model(
            ("(O)a", Season.Spring, 5),
            ("(O)b", Season.Spring, 5),
            ("(O)c", Season.Spring, 5),
            ("(O)d", Season.Spring, 5));

        var forward = BundleDeadlines.For(
            new List<string> { "(O)a", "(O)b", "(O)c", "(O)d" }, model);
        var reversed = BundleDeadlines.For(
            new List<string> { "(O)d", "(O)c", "(O)b", "(O)a" }, model);

        Assert.Equal(forward["(O)a"], reversed["(O)a"]);
        Assert.Equal(forward["(O)d"], reversed["(O)d"]);
    }

    [Fact]
    public void Every_Ingredient_Gets_A_Deadline()
    {
        var model = Model(("(O)a", Season.Spring, 1));

        var result = BundleDeadlines.For(new List<string> { "(O)a", "(O)unknown" }, model);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void An_Empty_Ingredient_List_Returns_An_Empty_Map()
        => Assert.Empty(BundleDeadlines.For(new List<string>(), Model()));
}
