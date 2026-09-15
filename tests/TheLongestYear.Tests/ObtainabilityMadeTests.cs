using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;
using TheLongestYear.Core.Obtainability;

namespace TheLongestYear.Tests;

public class ObtainabilityMadeTests
{
    private static readonly Dictionary<string, FestivalDates> NoFestivals = new();

    private static ObtainabilityModel Snapshot(params (string Id, WeekMask Weeks, Reliability R)[] rows)
        => new(rows.GroupBy(r => r.Id).ToDictionary(
            g => g.Key,
            g => (IReadOnlyList<ObtainSource>)g.Select(r => new ObtainSource(SourceKind.Forage, DayTable.InWeeks(r.Weeks), r.R, ObtainConditions.None, "test")).ToList()));

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
        Assert.Equal(WeekMask.Of(4), MadeSources.ShiftByDays(WeekMask.Of(3), 7));
        Assert.Equal("3-4", MadeSources.ShiftByDays(WeekMask.Of(3), 3).ToString());
        Assert.Equal(WeekMask.None, MadeSources.ShiftByDays(WeekMask.Of(16), 7));
    }

    [Fact(Skip = "phase 2 task 4/5 rewrites this to the start-day meaning")]
    public void A_keg_makes_wine_the_week_after_its_fruit()
    {
        var snapshot = Snapshot(("(O)613", WeekMask.ForSeason(Season.Fall), Reliability.Dependable),
                                ("(O)254", WeekMask.ForSeason(Season.Summer), Reliability.Dependable));
        var rows = new[] { Rule("(BC)12", null, new[] { "category_fruits" }, 10000, -1, new MachineOutput("FLAVORED_ITEM Wine DROP_IN_ID", null, null)) };
        var wine = MadeSources.Machines(rows, Objects, snapshot, NoFestivals).Single();
        Assert.Equal("(O)348", wine.ItemId);
        Assert.Equal("6-13", wine.Source.Lands.ToString());
        Assert.Contains("machine:(BC)12", wine.Source.Conditions.Requires);
    }

    [Fact]
    public void A_trigger_with_an_id_and_tags_needs_both()
    {
        var snapshot = Snapshot(("(O)613", WeekMask.All, Reliability.Dependable));
        var rows = new[] { Rule("(BC)X", "(O)613", new[] { "category_vegetable" }, 60, -1, new MachineOutput("(O)900", null, null)) };
        Assert.Empty(MadeSources.Machines(rows, Objects, snapshot, NoFestivals));
    }

    [Fact(Skip = "phase 2 task 4/5 rewrites this to the start-day meaning")]
    public void Drop_in_outputs_the_input_and_output_methods_are_unresolved()
    {
        var snapshot = Snapshot(("(O)142", WeekMask.Of(9), Reliability.Dependable));
        var dropIn = MadeSources.Machines(new[] { Rule("(BC)Y", null, new[] { "fish_carp" }, 0, 1, new MachineOutput("DROP_IN", null, null)) },
            Objects, snapshot, NoFestivals).Single();
        Assert.Equal("(O)142", dropIn.ItemId);
        Assert.Equal("9-10", dropIn.Source.Lands.ToString());

        var method = MadeSources.Machines(new[] { Rule("(BC)25", null, new[] { "fish_carp" }, 0, 1, new MachineOutput(null, null, "Object.OutputSeedMaker")) },
            Objects, snapshot, NoFestivals).Single();
        Assert.StartsWith(ItemQueries.UnresolvedPrefix, method.ItemId);
        Assert.True(method.Source.Conditions.Unresolved);
    }

    [Fact]
    public void Output_conditions_narrow_and_several_outputs_are_chance()
    {
        var snapshot = Snapshot(("(O)24", WeekMask.All, Reliability.Dependable));
        var rows = new[] { Rule("(BC)Z", "(O)24", new string[0], 0, 0,
            new MachineOutput("(O)901", "SEASON Summer", null), new MachineOutput("(O)902", null, null)) };
        var list = MadeSources.Machines(rows, Objects, snapshot, NoFestivals).ToList();
        var summer = list.Single(x => x.ItemId == "(O)901").Source;
        Assert.Equal(DayTable.InWeeks(WeekMask.ForSeason(Season.Summer)), summer.Lands);
        Assert.All(list, x => Assert.Equal(Reliability.Chance, x.Source.Reliability));
    }

    [Fact]
    public void First_valid_outputs_are_tried_in_order()
    {
        var snapshot = Snapshot(("(O)24", WeekMask.All, Reliability.Dependable));
        var rows = new[] { new MachineRow("(BC)F", "(O)24", new string[0], null, new[]
            { new MachineOutput("(O)901", "SEASON Summer", null), new MachineOutput("(O)902", null, null) },
            0, 0, UseFirstValidOutput: true) };
        var list = MadeSources.Machines(rows, Objects, snapshot, NoFestivals).ToList();
        var first = list.Single(x => x.ItemId == "(O)901").Source;
        Assert.Equal(DayTable.InWeeks(WeekMask.ForSeason(Season.Summer)), first.Lands);
        Assert.Equal(Reliability.Dependable, first.Reliability);
        var second = list.Single(x => x.ItemId == "(O)902").Source;
        Assert.Equal(DayTable.InWeeks(WeekMask.All.Except(WeekMask.ForSeason(Season.Summer))), second.Lands);   // never in summer
        Assert.Equal(Reliability.Dependable, second.Reliability);
    }

    [Fact(Skip = "phase 2 task 4/5 rewrites this to the start-day meaning")]
    public void A_recipe_needs_every_ingredient_in_the_same_week_and_a_shop_recipe_once_learned()
    {
        var snapshot = Snapshot(("(O)24", WeekMask.ForSeason(Season.Spring), Reliability.Dependable),
                                ("(O)613", WeekMask.Range(3, 10), Reliability.Chance));
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
        var dish = list.Single(x => x.ItemId == "(O)900").Source;
        Assert.Equal("3-4", dish.Lands.ToString());
        Assert.Equal(Reliability.Chance, dish.Reliability);
        Assert.Equal("3-4", list.Single(x => x.ItemId == "(O)901").Source.Lands.ToString());
        var craft = list.Single(x => x.ItemId == "(BC)902").Source;
        Assert.Equal(SourceKind.Crafting, craft.Kind);
        Assert.Equal("Farming", craft.Conditions.Skill);
        Assert.Equal(3, craft.Conditions.SkillLevel);
        Assert.DoesNotContain(list, x => x.ItemId == "(O)903");   // melon is not obtainable in spring and nothing is stored
        Assert.False(list.Single(x => x.ItemId == "(O)901").Source.Conditions.Unresolved);   // "none", but a shop teaches it
        Assert.True(list.Single(x => x.ItemId == "(O)904").Source.Conditions.Unresolved);    // "none" and no shop: taught somewhere unknown
        Assert.Equal(Reliability.Chance, list.Single(x => x.ItemId == "(O)905").Source.Reliability);
        Assert.Equal(Reliability.Chance, list.Single(x => x.ItemId == "(O)906").Source.Reliability);
    }

    [Fact(Skip = "phase 2 task 4/5 rewrites this to the start-day meaning")]
    public void Ponds_use_only_the_lowest_precedence_match_and_carry_forward()
    {
        var snapshot = Snapshot(("(O)142", WeekMask.Of(5), Reliability.Dependable));
        var ponds = new[]
        {
            new PondRow("Generic", new[] { "fish_pond" }, 10, new[] { new PondProduct("(O)900", 1, 1.0, null) }),
            new PondRow("Carp", new[] { "fish_carp" }, 0, new[] { new PondProduct("(O)812", 1, 0.5, null), new PondProduct("(O)901", 5, 1.0, "SEASON Winter") }),
        };
        var list = MadeSources.Ponds(ponds, Objects, snapshot, NoFestivals).ToList();
        Assert.DoesNotContain(list, x => x.ItemId == "(O)900");
        var roe = list.Single(x => x.ItemId == "(O)812").Source;
        Assert.Equal("5-16", roe.Lands.ToString());
        Assert.Equal(Reliability.Chance, roe.Reliability);
        Assert.Equal(DayTable.InWeeks(WeekMask.ForSeason(Season.Winter)), list.Single(x => x.ItemId == "(O)901").Source.Lands);
    }

    [Fact]
    public void Animals_tappers_and_geodes_respect_their_conditions()
    {
        var animals = MadeSources.Animals(new[]
        {
            new AnimalRow("White Chicken", "Coop", 800,
                new[] { new AnimalProduce("(O)176", null, 0) },
                new[] { new AnimalProduce("(O)174", "SEASON Spring", 200) }),
        }, NoFestivals).ToList();
        Assert.Equal(DayTable.Always, animals.Single(a => a.ItemId == "(O)176").Source.Lands);
        var large = animals.Single(a => a.ItemId == "(O)174").Source;
        Assert.Equal(DayTable.InWeeks(WeekMask.ForSeason(Season.Spring)), large.Lands);
        Assert.Contains("friendship:White Chicken 200", large.Conditions.Requires);

        var taps = MadeSources.Tappers(new[] { new TapRow("7", "(O)422", 4, Season.Fall, 0.9, null) }, Objects, NoFestivals).Single().Source;
        Assert.Equal(DayTable.InWeeks(WeekMask.ForSeason(Season.Fall)), taps.Lands);
        Assert.Equal(Reliability.Chance, taps.Reliability);

        var snapshot = Snapshot(("(O)535", WeekMask.Range(2, 3), Reliability.Chance));
        var geode = MadeSources.Geodes(new GeodeDropRow[0], new[] { "(O)535" }, Objects, snapshot, NoFestivals).ToList();
        Assert.Contains(geode, g => g.ItemId == "(O)86" && g.Source.Lands.ToString() == "lands wk2/never/never/never");
        Assert.All(geode, g => Assert.Equal(SourceKind.Geode, g.Source.Kind));
    }
}
