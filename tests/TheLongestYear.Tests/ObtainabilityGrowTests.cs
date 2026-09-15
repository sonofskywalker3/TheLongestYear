using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;
using TheLongestYear.Core.Obtainability;

namespace TheLongestYear.Tests;

public class ObtainabilityGrowTests
{
    private static readonly Dictionary<string, FestivalDates> NoFestivals = new();

    private static ObtainabilityModel Snapshot(params (string Id, SourceKind Kind, WeekMask Weeks, Reliability R)[] rows)
        => new(rows.GroupBy(r => r.Id).ToDictionary(
            g => g.Key,
            g => (IReadOnlyList<ObtainSource>)g.Select(r => new ObtainSource(r.Kind, DayTable.InWeeks(r.Weeks), r.R, ObtainConditions.None, "test")).ToList()));

    [Fact]
    public void A_four_day_spring_crop_harvests_all_spring_but_not_past_it()
        => Assert.Equal("1-4", GrowSources.Harvest(WeekMask.ForSeason(Season.Spring), new[] { Season.Spring }, 4, 0).ToString());

    [Fact]
    public void A_regrowing_two_season_crop_keeps_yielding_on_its_interval()
    {
        var both = new[] { Season.Summer, Season.Fall };
        Assert.Equal("7-12", GrowSources.Harvest(WeekMask.ForSeasons(both), both, 14, 4).ToString());
        // Seed only in the first week of summer, regrowing every 21 days: harvests on days 43, 64 and 85 would
        // be weeks 7 and 10; day 85 is Winter, so it stops.
        Assert.Equal("7,10", GrowSources.Harvest(WeekMask.Of(5), both, 14, 21).ToString());
    }

    [Fact]
    public void The_greenhouse_needs_seed_on_the_planting_day_and_counts_growth_by_days()
    {
        Assert.Equal("1-5", GrowSources.Greenhouse(WeekMask.ForSeason(Season.Spring), 4, 0).ToString());
        Assert.Equal("1-16", GrowSources.Greenhouse(WeekMask.ForSeason(Season.Spring), 4, 1).ToString());
    }

    [Fact(Skip = "phase 2 task 4/5 rewrites this to the start-day meaning")]
    public void Crops_split_dependable_seed_weeks_from_chance_ones()
    {
        var snapshot = Snapshot(
            ("(O)472", SourceKind.Shop, WeekMask.ForSeason(Season.Spring), Reliability.Dependable),
            ("(O)472", SourceKind.Cart, WeekMask.All, Reliability.Chance));
        var rows = new[] { new CropRow("(O)472", "(O)24", new[] { Season.Spring }, 4, 0) };
        var sources = GrowSources.Crops(rows, snapshot).Where(s => s.ItemId == "(O)24").Select(s => s.Source).ToList();
        var outdoor = sources.Where(s => s.Kind == SourceKind.Crop).ToList();
        Assert.Single(outdoor);
        Assert.Equal(Reliability.Dependable, outdoor[0].Reliability);
        var greenhouse = sources.Where(s => s.Kind == SourceKind.GreenhouseCrop).ToList();
        Assert.Contains(greenhouse, s => s.Reliability == Reliability.Dependable && s.Lands.ToString() == "1-5");
        Assert.Contains(greenhouse, s => s.Reliability == Reliability.Chance && s.Lands.LandingWeek(1) == 14);
        Assert.All(greenhouse, s => Assert.Contains("mail:ccPantry", s.Conditions.Requires));
    }

    [Fact(Skip = "phase 2 task 4/5 rewrites this to the start-day meaning")]
    public void Mixed_seeds_give_the_planting_days_pool_and_winter_greenhouse_gives_every_pool()
    {
        var snapshot = Snapshot(("(O)770", SourceKind.Forage, WeekMask.All, Reliability.Chance));
        var rows = new[]
        {
            new CropRow("(O)472", "(O)24", new[] { Season.Spring }, 4, 0),
            new CropRow("(O)487", "(O)270", new[] { Season.Summer, Season.Fall }, 14, 4),
            new CropRow("(O)770", "(O)770", new Season[0], 1, 0),
        };
        var parsnip = GrowSources.Crops(rows, snapshot).Where(s => s.ItemId == "(O)24").Select(s => s.Source).ToList();
        Assert.Contains(parsnip, s => s.Kind == SourceKind.Crop && s.Detail.Contains("Mixed Seeds") && s.Lands.ToString() == "1-4");
        var indoor = parsnip.Single(s => s.Kind == SourceKind.GreenhouseCrop && s.Detail.Contains("Mixed Seeds"));
        Assert.Equal("1-5,13-16", indoor.Lands.ToString());     // spring pool in spring, every pool in winter, never summer or fall
        Assert.DoesNotContain(GrowSources.Crops(rows, snapshot), s => s.ItemId == "(O)770");
    }

    [Fact(Skip = "phase 2 task 4/5 rewrites this to the start-day meaning")]
    public void A_fruit_tree_fruits_from_maturity_in_its_season_and_a_spring_sapling_misses_spring()
    {
        var snapshot = Snapshot(
            ("(O)633", SourceKind.Shop, WeekMask.All, Reliability.Dependable),                         // apple sapling
            ("(O)628", SourceKind.Shop, WeekMask.ForSeason(Season.Spring), Reliability.Dependable),     // cherry sapling
            ("(O)69", SourceKind.Shop, WeekMask.All, Reliability.Dependable));                         // banana sapling
        var rows = new[]
        {
            new FruitTreeRow("(O)633", new[] { Season.Fall }, new[] { new FruitRow("(O)613", null, 1.0, null) }),
            new FruitTreeRow("(O)628", new[] { Season.Spring }, new[] { new FruitRow("(O)638", null, 1.0, null) }),
            new FruitTreeRow("(O)69", new[] { Season.Summer }, new[] { new FruitRow("(O)91", null, 0.5, "YEAR 2") }),
        };
        var all = GrowSources.FruitTrees(rows, snapshot, new Dictionary<string, ObjInfo>(), NoFestivals).ToList();
        Assert.Equal("9-12", all.Single(s => s.ItemId == "(O)613" && s.Source.Kind == SourceKind.FruitTree).Source.Lands.ToString());
        Assert.DoesNotContain(all, s => s.ItemId == "(O)638" && s.Source.Kind == SourceKind.FruitTree);
        Assert.Contains(all, s => s.ItemId == "(O)638" && s.Source.Kind == SourceKind.GreenhouseCrop);
        var banana = all.First(s => s.ItemId == "(O)91").Source;
        Assert.Equal(Reliability.Chance, banana.Reliability);
        Assert.True(banana.Conditions.YearTwo);
    }
}
