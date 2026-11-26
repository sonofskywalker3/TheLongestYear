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
    public void A_year_two_shop_row_beside_a_cart_row_never_reaches_a_default_answer()
    {
        // The Garlic shape: Garlic Seeds are dependable at Pierre in YEAR 2 and a chance at the cart
        // any year. The year 2 row must not feed an answer that excludes year 2, and the cart row must
        // still give the crop its chance route. Each filter variant of the seed is derived separately.
        var snapshot = new ObtainabilityModel(new Dictionary<string, IReadOnlyList<ObtainSource>>
        {
            ["(O)476"] = new[]
            {
                new ObtainSource(SourceKind.Shop, Spring, Reliability.Dependable,
                    ObtainConditions.None with { YearTwo = true }, "shop SeedShop"),
                new ObtainSource(SourceKind.Cart, DayTable.Always, Reliability.Chance, ObtainConditions.None, "shop Traveler"),
            },
        });
        var rows = new[] { new CropRow("(O)476", "(O)248", new[] { Season.Spring }, 4, 0) };
        var garlic = new ObtainabilityModel(GrowSources.Crops(rows, snapshot)
            .GroupBy(x => x.ItemId).ToDictionary(g => g.Key, g => (IReadOnlyList<ObtainSource>)g.Select(x => x.Source).ToList()));

        // Default filter: no dependable route at all, and the chance route is the cart's.
        Assert.True(garlic.Table("(O)248", ObtainFilter.DependableOnly).IsEmpty);
        Assert.Equal(5, garlic.Lands("(O)248", 1, ObtainFilter.Any));
        // Year 2 included: the shop row lands, dependably, and every source that carries it says so.
        Assert.Equal(5, garlic.Lands("(O)248", 1, ObtainFilter.DependableOnly with { IncludeYearTwo = true }));
        Assert.All(garlic.Sources("(O)248").Where(x => x.Reliability == Reliability.Dependable),
            x => Assert.True(x.Conditions.YearTwo));
        // Island is nothing to do with this seed, so no island-only variant was recorded.
        Assert.DoesNotContain(garlic.Sources("(O)248"), x => x.Conditions.GingerIsland);
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
    public void A_wild_tree_that_stands_nowhere_gives_its_drops_only_after_its_seed_is_planted_and_grown()
    {
        var snapshot = Snapshot(("(O)CinnamonSeed", SourceKind.Shop, DayTable.Always, Reliability.Dependable));
        var trees = new[]
        {
            new WildTreeRow("Cinnamon", "(O)CinnamonSeed", true, 0.2, false, Standing: false),
            new WildTreeRow("Oak", "(O)309", true, 0.2, false, Standing: true),
            new WildTreeRow("7", "(O)891", true, 0.2, false, Standing: false),        // Mushroom Tree: made by the Fall turn, not a seed
            new WildTreeRow("Mystic", "(O)NoSuchSeed", true, 0.2, false, Standing: false),
        };
        var drops = new[]
        {
            new TreeDropRow("Cinnamon", "(O)Sticks", TreeDropKind.Chop, null, 1.0, null),
            new TreeDropRow("Cinnamon", "(O)Sticks", TreeDropKind.Shake, null, 0.2, null),
            new TreeDropRow("Mystic", "(O)709", TreeDropKind.Chop, null, 1.0, null),
        };
        var taps = new[] { new TapRow("Cinnamon", "(O)Syrup", 7, null, 1.0, null) };

        Assert.Equal(new[] { "Cinnamon", "Mystic" }, LivestockSources.PlantedOnlyTreeIds(trees).OrderBy(t => t).ToArray());
        var routes = LivestockSources.PlantedTreeDrops(drops, taps, trees, snapshot, new Dictionary<string, ObjInfo>(), NoFestivals).ToList();
        var chop = routes.Single(r => r.ItemId == "(O)Sticks" && r.Source.Reliability == Reliability.Dependable).Source;
        Assert.Equal(26, chop.Lands.Lands(1));                  // planted day 1, a stage on mornings 2 to 26 at 0.2 a night
        Assert.Null(chop.Lands.Lands(80));                      // four mornings before Winter, then no growth
        Assert.Contains("item:(O)CinnamonSeed", chop.Conditions.Requires);
        Assert.Contains(chop.Setup, s => s.Name == "tree" && s.Days == 25);
        Assert.Contains(routes, r => r.ItemId == "(O)Sticks" && r.Source.Reliability == Reliability.Chance && r.Source.Lands.Lands(1) == 26);
        Assert.Equal(33, routes.Single(r => r.ItemId == "(O)Syrup").Source.Lands.Lands(1));   // grown day 26, then 7 nights in the tapper
        Assert.DoesNotContain(routes, r => r.ItemId == "(O)709");   // a seed with no route grows no tree
        Assert.False(PlantedTrees.StandingCounts("10"));        // green rain trees stand from Summer: not recorded
        Assert.True(PlantedTrees.StandingCounts("Cinnamon"));
        Assert.Equal(10, PlantedTrees.GrowthNights(0.5));
        Assert.Null(PlantedTrees.GrowthNights(0));
    }

    [Fact]
    public void A_sapling_keyed_as_a_wild_tree_seed_too_plants_the_wild_tree_not_the_fruit_tree()
    {
        var trees = new[]
        {
            new WildTreeRow("Camphor", "(O)CamphorSapling", true, 0.2, false, false),
            new WildTreeRow("Unplantable", "(O)OliveSeeds", false, 0.2, false, false),
        };
        IReadOnlySet<string> seeds = PlantedTrees.WildSeedLocalIds(trees);
        Assert.True(PlantedTrees.PlantsAWildTree("CamphorSapling", seeds));
        Assert.True(PlantedTrees.PlantsAWildTree("(O)CamphorSapling", seeds));
        Assert.False(PlantedTrees.PlantsAWildTree("OliveSeeds", seeds));    // not SeedPlantable: the fruit tree grows
        Assert.False(PlantedTrees.PlantsAWildTree("628", seeds));
    }

    [Fact]
    public void A_seed_drop_behind_a_sure_drop_under_the_same_condition_is_never_reached()
    {
        const string fallOrIndoors = "ANY \"LOCATION_SEASON Town fall\" \"LOCATION_IS_INDOORS Here\"";
        var datePalm = new[]
        {
            new PlantedTrees.SeedDrop(1.0, null, fallOrIndoors, PlainItem: true, ContinueOnDrop: false),                 // Date
            new PlantedTrees.SeedDrop(1.0, null, "LOCATION_SEASON Town spring summer winter", true, false),               // Palm Nut
            new PlantedTrees.SeedDrop(0.25, null, fallOrIndoors, true, false),                                           // Palm Nut, never
        };
        Assert.Equal(new[] { true, true, false }, PlantedTrees.ReachableSeedDrops(datePalm));

        var notSure = new[]
        {
            new PlantedTrees.SeedDrop(1.0, null, "RANDOM 0.5", true, false),           // a random roll can fail
            new PlantedTrees.SeedDrop(1.0, null, "RANDOM 0.5", true, false),
            new PlantedTrees.SeedDrop(1.0, Season.Fall, null, true, false),            // only in Fall
            new PlantedTrees.SeedDrop(1.0, Season.Spring, null, true, false),
            new PlantedTrees.SeedDrop(1.0, null, null, true, ContinueOnDrop: true),    // goes on to the next row
            new PlantedTrees.SeedDrop(1.0, null, null, PlainItem: false, false),       // a query can come up empty
            new PlantedTrees.SeedDrop(0.5, null, null, true, false),
            new PlantedTrees.SeedDrop(1.0, null, null, true, false),                   // always drops: blocks what follows
            new PlantedTrees.SeedDrop(1.0, Season.Winter, "SEASON winter", true, false),
        };
        Assert.Equal(new[] { true, true, true, true, true, true, true, true, false }, PlantedTrees.ReachableSeedDrops(notSure));
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
        // Sheltered means a greenhouse OR an indoor pot (Bush.IsSheltered, Bush.cs 196-205), and a pot
        // needs no unlock, so this source must not demand the pantry bundle.
        Assert.Contains("sheltered (greenhouse or indoor pot)", sheltered.Conditions.Requires);
        Assert.DoesNotContain("mail:ccPantry", sheltered.Conditions.Requires);
        Assert.Empty(GrowSources.TeaBush(new ObtainabilityModel(new Dictionary<string, IReadOnlyList<ObtainSource>>())));
    }
}
