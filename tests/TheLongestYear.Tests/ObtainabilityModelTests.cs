using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;
using TheLongestYear.Core.Obtainability;

namespace TheLongestYear.Tests;

public class ObtainabilityModelTests
{
    private static ObtainSource Src(SourceKind kind, DayTable lands, Reliability r, ObtainConditions? c = null, string detail = "test")
        => new(kind, lands, r, c ?? ObtainConditions.None, detail);

    private static readonly ObtainabilityModel Model = new(new Dictionary<string, IReadOnlyList<ObtainSource>>
    {
        ["(O)147"] = new[]
        {
            Src(SourceKind.Fish, DayTable.InWeeks(WeekMask.ForSeason(Season.Spring) | WeekMask.ForSeason(Season.Winter)), Reliability.Dependable),
            Src(SourceKind.Cart, DayTable.Always, Reliability.Chance),
        },
        ["(O)91"] = new[] { Src(SourceKind.FruitTree, DayTable.Always, Reliability.Dependable, ObtainConditions.None with { YearTwo = true }) },
        ["(O)829"] = new[] { Src(SourceKind.Forage, DayTable.Always, Reliability.Dependable, ObtainConditions.None with { GingerIsland = true }) },
        ["(O)999"] = new[] { Src(SourceKind.Other, DayTable.Always, Reliability.Chance, ObtainConditions.None with { Unresolved = true }) },
    });

    [Fact]
    public void Ids_are_normalized_on_the_way_in_and_out()
    {
        var model = new ObtainabilityModel(new Dictionary<string, IReadOnlyList<ObtainSource>> { ["24"] = new[] { Src(SourceKind.Crop, DayTable.Always, Reliability.Dependable) } });
        Assert.Single(model.Sources("(O)24"));
        Assert.Single(model.Sources("24"));
        Assert.Contains("(O)24", model.ItemIds);
    }

    [Fact]
    public void Dependable_only_waits_for_the_season_while_any_source_lands_at_once()
    {
        Assert.Equal(1, Model.Lands("(O)147", 1, ObtainFilter.DependableOnly));
        Assert.Equal(85, Model.Lands("(O)147", 29, ObtainFilter.DependableOnly));   // Summer 1: wait for Winter
        Assert.Equal(29, Model.Lands("(O)147", 29, ObtainFilter.Any));
        Assert.Equal(13, Model.Table("(O)147", ObtainFilter.DependableOnly).LandingWeek(29));
        Assert.Equal(1, Model.LandingWeekFromDay1("(O)147", ObtainFilter.DependableOnly));
    }

    [Fact]
    public void CanObtain_applies_the_callers_deadline()
    {
        Assert.False(Model.CanObtain("(O)147", 29, 84, ObtainFilter.DependableOnly));
        Assert.True(Model.CanObtain("(O)147", 29, 85, ObtainFilter.DependableOnly));
        Assert.True(Model.CanObtain("(O)147", 29, 29, ObtainFilter.Any));
        Assert.False(Model.CanObtain("(O)nothing", 1, 112, ObtainFilter.Any));
    }

    [Fact]
    public void Year_two_and_island_and_unresolved_sources_count_only_when_asked()
    {
        Assert.Null(Model.Lands("(O)91", 1, ObtainFilter.Any));
        Assert.Equal(1, Model.Lands("(O)91", 1, ObtainFilter.Any with { IncludeYearTwo = true }));
        Assert.Null(Model.Lands("(O)829", 1, ObtainFilter.Any));
        Assert.Equal(1, Model.Lands("(O)829", 1, ObtainFilter.Any with { IncludeGingerIsland = true }));
        Assert.Equal(1, Model.Lands("(O)999", 1, ObtainFilter.Any));
        Assert.Null(Model.Lands("(O)999", 1, ObtainFilter.Any with { IncludeUnresolved = false }));
    }

    [Fact]
    public void Kind_filters_pick_sources()
    {
        var cartOnly = ObtainFilter.Any with { Kinds = new[] { SourceKind.Cart } };
        Assert.Equal(1, Model.Lands("(O)147", 1, cartOnly));
        Assert.Null(Model.Lands("(O)147", 1, cartOnly with { Reliabilities = new[] { Reliability.Dependable } }));
    }

    [Fact]
    public void Identical_sources_collapse_and_setup_is_part_of_identity()
    {
        var a = Src(SourceKind.Animal, DayTable.Always, Reliability.Dependable) with { Setup = new[] { new SetupStep("building:Coop", 3) } };
        var b = Src(SourceKind.Animal, DayTable.Always, Reliability.Dependable) with { Setup = new[] { new SetupStep("building:Coop", 3) } };
        var c = Src(SourceKind.Animal, DayTable.Always, Reliability.Dependable);
        var model = new ObtainabilityModel(new Dictionary<string, IReadOnlyList<ObtainSource>> { ["(O)176"] = new[] { a, b, c } });
        Assert.Equal(2, model.Sources("(O)176").Count);
    }

    [Fact]
    public void SourcePair_splits_luck_only_starts_from_dependable_ones()
    {
        DayTable dep = DayTable.InWeeks(WeekMask.ForSeason(Season.Summer));
        var pair = SourcePair.Of(SourceKind.Crop, dep, DayTable.Always, ObtainConditions.None, "x").ToList();
        Assert.Equal(2, pair.Count);
        Assert.Equal(Reliability.Dependable, pair[0].Reliability);
        Assert.Equal(29, pair[0].Lands.Lands(1));
        Assert.Equal(Reliability.Chance, pair[1].Reliability);
        Assert.Equal(1, pair[1].Lands.Lands(1));
        Assert.Null(pair[1].Lands.Lands(29));   // in Summer luck adds nothing
        Assert.Single(SourcePair.Of(SourceKind.Crop, dep, dep, ObtainConditions.None, "x"));
        Assert.Empty(SourcePair.Of(SourceKind.Crop, DayTable.None, DayTable.None, ObtainConditions.None, "x"));
    }
}
