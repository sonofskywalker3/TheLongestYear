using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;
using TheLongestYear.Core.Obtainability;

namespace TheLongestYear.Tests;

public class ObtainabilityGrowTests
{
    private static readonly Dictionary<string, FestivalDates> NoFestivals = new();

    private static ObtainabilityModel Snapshot(params (string Id, SourceKind Kind, DayTable Lands, Reliability R)[] rows)
        => new(rows.GroupBy(r => r.Id).ToDictionary(
            g => g.Key,
            g => (IReadOnlyList<ObtainSource>)g.Select(r => new ObtainSource(r.Kind, r.Lands, r.R, ObtainConditions.None, "test")).ToList()));

    private static DayTable Spring => DayTable.InWeeks(WeekMask.ForSeason(Season.Spring));

    [Fact]
    public void A_spring_crop_planted_late_in_spring_never_lands_and_the_greenhouse_needs_the_seed_first()
    {
        Assert.Equal(5, GrowSources.PlantTable(new[] { Season.Spring }, 4).Lands(1));
        Assert.Equal(28, GrowSources.PlantTable(new[] { Season.Spring }, 4).Lands(24));
        Assert.Null(GrowSources.PlantTable(new[] { Season.Spring }, 4).Lands(25));
        Assert.Equal(29, GrowSources.GreenhouseTable(4).Lands(25));
        Assert.Equal(112, GrowSources.GreenhouseTable(4).Lands(108));
        Assert.Null(GrowSources.GreenhouseTable(4).Lands(109));
    }

    [Fact]
    public void A_two_season_crop_grows_across_the_season_line()
    {
        DayTable t = GrowSources.PlantTable(new[] { Season.Summer, Season.Fall }, 14);
        Assert.Equal(43, t.Lands(1));     // wait for Summer 1, plus 14
        Assert.Equal(84, t.Lands(70));    // Fall 14 + 14 = Fall 28
        Assert.Null(t.Lands(71));
    }

    [Fact]
    public void Crops_chain_from_the_seeds_own_table_and_split_reliability()
    {
        var snapshot = Snapshot(
            ("(O)472", SourceKind.Shop, Spring, Reliability.Dependable),
            ("(O)472", SourceKind.Cart, DayTable.Always, Reliability.Chance));
        var rows = new[] { new CropRow("(O)472", "(O)24", new[] { Season.Spring }, 4, 0) };
        var sources = GrowSources.Crops(rows, snapshot).Where(s => s.ItemId == "(O)24").Select(s => s.Source).ToList();
        var outdoor = sources.Single(s => s.Kind == SourceKind.Crop && s.Reliability == Reliability.Dependable);
        Assert.Equal(5, outdoor.Lands.Lands(1));
        Assert.Null(outdoor.Lands.Lands(25));                   // seed on Spring 25 cannot finish; no later seed
        Assert.DoesNotContain(sources, s => s.Kind == SourceKind.Crop && s.Reliability == Reliability.Chance);   // the cart seed is a Spring seed too: outdoors it adds nothing
        var greenhouse = sources.Where(s => s.Kind == SourceKind.GreenhouseCrop).ToList();
        var dep = greenhouse.Single(s => s.Reliability == Reliability.Dependable);
        Assert.Equal(32, dep.Lands.Lands(28));                 // seed bought Spring 28, greenhouse, lands Summer 4
        Assert.Null(dep.Lands.Lands(29));                      // hit in week 5: the seed is gone
        var luck = greenhouse.Single(s => s.Reliability == Reliability.Chance);
        Assert.Equal(33, luck.Lands.Lands(29));                // the cart could sell it any day
        Assert.All(greenhouse, s => Assert.Contains("mail:ccPantry", s.Conditions.Requires));
    }

    [Fact]
    public void A_crop_whose_only_seed_source_is_year_two_is_recorded_year_two_flagged()
    {
        var snapshot = new ObtainabilityModel(new Dictionary<string, IReadOnlyList<ObtainSource>>
        {
            ["(O)472"] = new[]
            {
                new ObtainSource(SourceKind.Shop, Spring, Reliability.Dependable,
                    ObtainConditions.None with { YearTwo = true }, "shop SeedShop"),
            },
        });
        var rows = new[] { new CropRow("(O)472", "(O)24", new[] { Season.Spring }, 4, 0) };
        var parsnip = GrowSources.Crops(rows, snapshot).Where(s => s.ItemId == "(O)24").Select(s => s.Source).ToList();
        Assert.NotEmpty(parsnip);                                 // the crop is recorded, not lost
        Assert.All(parsnip, s => Assert.True(s.Conditions.YearTwo));
        Assert.Equal(5, parsnip.First(s => s.Kind == SourceKind.Crop).Lands.Lands(1));
    }

    [Fact]
    public void Mixed_seeds_give_the_planting_days_pool_and_winter_greenhouse_gives_every_pool()
    {
        var snapshot = Snapshot(("(O)770", SourceKind.Forage, DayTable.Always, Reliability.Chance));
        var rows = new[]
        {
            new CropRow("(O)472", "(O)24", new[] { Season.Spring }, 4, 0),
            new CropRow("(O)487", "(O)270", new[] { Season.Summer, Season.Fall }, 14, 4),
            new CropRow("(O)770", "(O)770", new Season[0], 1, 0),
        };
        var parsnip = GrowSources.Crops(rows, snapshot).Where(s => s.ItemId == "(O)24").Select(s => s.Source).ToList();
        var outdoor = parsnip.Single(s => s.Kind == SourceKind.Crop && s.Detail.Contains("Mixed Seeds"));
        Assert.Equal(Reliability.Chance, outdoor.Reliability);
        Assert.Equal(5, outdoor.Lands.Lands(1));
        Assert.Null(outdoor.Lands.Lands(25));
        var indoor = parsnip.Single(s => s.Kind == SourceKind.GreenhouseCrop && s.Detail.Contains("Mixed Seeds"));
        Assert.Equal(89, indoor.Lands.Lands(29));               // Summer 1 hit: the spring pool next comes in Winter (85 + 4)
        Assert.DoesNotContain(GrowSources.Crops(rows, snapshot), s => s.ItemId == "(O)770");
    }

    [Fact]
    public void A_fruit_tree_matures_28_days_after_the_sapling_then_waits_for_its_season()
    {
        var snapshot = Snapshot(
            ("(O)633", SourceKind.Shop, DayTable.Always, Reliability.Dependable),   // apple sapling, Fall fruit
            ("(O)628", SourceKind.Shop, Spring, Reliability.Dependable),            // cherry sapling, Spring fruit
            ("(O)69", SourceKind.Shop, DayTable.Always, Reliability.Dependable));   // banana sapling
        var rows = new[]
        {
            new FruitTreeRow("(O)633", new[] { Season.Fall }, new[] { new FruitRow("(O)613", null, 1.0, null) }),
            new FruitTreeRow("(O)628", new[] { Season.Spring }, new[] { new FruitRow("(O)638", null, 1.0, null) }),
            new FruitTreeRow("(O)69", new[] { Season.Summer }, new[] { new FruitRow("(O)91", null, 0.5, "YEAR 2") }),
        };
        var all = GrowSources.FruitTrees(rows, snapshot, new Dictionary<string, ObjInfo>(), NoFestivals).ToList();
        var apple = all.Single(s => s.ItemId == "(O)613" && s.Source.Kind == SourceKind.FruitTree).Source;
        Assert.Equal(57, apple.Lands.Lands(1));                 // mature by day 29, first Fall day 57
        Assert.Equal(84, apple.Lands.Lands(56));                // planted Summer 28, mature Fall 28
        Assert.Null(apple.Lands.Lands(57));
        Assert.Contains(apple.Setup, s => s.Name == "sapling" && s.Days == 28);
        Assert.DoesNotContain(all, s => s.ItemId == "(O)638" && s.Source.Kind == SourceKind.FruitTree);   // a Spring sapling never fruits outdoors this year
        var cherryIndoor = all.Single(s => s.ItemId == "(O)638" && s.Source.Kind == SourceKind.GreenhouseCrop).Source;
        Assert.Equal(29, cherryIndoor.Lands.Lands(1));
        var banana = all.First(s => s.ItemId == "(O)91").Source;
        Assert.Equal(Reliability.Chance, banana.Reliability);
        Assert.True(banana.Conditions.YearTwo);
    }

    [Fact]
    public void A_tea_bush_gives_leaves_from_day_22_after_20_days_and_only_sheltered_in_winter()
    {
        var snapshot = Snapshot(("(O)251", SourceKind.Crafting, DayTable.Always, Reliability.Dependable));
        var leaves = GrowSources.TeaBush(snapshot).Where(s => s.ItemId == "(O)815").Select(s => s.Source).ToList();
        var outdoor = leaves.Single(s => s.Kind == SourceKind.Crop);
        Assert.Equal(22, outdoor.Lands.Lands(1));               // planted day 1, age 20 on day 21, bloom from day 22
        Assert.Equal(23, outdoor.Lands.Lands(3));               // planted day 3, age 20 on day 23, and Spring 23 is already past the 22nd
        Assert.Null(outdoor.Lands.Lands(65));                   // Fall 9: age 20 lands Fall 29 = Winter, outdoors never
        var sheltered = leaves.Single(s => s.Kind == SourceKind.GreenhouseCrop);
        Assert.Equal(106, sheltered.Lands.Lands(65));           // Winter 22
        Assert.Contains(outdoor.Setup, s => s.Name == "tea bush" && s.Days == 20);
        Assert.Empty(GrowSources.TeaBush(new ObtainabilityModel(new Dictionary<string, IReadOnlyList<ObtainSource>>())));
    }
}
