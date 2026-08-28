using System.Collections.Generic;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class ItemAvailabilityModelWeekTests
{
    private static readonly Dictionary<string, ItemAvailability> NoDerived = new();

    [Fact]
    public void Effort_only_week_becomes_the_floor_and_the_gate()
    {
        var model = new ItemAvailabilityModel(NoDerived,
            effortDerived: new Dictionary<string, ItemEffort> { ["(O)66"] = new(1, "amethyst", EarliestWeek: 1) });
        ItemAvailability a = model.For("(O)66");
        Assert.Equal(1, a.Week);
        Assert.Equal(Season.Spring, a.EarliestSeason);
        Assert.Equal(Season.Spring, a.Gate);
        Assert.True(model.IsPlaced("(O)66"));
    }

    [Fact]
    public void Effort_only_gate_can_be_later_than_its_week()
    {
        var model = new ItemAvailabilityModel(NoDerived,
            effortDerived: new Dictionary<string, ItemEffort> { ["(O)64"] = new(5, "ruby", EarliestWeek: 3, GateSeason: Season.Summer) });
        ItemAvailability a = model.For("(O)64");
        Assert.Equal(3, a.Week);
        Assert.Equal(Season.Summer, a.Gate);
    }

    [Fact]
    public void Effort_without_a_week_is_unknown()
    {
        var model = new ItemAvailabilityModel(NoDerived,
            effortDerived: new Dictionary<string, ItemEffort> { ["(O)1"] = new(4, "no week") });
        ItemAvailability a = model.For("(O)1");
        Assert.Equal(AvailabilityWeeks.UnknownWeek, a.Week);
        Assert.Equal(Season.Winter, a.Gate);
        Assert.False(model.IsPlaced("(O)1"));
        Assert.Contains("(O)1", model.UnknownIds);
    }

    [Fact]
    public void Week_override_moves_a_floor_later_and_sets_the_gate()
    {
        var model = new ItemAvailabilityModel(NoDerived,
            effortDerived: new Dictionary<string, ItemEffort> { ["(O)66"] = new(1, "amethyst", EarliestWeek: 1) },
            weekOverrides: new Dictionary<string, int> { ["(O)66"] = 6 });
        ItemAvailability a = model.For("(O)66");
        Assert.Equal(6, a.Week);
        Assert.Equal(Season.Summer, a.Gate);
        Assert.Contains("override", a.Basis);
    }

    [Fact]
    public void Week_override_earlier_than_a_phase_1_floor_is_rejected()
    {
        var model = new ItemAvailabilityModel(
            new Dictionary<string, ItemAvailability> { ["(O)384"] = new(Season.Spring, 5, "gold", EffortSource.Derived, 3, Season.Summer) },
            weekOverrides: new Dictionary<string, int> { ["(O)384"] = 1 });
        Assert.Equal(3, model.For("(O)384").Week);
        Assert.Contains("(O)384", model.RejectedSeasonOverrides);
    }

    [Fact]
    public void A_pin_may_move_a_rule_week_earlier_because_rule_weeks_are_judgements()
    {
        // Wood: the Recycling Machine rule says week 5; the pin says Spring. The pin wins.
        var model = new ItemAvailabilityModel(NoDerived,
            effortDerived: new Dictionary<string, ItemEffort> { ["(O)388"] = new(4, "recycler", EarliestWeek: 5) },
            seasonOverrides: new Dictionary<string, Season> { ["(O)388"] = Season.Spring });
        Assert.Equal(1, model.For("(O)388").Week);
        Assert.DoesNotContain("(O)388", model.RejectedSeasonOverrides);
    }

    [Fact]
    public void Season_pin_on_an_unknown_item_places_it_at_the_seasons_first_week()
    {
        var model = new ItemAvailabilityModel(NoDerived,
            seasonOverrides: new Dictionary<string, Season> { ["(O)388"] = Season.Spring });
        ItemAvailability a = model.For("(O)388");
        Assert.Equal(1, a.Week);
        Assert.True(model.IsPlaced("(O)388"));
        Assert.DoesNotContain("(O)388", model.UnknownIds);
    }
}
