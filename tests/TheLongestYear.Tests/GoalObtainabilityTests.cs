using System.Collections.Generic;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>Bundle-loop audit 2026-08-29: Master Fisher's offered Scorpion Carp as a Summer
/// Fishing goal. The desert pond lists it in every season, but the bus is not a Summer thing on
/// this mod's start, and the availability model already says so (LocationGating: Desert = Fall).
/// Weekly goals now consult that floor as well as the catalog's season set.</summary>
public class GoalObtainabilityTests
{
    [Fact]
    public void A_week_3_item_is_not_a_week_1_goal_but_is_a_week_3_goal()
    {
        var model = new ItemAvailabilityModel(new Dictionary<string, ItemAvailability>(),
            effortDerived: new Dictionary<string, ItemEffort> { ["(O)64"] = new(5, "ruby", EarliestWeek: 3, GateSeason: Season.Summer) });
        Assert.False(GoalObtainability.IsObtainable(null, model, "(O)64", 1));
        Assert.True(GoalObtainability.IsObtainable(null, model, "(O)64", 3));
    }

    [Fact]
    public void An_unknown_item_is_still_a_goal_when_the_catalog_allows_it()
    {
        var model = new ItemAvailabilityModel(new Dictionary<string, ItemAvailability>());
        Assert.True(GoalObtainability.IsObtainable(null, model, "(O)24", 1));
    }

    private static readonly IReadOnlySet<Season> AllSeasons =
        new HashSet<Season> { Season.Spring, Season.Summer, Season.Fall, Season.Winter };

    private static ItemAvailabilityModel Model() => new(new Dictionary<string, ItemAvailability>
    {
        ["(O)165"] = new ItemAvailability(Season.Fall, 9, "fish, gated by location (Desert)"),
        ["(O)145"] = new ItemAvailability(Season.Spring, 2, "fish, earliest Spring"),
    });

    [Fact]
    public void Derived_location_floor_blocks_the_goal_before_its_season()
    {
        Assert.False(GoalObtainability.IsObtainable(AllSeasons, Model(), "(O)165", Season.Summer));
        Assert.True(GoalObtainability.IsObtainable(AllSeasons, Model(), "(O)165", Season.Fall));
    }

    [Fact]
    public void Catalog_seasons_still_apply()
    {
        var summerOnly = new HashSet<Season> { Season.Summer };
        Assert.False(GoalObtainability.IsObtainable(summerOnly, Model(), "(O)145", Season.Winter));
        Assert.True(GoalObtainability.IsObtainable(summerOnly, Model(), "(O)145", Season.Summer));
    }

    [Fact]
    public void Unknown_items_are_not_floored_at_winter()
    {
        // The model floors an unrecognised id at Winter for GATES (the safe direction there); a
        // goal must not inherit that, or no crop or forage could ever be a goal before Winter.
        Assert.True(GoalObtainability.IsObtainable(AllSeasons, Model(), "(O)24", Season.Spring));
        Assert.True(GoalObtainability.IsObtainable(null, null, "(O)24", Season.Spring));
    }
}
