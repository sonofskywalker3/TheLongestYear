using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;
using TheLongestYear.Core.Obtainability;

namespace TheLongestYear.Tests;

public class ObtainabilityMadeTests
{
    private static readonly Dictionary<string, FestivalDates> NoFestivals = new();
    private static readonly CropRow[] NoCrops = new CropRow[0];
    private static readonly Dictionary<string, int> NoBuildings = new();
    private static readonly Dictionary<string, WeekMask> NoShopWeeks = new();

    private static ObtainabilityModel Snapshot(params (string Id, SourceKind Kind, DayTable Lands, Reliability R)[] rows)
        => new(rows.GroupBy(r => r.Id).ToDictionary(
            g => g.Key,
            g => (IReadOnlyList<ObtainSource>)g.Select(r => new ObtainSource(r.Kind, r.Lands, r.R, ObtainConditions.None, "test")).ToList()));

    private static readonly Dictionary<string, ObjInfo> Objects = new()
    {
        ["(O)613"] = new ObjInfo("(O)613", "Apple", -79, 100, new[] { "category_fruits", "id_o_613" }, false),
        ["(O)254"] = new ObjInfo("(O)254", "Melon", -79, 250, new[] { "category_fruits", "id_o_254" }, false),
        ["(O)24"] = new ObjInfo("(O)24", "Parsnip", -75, 35, new[] { "category_vegetable" }, false),
        ["(O)142"] = new ObjInfo("(O)142", "Carp", -4, 30, new[] { "fish_carp", "fish_pond" }, false),
    };

    private static MachineRow Rule(string machine, string? id, string[] tags, int minutes, int days, params MachineOutput[] outputs)
        => new(machine, id, tags, null, outputs, minutes, days);

    [Fact]
    public void Ready_time_prefers_days_and_rounds_minutes_up_to_days()
    {
        Assert.Equal(7, MadeSources.ProcessingDays(10000, -1));
        Assert.Equal(3, MadeSources.ProcessingDays(4000, -1));
        Assert.Equal(2, MadeSources.ProcessingDays(10000, 2));
    }

    [Fact]
    public void A_machine_lands_its_input_plus_processing_and_never_makes_its_input_earlier()
    {
        var snapshot = Snapshot(("(O)613", SourceKind.FruitTree, DayTable.Available(d => d >= 57), Reliability.Dependable));
        var rows = new[]
        {
            new MachineRow("(BC)12", null, new[] { "category_fruits" }, null, new[] { new MachineOutput("(O)348", null, null) }, 10000, -1),
            new MachineRow("(BC)Dehydrator", null, new[] { "category_fruits" }, null, new[] { new MachineOutput("DROP_IN", null, null) }, 0, 1),
        };
        var all = MadeSources.Machines(rows, Objects, snapshot, NoFestivals, NoCrops).ToList();
        var wine = all.Single(s => s.ItemId == "(O)348").Source;
        Assert.Equal(64, wine.Lands.Lands(1));                  // apple day 57 + 7 days
        Assert.Null(wine.Lands.Lands(106));
        var apple = all.Single(s => s.ItemId == "(O)613").Source;   // DROP_IN keyed under the input
        Assert.Equal(58, apple.Lands.Lands(1));                 // one day later than the apple itself, never earlier
    }

    [Fact]
    public void A_machine_source_records_the_item_it_is_made_from()
    {
        // The table says WHEN the wine lands, never that it took an apple. A consumer checking a
        // route against a real save needs the input id to ask the same question of it.
        var snapshot = Snapshot(("(O)613", SourceKind.FruitTree, DayTable.Available(d => d >= 57), Reliability.Dependable));
        var rows = new[]
        {
            new MachineRow("(BC)12", null, new[] { "category_fruits" }, null, new[] { new MachineOutput("(O)348", null, null) }, 10000, -1),
        };
        var wine = MadeSources.Machines(rows, Objects, snapshot, NoFestivals, NoCrops)
            .Single(s => s.ItemId == "(O)348").Source;
        Assert.Equal(new[] { "(O)613" }, Assert.Single(wine.Inputs));
    }

    [Fact]
    public void A_trigger_with_an_id_and_tags_needs_both()
    {
        var snapshot = Snapshot(("(O)613", SourceKind.FruitTree, DayTable.Always, Reliability.Dependable));
        var rows = new[] { Rule("(BC)X", "(O)613", new[] { "category_vegetable" }, 60, -1, new MachineOutput("(O)900", null, null)) };
        Assert.Empty(MadeSources.Machines(rows, Objects, snapshot, NoFestivals, NoCrops));
    }

    [Fact]
    public void A_seed_maker_returns_a_crops_seed_and_a_mushroom_log_gives_mushrooms()
    {
        var snapshot = Snapshot(("(O)24", SourceKind.Crop, DayTable.Available(d => d >= 5), Reliability.Dependable));
        var crops = new[] { new CropRow("(O)472", "(O)24", new[] { Season.Spring }, 4, 0) };
        var rows = new[]
        {
            new MachineRow("(BC)25", null, new[] { "!seedmaker_banned" }, null, new[] { new MachineOutput(null, null, "OutputSeedMaker", false, OutputMethodKind.SeedMaker) }, 20, -1),
            new MachineRow("(BC)MushroomLog", null, new string[0], null, new[] { new MachineOutput(null, null, "OutputMushroomLog", false, OutputMethodKind.MushroomLog) }, 0, 6),
            new MachineRow("(BC)163", "(O)348", new string[0], null, new[] { new MachineOutput(null, null, "OutputCask", false, OutputMethodKind.Cask) }, 0, 14),
        };
        var all = MadeSources.Machines(rows, Objects, snapshot, NoFestivals, crops).ToList();
        var seeds = all.Single(s => s.ItemId == "(O)472" && s.Source.Kind == SourceKind.Machine).Source;
        Assert.Equal(6, seeds.Lands.Lands(1));                  // parsnip day 5, 20 minutes rounds up to 1 day
        Assert.Equal(Reliability.Dependable, seeds.Reliability);
        Assert.Contains(all, s => s.ItemId == "(O)770" && s.Source.Reliability == Reliability.Chance);
        Assert.Contains(all, s => s.ItemId == "(O)499" && s.Source.Reliability == Reliability.Chance);
        foreach (string mushroom in new[] { "(O)404", "(O)420", "(O)422", "(O)257", "(O)281" })
        {
            var m = all.Single(s => s.ItemId == mushroom && s.Source.Kind == SourceKind.Machine
                && s.Source.Conditions.Requires.Contains("machine:(BC)MushroomLog")).Source;
            Assert.Equal(Reliability.Chance, m.Reliability);
            Assert.Equal(7, m.Lands.Lands(1));
        }
        Assert.DoesNotContain(all, s => s.Source.Detail.Contains("(BC)163"));   // casks change quality only
        Assert.DoesNotContain(all, s => s.ItemId.StartsWith(ItemQueries.UnresolvedPrefix) && s.Source.Detail.Contains("Cask"));
    }

    [Fact]
    public void Output_conditions_narrow_and_several_outputs_are_chance()
    {
        var snapshot = Snapshot(("(O)24", SourceKind.Crop, DayTable.Always, Reliability.Dependable));
        var rows = new[] { Rule("(BC)Z", "(O)24", new string[0], 0, 0,
            new MachineOutput("(O)901", "SEASON Summer", null), new MachineOutput("(O)902", null, null)) };
        var list = MadeSources.Machines(rows, Objects, snapshot, NoFestivals, NoCrops).ToList();
        var summer = list.Single(x => x.ItemId == "(O)901").Source;
        Assert.Equal(DayTable.InWeeks(WeekMask.ForSeason(Season.Summer)), summer.Lands);
        Assert.All(list, x => Assert.Equal(Reliability.Chance, x.Source.Reliability));
    }

    [Fact]
    public void First_valid_outputs_are_tried_in_order()
    {
        var snapshot = Snapshot(("(O)24", SourceKind.Crop, DayTable.Always, Reliability.Dependable));
        var rows = new[] { new MachineRow("(BC)F", "(O)24", new string[0], null, new[]
            { new MachineOutput("(O)901", "SEASON Summer", null), new MachineOutput("(O)902", null, null) },
            0, 0, UseFirstValidOutput: true) };
        var list = MadeSources.Machines(rows, Objects, snapshot, NoFestivals, NoCrops).ToList();
        var first = list.Single(x => x.ItemId == "(O)901").Source;
        Assert.Equal(DayTable.InWeeks(WeekMask.ForSeason(Season.Summer)), first.Lands);
        Assert.Equal(Reliability.Dependable, first.Reliability);
        var second = list.Single(x => x.ItemId == "(O)902").Source;
        Assert.Equal(DayTable.InWeeks(WeekMask.All.Except(WeekMask.ForSeason(Season.Summer))), second.Lands);   // never in summer
        Assert.Equal(Reliability.Dependable, second.Reliability);
    }

    [Fact]
    public void A_recipe_needs_every_ingredient_in_the_same_week_and_a_shop_recipe_once_learned()
    {
        var snapshot = Snapshot(
            ("(O)24", SourceKind.Crop, DayTable.InWeeks(WeekMask.ForSeason(Season.Spring)), Reliability.Dependable),
            ("(O)613", SourceKind.FruitTree, DayTable.InWeeks(WeekMask.Range(3, 10)), Reliability.Chance));
        var rows = new[]
        {
            new RecipeRow("Test Dish", new[] { "(O)24", "-79" }, "(O)900", "default", true),
            new RecipeRow("Shop Dish", new[] { "(O)24" }, "(O)901", "none", true),
            new RecipeRow("Skill Craft", new[] { "(O)24" }, "(BC)902", "s Farming 3", false),
            new RecipeRow("Split Seasons", new[] { "(O)24", "(O)254" }, "(O)903", "default", true),
            new RecipeRow("Taught Elsewhere", new[] { "(O)24" }, "(O)904", "none", true),
            new RecipeRow("Either Output", new[] { "(O)24" }, "(O)905", "default", false, new[] { "(O)906" }),
        };
        var shopWeeks = new Dictionary<string, WeekMask> { ["(O)901"] = WeekMask.Of(3) };
        var list = MadeSources.Recipes(rows, Objects, shopWeeks, snapshot).ToList();
        // Ingredient "-79" is the fruit category: only (O)613 supplies it, and only as a chance
        // (week 3-10) source, so the dish is never dependable, and it needs day 15 (week 3, the
        // earliest the fruit is in) before the parsnip alone (day 1) would allow it.
        var dish = list.Single(x => x.ItemId == "(O)900").Source;
        Assert.Equal(15, dish.Lands.Lands(1));
        Assert.Equal(Reliability.Chance, dish.Reliability);
        // Learned "none" but a shop teaches it week 3 (day 15): same day 15, and now dependable
        // because the parsnip alone (dependable) is the only ingredient.
        var shopDish = list.Single(x => x.ItemId == "(O)901").Source;
        Assert.Equal(15, shopDish.Lands.Lands(1));
        Assert.Equal(Reliability.Dependable, shopDish.Reliability);
        var craft = list.Single(x => x.ItemId == "(BC)902").Source;
        Assert.Equal(SourceKind.Crafting, craft.Kind);
        Assert.Equal("Farming", craft.Conditions.Skill);
        Assert.Equal(3, craft.Conditions.SkillLevel);
        Assert.DoesNotContain(list, x => x.ItemId == "(O)903");   // melon is not obtainable at all and nothing is stored
        Assert.False(list.Single(x => x.ItemId == "(O)901").Source.Conditions.Unresolved);   // "none", but a shop teaches it
        Assert.True(list.Single(x => x.ItemId == "(O)904").Source.Conditions.Unresolved);    // "none" and no shop: taught somewhere unknown
        Assert.Equal(Reliability.Chance, list.Single(x => x.ItemId == "(O)905").Source.Reliability);
        Assert.Equal(Reliability.Chance, list.Single(x => x.ItemId == "(O)906").Source.Reliability);
    }

    [Fact]
    public void A_queen_of_sauce_recipe_lands_on_the_sunday_of_its_episode_week()
    {
        var snapshot = Snapshot(("(O)24", SourceKind.Crop, DayTable.Always, Reliability.Dependable));
        var rows = new[]
        {
            new RecipeRow("Chocolate Cake", new[] { "(O)24" }, "(O)220", "l 0", true),
            new RecipeRow("Year Two Dish", new[] { "(O)24" }, "(O)907", "l 0", true),
            new RecipeRow("Friend Dish", new[] { "(O)24" }, "(O)908", "f Robin 7", true),
            new RecipeRow("Off The Air", new[] { "(O)24" }, "(O)909", "none", true),
        };
        var channel = new Dictionary<string, int>
        {
            ["Chocolate Cake"] = 14, ["Year Two Dish"] = 20, ["Friend Dish"] = 3,
        };
        var list = MadeSources.Recipes(rows, Objects, NoShopWeeks, snapshot, channel).ToList();

        // Episode 14 airs on day 98, the Sunday of week 14; the parsnip is there from day 1, so the
        // TV is the whole wait, and once learned it stays learned (starting on day 99 it lands day 99).
        var cake = Assert.Single(list.Where(x => x.ItemId == "(O)220")).Source;
        Assert.Equal(98, cake.Lands.Lands(1));
        Assert.Equal(14, cake.Lands.LandingWeek(1));
        Assert.Equal(99, cake.Lands.Lands(99));
        Assert.Equal(Reliability.Dependable, cake.Reliability);
        Assert.False(cake.Conditions.Unresolved);
        Assert.False(cake.Conditions.FewDays);
        Assert.False(cake.Conditions.YearTwo);
        Assert.Contains("recipe:Chocolate Cake", cake.Conditions.Requires);
        Assert.Contains("unlock:Queen of Sauce episode 14 (Sunday of week 14)", cake.Conditions.Requires);

        // Episode 20 airs in year 2: one source, flagged, and the "taught some other way" guess is gone.
        var yearTwo = Assert.Single(list.Where(x => x.ItemId == "(O)907")).Source;
        Assert.True(yearTwo.Conditions.YearTwo);
        Assert.False(yearTwo.Conditions.Unresolved);
        Assert.Equal(1, yearTwo.Lands.Lands(1));

        // A friendship unlock is a route of its own: it stays, and the TV route is added beside it.
        var friend = list.Where(x => x.ItemId == "(O)908").Select(x => x.Source).ToList();
        Assert.Equal(2, friend.Count);
        Assert.Contains(friend, s => s.Conditions.Requires.Contains("unlock:f Robin 7"));
        var onAir = friend.Single(s => s.Conditions.Requires.Contains("unlock:Queen of Sauce episode 3 (Sunday of week 3)"));
        Assert.Equal(21, onAir.Lands.Lands(1));

        // Not in the channel at all: unchanged, still a guess.
        var offAir = Assert.Single(list.Where(x => x.ItemId == "(O)909")).Source;
        Assert.True(offAir.Conditions.Unresolved);
    }

    [Fact]
    public void A_shop_taught_recipe_the_tv_also_teaches_keeps_both_routes()
    {
        var snapshot = Snapshot(("(O)24", SourceKind.Crop, DayTable.Always, Reliability.Dependable));
        var rows = new[]
        {
            new RecipeRow("Shop And TV Dish", new[] { "(O)24" }, "(O)910", "none", true),
            new RecipeRow("Shop And TV Level Dish", new[] { "(O)24" }, "(O)911", "l 0", true),
        };
        var shopWeeks = new Dictionary<string, WeekMask> { ["(O)910"] = WeekMask.Of(3), ["(O)911"] = WeekMask.Of(3) };
        var channel = new Dictionary<string, int> { ["Shop And TV Dish"] = 9, ["Shop And TV Level Dish"] = 9 };
        var all = MadeSources.Recipes(rows, Objects, shopWeeks, snapshot, channel).ToList();

        // The shop's week is a real answer already, so the TV route is added beside it, not over it.
        var list = all.Where(x => x.ItemId == "(O)910").Select(x => x.Source).ToList();
        Assert.Equal(2, list.Count);
        var shop = list.Single(s => s.Conditions.Requires.Contains("unlock:shop"));
        Assert.Equal(15, shop.Lands.Lands(1));   // week 3 starts on day 15
        var tv = list.Single(s => s.Conditions.Requires.Contains("unlock:Queen of Sauce episode 9 (Sunday of week 9)"));
        Assert.Equal(63, tv.Lands.Lands(1));     // day 7 x 9
        Assert.All(list, s => Assert.Equal(Reliability.Dependable, s.Reliability));
        Assert.All(list, s => Assert.False(s.Conditions.Unresolved));

        // Same for a farmhouse-level unlock, whose note is the raw unlock rather than "unlock:shop":
        // it too already carries the shop's week, so the shop route survives.
        var level = all.Where(x => x.ItemId == "(O)911").Select(x => x.Source).ToList();
        Assert.Equal(2, level.Count);
        Assert.Equal(15, level.Single(s => s.Conditions.Requires.Contains("unlock:l 0")).Lands.Lands(1));
        Assert.Equal(63, level.Single(s => s.Conditions.Requires.Contains("unlock:Queen of Sauce episode 9 (Sunday of week 9)")).Lands.Lands(1));
        Assert.All(level, s => Assert.False(s.Conditions.Unresolved));
    }

    [Fact]
    public void Tea_sapling_and_wild_bait_are_taught_by_friendship_not_unresolved()
    {
        var rows = new[]
        {
            new RecipeRow("Tea Sapling", new[] { "(O)771" }, "(O)251", "null", IsCooking: false),
            new RecipeRow("Wild Bait", new[] { "(O)771" }, "(O)774", "null", IsCooking: false),
        };
        var objects = new Dictionary<string, ObjInfo> { ["(O)771"] = new("(O)771", "Fiber", -16, 1, new List<string>(), false) };
        var snapshot = new ObtainabilityModel(new Dictionary<string, IReadOnlyList<ObtainSource>>
        {
            ["(O)771"] = new[] { new ObtainSource(SourceKind.Forage, DayTable.Always, Reliability.Dependable, ObtainConditions.None, "weeds") },
        });
        var made = MadeSources.Recipes(rows, objects, new Dictionary<string, WeekMask>(), snapshot).ToList();
        ObtainSource tea = made.First(m => m.ItemId == "(O)251").Source;
        ObtainSource bait = made.First(m => m.ItemId == "(O)774").Source;
        Assert.False(tea.Conditions.Unresolved);
        Assert.Contains("unlock:f Caroline 2", tea.Conditions.Requires);
        Assert.False(bait.Conditions.Unresolved);
        Assert.Contains("unlock:f Linus 4", bait.Conditions.Requires);
    }

    [Fact]
    public void The_any_wild_seed_ingredient_reads_the_four_seed_packets()
    {
        var rows = new[]
        {
            new RecipeRow("Tea Sapling", new[] { "-777", "(O)771" }, "(O)251", "null", IsCooking: false),
        };
        var objects = new Dictionary<string, ObjInfo> { ["(O)771"] = new("(O)771", "Fiber", -16, 1, new List<string>(), false) };
        // Spring, summer and winter seeds are in the model too (Pierre sells each in its own season),
        // just not landing as soon as fall's: only fall seeds and the fiber need to be at day 1 for
        // the recipe itself to land day 1, but all four ids still belong in the merged group.
        var laterSeed = new ObtainSource(SourceKind.Shop, DayTable.Available(d => d >= 50), Reliability.Dependable, ObtainConditions.None, "seasonal seeds");
        var snapshot = new ObtainabilityModel(new Dictionary<string, IReadOnlyList<ObtainSource>>
        {
            ["(O)495"] = new[] { laterSeed },
            ["(O)496"] = new[] { laterSeed },
            ["(O)497"] = new[] { new ObtainSource(SourceKind.Shop, DayTable.Always, Reliability.Dependable, ObtainConditions.None, "fall seeds") },
            ["(O)498"] = new[] { laterSeed },
            ["(O)771"] = new[] { new ObtainSource(SourceKind.Forage, DayTable.Always, Reliability.Dependable, ObtainConditions.None, "weeds") },
        });
        var made = MadeSources.Recipes(rows, objects, new Dictionary<string, WeekMask>(), snapshot).ToList();
        ObtainSource tea = made.First(m => m.ItemId == "(O)251").Source;
        Assert.Equal(1, tea.Lands.Lands(1));
        Assert.Equal(Reliability.Dependable, tea.Reliability);
        Assert.Contains(tea.Inputs, group => new[] { "(O)495", "(O)496", "(O)497", "(O)498" }.All(group.Contains) && group.Count == 4);
    }

    [Fact]
    public void Ponds_use_only_the_lowest_precedence_match_and_carry_forward()
    {
        var snapshot = Snapshot(("(O)142", SourceKind.Fish, DayTable.InWeeks(WeekMask.Of(5)), Reliability.Dependable));
        var ponds = new[]
        {
            new PondRow("Generic", new[] { "fish_pond" }, 10, new[] { new PondProduct("(O)900", 1, 1.0, null) }),
            new PondRow("Carp", new[] { "fish_carp" }, 0, new[] { new PondProduct("(O)812", 1, 0.5, null), new PondProduct("(O)901", 5, 1.0, "SEASON Winter") }),
        };
        var list = MadeSources.Ponds(ponds, Objects, snapshot, NoFestivals, NoBuildings).ToList();
        Assert.DoesNotContain(list, x => x.ItemId == "(O)900");
        // Fish lands week 5 (day 29); population is already 1, so no further growth is needed, but
        // the 0.5 chance means it is not dependable.
        var roe = list.Single(x => x.ItemId == "(O)812").Source;
        Assert.Equal(29, roe.Lands.Lands(1));
        Assert.Equal(Reliability.Chance, roe.Reliability);
        // Population 5 needs 4 more fish at the default 1-day spawn time (day 29 + 4 = day 33), but
        // it is Winter-only, so it waits for Winter 1 (day 85).
        var deluxe = list.Single(x => x.ItemId == "(O)901").Source;
        Assert.Equal(85, deluxe.Lands.Lands(1));
        Assert.Equal(Reliability.Dependable, deluxe.Reliability);
    }

    [Fact]
    public void An_animal_records_its_setup_days_and_lands_after_days_to_produce()
    {
        var buildings = new Dictionary<string, int> { ["Coop"] = 3 };
        var rows = new[]
        {
            new AnimalRow("Chicken", "Coop", 800,
                new[] { new AnimalProduce("(O)176", null, 0) }, new[] { new AnimalProduce("(O)174", null, 0) }, 200, DaysToProduce: 1),
        };
        var all = MadeSources.Animals(rows, NoFestivals, buildings).ToList();
        var egg = all.Single(s => s.ItemId == "(O)176").Source;
        Assert.Equal(2, egg.Lands.Lands(1));
        Assert.Contains(egg.Setup, s => s.Name == "building:Coop" && s.Days == 3);
        Assert.Contains(egg.Setup, s => s.Name == "animal:Chicken" && s.Days == 1);
        var large = all.Single(s => s.ItemId == "(O)174").Source;
        Assert.Contains(large.Setup, s => s.Name == "friendship:Chicken 200" && s.Days == 14);
        Assert.Equal(2, large.Lands.Lands(1));                  // the blind table assumes setup done (spec decision 3)
    }

    [Fact]
    public void A_bought_animal_waits_out_growing_up()
    {
        var cow = new AnimalRow("White Cow", "Barn", 750, new[] { new AnimalProduce("(O)184", null, 0) },
            new AnimalProduce[0], DaysToMature: 5);
        SetupStep step = MadeSources.AnimalSetup(cow, new Dictionary<string, int>(), 0).Single(s => s.Name == "animal:White Cow");
        Assert.Equal(5, step.Days);
    }

    [Fact]
    public void A_hatched_only_animal_is_owned_only_and_waits_for_incubation_and_growing_up()
    {
        var dino = new AnimalRow("Dinosaur", "Coop", -1, new[] { new AnimalProduce("(O)107", null, 0) },
            new AnimalProduce[0], DaysToProduce: 7, DaysToMature: 0, IncubationDays: 13);
        ObtainSource egg = MadeSources.Animals(new[] { dino }, NoFestivals, NoBuildings).Single().Source;
        Assert.True(egg.Conditions.OwnedOnly);
        Assert.Equal(13, egg.Setup.Single(s => s.Name == "animal:Dinosaur").Days);
    }

    [Fact]
    public void A_machine_fed_only_an_owned_only_input_emits_an_owned_only_dependable_source()
    {
        var snapshot = new ObtainabilityModel(new Dictionary<string, IReadOnlyList<ObtainSource>>
        {
            ["(O)24"] = new ObtainSource[]
            {
                new(SourceKind.Animal, DayTable.Available(d => d >= 5), Reliability.Dependable,
                    ObtainConditions.None with { OwnedOnly = true }, "owned egg"),
            },
        });
        var rows = new[]
        {
            new MachineRow("(BC)Mayo", null, new[] { "category_vegetable" }, null, new[] { new MachineOutput("(O)999", null, null) }, 0, 2),
        };
        ObtainSource mayo = MadeSources.Machines(rows, Objects, snapshot, NoFestivals, NoCrops).Single(s => s.ItemId == "(O)999").Source;
        Assert.Equal(Reliability.Dependable, mayo.Reliability);
        Assert.True(mayo.Conditions.OwnedOnly);
        Assert.False(ObtainFilter.DependableOnly.Accepts(mayo));
        Assert.True((ObtainFilter.DependableOnly with { IncludeOwnedOnly = true }).Accepts(mayo));
    }

    [Fact]
    public void A_machine_fed_an_owned_only_input_beside_an_ordinary_one_keeps_a_plain_dependable_source()
    {
        var snapshot = new ObtainabilityModel(new Dictionary<string, IReadOnlyList<ObtainSource>>
        {
            ["(O)24"] = new ObtainSource[]
            {
                new(SourceKind.Crop, DayTable.Always, Reliability.Dependable, ObtainConditions.None, "ordinary"),
                new(SourceKind.Animal, DayTable.Always, Reliability.Dependable, ObtainConditions.None with { OwnedOnly = true }, "owned"),
            },
        });
        var rows = new[]
        {
            new MachineRow("(BC)Mayo", null, new[] { "category_vegetable" }, null, new[] { new MachineOutput("(O)999", null, null) }, 0, 2),
        };
        var mayo = MadeSources.Machines(rows, Objects, snapshot, NoFestivals, NoCrops)
            .Where(s => s.ItemId == "(O)999").Select(s => s.Source).ToList();
        // Both the ordinary and the owned-only source land the same day, so the owned-only variant
        // is trimmed (its table says nothing the plain variant did not already say) and only the
        // plain, unflagged Dependable source survives.
        Assert.Single(mayo);
        Assert.False(mayo[0].Conditions.OwnedOnly);
    }

    [Fact]
    public void An_alternate_purchase_is_sold()
    {
        var brown = new AnimalRow("Brown Cow", "Barn", -1, new[] { new AnimalProduce("(O)184", null, 0) },
            new AnimalProduce[0], SoldAsAlternate: true);
        ObtainSource milk = MadeSources.Animals(new[] { brown }, NoFestivals, NoBuildings).Single().Source;
        Assert.False(milk.Conditions.OwnedOnly);
        Assert.DoesNotContain(milk.Conditions.Requires, r => r.EndsWith("(not sold)"));
    }

    [Fact]
    public void A_pond_counts_population_growth_from_its_spawn_time()
    {
        var snapshot = Snapshot(("(O)142", SourceKind.Fish, DayTable.Always, Reliability.Dependable));
        var rows = new[] { new PondRow("Carp", new[] { "fish_carp" }, 0, new[] { new PondProduct("(O)812", 3, 1.0, null) }, SpawnTime: 4) };
        var buildings = new Dictionary<string, int> { ["Fish Pond"] = 2 };
        var roe = MadeSources.Ponds(rows, Objects, snapshot, NoFestivals, buildings).Single(s => s.ItemId == "(O)812").Source;
        Assert.Equal(9, roe.Lands.Lands(1));                    // fish day 1, two more fish at 4 days each = day 9
        Assert.Contains(roe.Setup, s => s.Name == "building:Fish Pond" && s.Days == 2);
    }

    [Fact]
    public void Animals_tappers_and_geodes_respect_their_conditions()
    {
        var animals = MadeSources.Animals(new[]
        {
            new AnimalRow("White Chicken", "Coop", 800,
                new[] { new AnimalProduce("(O)176", null, 0) },
                new[] { new AnimalProduce("(O)174", "SEASON Spring", 200) }),
        }, NoFestivals, NoBuildings).ToList();
        Assert.Equal(2, animals.Single(a => a.ItemId == "(O)176").Source.Lands.Lands(1));   // always available, +1 day to produce
        var large = animals.Single(a => a.ItemId == "(O)174").Source;
        Assert.Equal(2, large.Lands.Lands(1));   // Spring day 1, +1 day to produce
        Assert.Contains(large.Setup, s => s.Name == "friendship:White Chicken 200");

        var taps = MadeSources.Tappers(new[] { new TapRow("7", "(O)422", 4, Season.Fall, 0.9, null) }, Objects, NoFestivals).Single().Source;
        Assert.Equal(61, taps.Lands.Lands(1));   // Fall day 1 (day 57) + 4 days until ready
        Assert.Equal(Reliability.Chance, taps.Reliability);

        var snapshot = Snapshot(("(O)535", SourceKind.Geode, DayTable.InWeeks(WeekMask.Range(2, 3)), Reliability.Chance));
        var geode = MadeSources.Geodes(new GeodeDropRow[0], new[] { "(O)535" }, Objects, snapshot, NoFestivals).ToList();
        Assert.Contains(geode, g => g.ItemId == "(O)86" && g.Source.Lands.ToString() == "lands wk2/never/never/never");
        Assert.All(geode, g => Assert.Equal(SourceKind.Geode, g.Source.Kind));
    }
}
