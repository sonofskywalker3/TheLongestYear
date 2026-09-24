using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class BundleDeadlinesTests
{
    [Fact]
    public void Deadline_clamps_to_the_gate_season_not_the_goal_week()
    {
        // Ruby: goal week 3 (Spring), gate Summer. Easiest of four, so the spread puts it on
        // Spring; the clamp lifts it to its gate season, not to its goal week's season.
        var model = new ItemAvailabilityModel(new Dictionary<string, ItemAvailability>
        {
            ["(O)64"] = new ItemAvailability(Season.Spring, 1, "ruby", EffortSource.Derived, 3, Season.Summer),
            ["(O)b"] = new ItemAvailability(Season.Spring, 2, "test"),
            ["(O)c"] = new ItemAvailability(Season.Spring, 3, "test"),
            ["(O)d"] = new ItemAvailability(Season.Spring, 4, "test"),
        });
        var result = BundleDeadlines.For(new List<string> { "(O)64", "(O)b", "(O)c", "(O)d" }, model);
        Assert.Equal(Season.Summer, result["(O)64"]);
    }

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

        // Both floor at Winter, so both clamp up to Winter. Asserted concretely: "s <= Winter"
        // cannot fail, Winter being the top of the enum.
        Assert.Equal(Season.Winter, result["(O)a"]);
        Assert.Equal(Season.Winter, result["(O)b"]);
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

        // Pinned to the concrete map, not just to agreement between the two runs: equal effort
        // means the ordinal id tiebreak decides, so all four ids and all four seasons are stated.
        // Comparing two runs alone would pass for a rule that returned Winter for everything.
        foreach (var actual in new[] { forward, reversed })
        {
            Assert.Equal(4, actual.Count);
            Assert.Equal(Season.Spring, actual["(O)a"]);
            Assert.Equal(Season.Summer, actual["(O)b"]);
            Assert.Equal(Season.Fall, actual["(O)c"]);
            Assert.Equal(Season.Winter, actual["(O)d"]);
        }
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

/// <summary>Spec 2026-08-28-obtainable-board-2-stretch: a stretch line pins an ingredient to its
/// own season instead of clamping it up to the gate.</summary>
public class BundleDeadlinesStretchTests
{
    private static ItemAvailabilityModel ModelWith(params (string Id, int Week, int Hard)[] items)
        => new(items.ToDictionary(
            i => i.Id,
            i => new ItemAvailability(
                AvailabilityWeeks.SeasonOf(i.Week), 3, "test", EffortSource.Derived,
                i.Week, AvailabilityWeeks.SeasonOf(i.Week), i.Hard),
            System.StringComparer.Ordinal));

    [Fact]
    public void A_stretch_line_pins_a_per_item_ingredient_to_its_stretch_season()
    {
        var model = ModelWith(("(O)a", 6, 1), ("(O)b", 6, 5), ("(O)c", 13, 13));
        var lines = new Dictionary<string, Season> { ["(O)a"] = Season.Spring };
        IReadOnlyDictionary<string, Season> pins = BundleDeadlines.For(new[] { "(O)a", "(O)b", "(O)c" }, model, lines);
        Assert.Equal(Season.Spring, pins["(O)a"]);
        Assert.True(pins["(O)b"] >= Season.Summer);
    }
}
