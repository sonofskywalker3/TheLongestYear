using System.Collections.Generic;
using TheLongestYear.Core;
using TheLongestYear.Core.Obtainability;

namespace TheLongestYear.Tests;

public class ObtainabilityModelTests
{
    [Fact]
    public void A_season_is_its_four_weeks()
    {
        WeekMask winter = WeekMask.ForSeason(Season.Winter);
        Assert.False(winter.Contains(12));
        Assert.True(winter.Contains(13));
        Assert.True(winter.Contains(16));
        Assert.Equal(13, winter.Earliest);
    }

    [Fact]
    public void Days_map_to_weeks_across_the_year()
    {
        Assert.Equal(1, WeekMask.WeekOfDay(1));
        Assert.Equal(1, WeekMask.WeekOfDay(7));
        Assert.Equal(2, WeekMask.WeekOfDay(8));
        Assert.Equal(15, WeekMask.WeekOfDay(84 + 15)); // Winter 15
        Assert.Equal(16, WeekMask.WeekOfDay(112));
        Assert.Equal(WeekMask.Of(15), WeekMask.ForDays(84 + 15, 84 + 17)); // Night Market
    }

    [Fact]
    public void A_spring_and_winter_fish_is_absent_in_summer()
    {
        WeekMask m = WeekMask.ForSeasons(new[] { Season.Spring, Season.Winter });
        Assert.True(m.Contains(2));
        Assert.False(m.Contains(6));
        Assert.True(m.Contains(14));
        Assert.Equal("1-4,13-16", m.ToString());
    }

    [Fact]
    public void Shifting_later_drops_weeks_past_the_year()
    {
        Assert.Equal(WeekMask.Range(2, 16), WeekMask.All.ShiftLater(1));
        Assert.Equal(WeekMask.Range(15, 16), WeekMask.FromWeekOnwardOf(13).ShiftLater(2));
        Assert.Equal(WeekMask.Range(5, 16), WeekMask.Of(5).FromWeekOnward());
        Assert.Equal(WeekMask.Range(1, 12), WeekMask.All.Except(WeekMask.ForSeason(Season.Winter)));
    }

    [Fact]
    public void Conditions_compare_by_content_not_by_list_reference()
    {
        var a = ObtainConditions.None with { Requires = new[] { "shop:SeedShop" } };
        var b = ObtainConditions.None with { Requires = new List<string> { "shop:SeedShop" } };
        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
        Assert.NotEqual(a, a with { Requires = new[] { "shop:Sandy" } });
    }

    [Fact]
    public void Filters_pick_reliability_and_skip_year_two_and_the_island_by_default()
    {
        var sources = new Dictionary<string, IReadOnlyList<ObtainSource>>
        {
            ["(O)24"] = new[]
            {
                new ObtainSource(SourceKind.Shop, WeekMask.ForSeason(Season.Spring), Reliability.Dependable, ObtainConditions.None, "SeedShop"),
                new ObtainSource(SourceKind.Cart, WeekMask.All, Reliability.Chance, ObtainConditions.None, "Traveler"),
                new ObtainSource(SourceKind.Shop, WeekMask.All, Reliability.Dependable, ObtainConditions.None with { YearTwo = true }, "year 2 row"),
                new ObtainSource(SourceKind.Forage, WeekMask.All, Reliability.Dependable, ObtainConditions.None with { GingerIsland = true }, "island"),
            },
        };
        var model = new ObtainabilityModel(sources);

        Assert.Equal(WeekMask.ForSeason(Season.Spring), model.Weeks("24", ObtainFilter.DependableOnly));
        Assert.Equal(WeekMask.All, model.Weeks("(O)24", ObtainFilter.Any));
        Assert.True(model.IsObtainable("(O)24", 14, ObtainFilter.Any));
        Assert.False(model.IsObtainable("(O)24", 14, ObtainFilter.DependableOnly));
        Assert.Equal(1, model.EarliestWeek("(O)24", ObtainFilter.DependableOnly));
        Assert.Null(model.EarliestWeek("(O)999", ObtainFilter.Any));
        Assert.Empty(model.Sources("(O)999"));
        var cartOnly = new ObtainFilter { Kinds = new[] { SourceKind.Cart } };
        Assert.Equal(WeekMask.All, model.Weeks("(O)24", cartOnly));
    }

    [Fact]
    public void Unresolved_sources_count_by_default_and_can_be_excluded()
    {
        var sources = new Dictionary<string, IReadOnlyList<ObtainSource>>
        {
            ["(O)1"] = new[] { new ObtainSource(SourceKind.Other, WeekMask.All, Reliability.Chance,
                ObtainConditions.None with { Unresolved = true }, "machine output method") },
        };
        var model = new ObtainabilityModel(sources);
        Assert.Equal(WeekMask.All, model.Weeks("(O)1", ObtainFilter.Any));
        Assert.True(model.Weeks("(O)1", ObtainFilter.Any with { IncludeUnresolved = false }).IsEmpty);
    }

    [Fact]
    public void Keys_that_normalize_to_the_same_id_are_merged_not_overwritten()
    {
        var shop = new ObtainSource(SourceKind.Shop, WeekMask.Of(1), Reliability.Dependable, ObtainConditions.None, "a");
        var forage = new ObtainSource(SourceKind.Forage, WeekMask.Of(9), Reliability.Dependable, ObtainConditions.None, "b");
        var model = new ObtainabilityModel(new Dictionary<string, IReadOnlyList<ObtainSource>>
        {
            ["24"] = new[] { shop },
            ["(O)24"] = new[] { forage, shop },
        });
        Assert.Equal(2, model.Sources("(O)24").Count);
        Assert.Equal("1,9", model.Weeks("(O)24", ObtainFilter.Any).ToString());
    }
}
