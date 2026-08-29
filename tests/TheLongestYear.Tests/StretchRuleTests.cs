using System.Collections.Generic;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class StretchRuleTests
{
    private static ItemAvailability Item(int week, int hard) =>
        new(AvailabilityWeeks.SeasonOf(week), 3, "test", EffortSource.Derived, week, AvailabilityWeeks.SeasonOf(week), hard);

    private static ItemAvailabilityModel Model(DifficultyStep step, params (string Id, int Week, int Hard)[] items)
    {
        var derived = new Dictionary<string, ItemAvailability>();
        foreach ((string id, int week, int hard) in items) derived[id] = Item(week, hard);
        // Mode derived from the step exactly as ModEntry.BuildAvailabilityModelFor does: a model
        // whose mode did not match its step is not a model the mod can ever build.
        return new ItemAvailabilityModel(derived, mode: WeekModes.For(step), step: step);
    }

    [Fact]
    public void A_summer_week_6_item_with_a_spring_hard_week_is_a_spring_stretch()
    {
        Assert.True(StretchRule.IsStretchFor(Item(6, 1), Season.Spring));
        Assert.False(StretchRule.IsStretchFor(Item(7, 1), Season.Spring));    // past the window
        Assert.False(StretchRule.IsStretchFor(Item(6, 5), Season.Spring));    // hard week is Summer: a real fact
        Assert.False(StretchRule.IsStretchFor(Item(3, 1), Season.Spring));    // already reachable
        Assert.False(StretchRule.IsStretchFor(Item(14, 13), Season.Winter));  // Winter never stretches
    }

    [Fact]
    public void A_bundle_with_nothing_new_in_spring_gets_one_stretch_line()
    {
        ItemAvailabilityModel model = Model(DifficultyStep.Normal, ("(O)b", 6, 1), ("(O)a", 5, 2), ("(O)c", 9, 9), ("(O)d", 13, 13));
        IReadOnlyDictionary<string, Season> lines = StretchRule.Lines(new[] { "(O)b", "(O)a", "(O)c", "(O)d" }, model);
        Assert.Equal(Season.Spring, lines["(O)a"]);   // ordinal first of the two candidates
        Assert.False(lines.ContainsKey("(O)b"));
        // Summer gains (O)a and (O)b (reachable by week 8, not by week 4): no Summer stretch.
        // Fall gains (O)c: no Fall stretch.
        Assert.Single(lines);
    }

    [Fact]
    public void A_bundle_that_gains_something_every_season_has_no_stretch_lines()
    {
        ItemAvailabilityModel model = Model(DifficultyStep.Normal, ("(O)a", 1, 1), ("(O)b", 5, 5), ("(O)c", 9, 9), ("(O)d", 13, 13));
        Assert.Empty(StretchRule.Lines(new[] { "(O)a", "(O)b", "(O)c", "(O)d" }, model));
    }

    [Fact]
    public void Easy_never_stretches()
    {
        ItemAvailabilityModel model = Model(DifficultyStep.Easy, ("(O)a", 5, 2), ("(O)d", 13, 13));
        Assert.Empty(StretchRule.Lines(new[] { "(O)a", "(O)d" }, model));
        Assert.False(StretchRule.Applies(model));
        Assert.False(StretchRule.Applies(DifficultyStep.Easy, WeekMode.Pacing));
    }

    /// <summary>The stretch rule is a PACING-MODE mechanism. Easy is out because the rule says so;
    /// Hard and Extreme are out because their gates already read the hard week (WeekModes.For maps
    /// them to HardGates / HardAll), so there is nothing left to stretch towards. Only Normal
    /// stretches. Same ingredients every time: only the step, and the mode it implies, changes.</summary>
    [Theory]
    [InlineData(DifficultyStep.Easy, false)]
    [InlineData(DifficultyStep.Normal, true)]
    [InlineData(DifficultyStep.Hard, false)]
    [InlineData(DifficultyStep.Extreme, false)]
    public void Only_normal_stretches(DifficultyStep step, bool expectLines)
    {
        ItemAvailabilityModel model = Model(step, ("(O)b", 6, 1), ("(O)a", 5, 2), ("(O)c", 9, 9), ("(O)d", 13, 13));
        IReadOnlyDictionary<string, Season> lines = StretchRule.Lines(new[] { "(O)b", "(O)a", "(O)c", "(O)d" }, model);
        Assert.Equal(expectLines, lines.Count > 0);
        Assert.Equal(expectLines, StretchRule.Applies(model));
    }

    [Fact]
    public void Stretch_seasons_are_the_three_non_winter_seasons()
        => Assert.Equal(new[] { Season.Spring, Season.Summer, Season.Fall }, StretchRule.StretchSeasons);
}
