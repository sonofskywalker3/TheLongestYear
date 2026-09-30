using System;
using System.Collections.Generic;
using TheLongestYear.Core;
using TheLongestYear.Core.Availability;
using Xunit;

namespace TheLongestYear.Tests;

public class DishAskBasisTests
{
    private static RawObjectEntry Obj(int category, string name) => new("Basic", category, 10, false, new string[0], name);

    // Fixture: Fried Egg (any egg, default, week 6), Plum Pudding (2 Wild Plum + Flour + Sugar,
    // week 13), Salad (shop 220g, week 1), Tortilla dish used by Fish Taco (dish ingredient).
    private static EffortData Data() => new()
    {
        Objects = new Dictionary<string, RawObjectEntry>
        {
            ["176"] = Obj(-5, "Egg"), ["406"] = Obj(-81, "Wild Plum"), ["246"] = Obj(-25, "Wheat Flour"),
            ["245"] = Obj(-25, "Sugar"), ["270"] = Obj(-75, "Corn"), ["130"] = Obj(-4, "Tuna"), ["229"] = Obj(-7, "Tortilla"),
        },
        CookingRecipes = new List<RawCookingRecipe>
        {
            new("Fried Egg", new[] { "-5" }, "(O)194", "default"),
            new("Plum Pudding", new[] { "(O)406", "(O)246", "(O)245" }, "(O)604", "l 26", new[] { 2, 1, 1 }),
            new("Salad", new[] { "(O)20", "(O)22", "(O)419" }, "(O)196", "f Emily 3"),
            new("Tortilla", new[] { "(O)270" }, "(O)229", "l 23"),
            new("Fish Taco", new[] { "(O)130", "(O)229" }, "(O)213", "f Linus 7"),
        },
    };

    private static ItemAvailability At(int week) => new(AvailabilityWeeks.SeasonOf(week), 3, "t", EarliestWeek: week, HardWeek: week);

    private static IReadOnlyDictionary<string, double[]> Build()
        => DishAskBasis.Build(Data(),
            id => id switch { "(O)194" => At(6), "(O)604" => At(13), "(O)196" => At(1), "(O)213" => At(8), "(O)229" => At(6), _ => null },
            (id, s) => id switch { "(O)176" => 28, "(O)406" => 40, "(O)246" => 40, "(O)245" => 40, "(O)270" => 99, "(O)130" => 16.9, _ => null },
            _ => 2);

    [Fact] public void Nothing_before_the_dish_exists() => Assert.Equal(0, Build()["(O)194"][0]);   // Fried Egg in Spring
    [Fact] public void Scarcest_ingredient_times_share() => Assert.Equal(7, Build()["(O)194"][1]);   // Egg 28 x 0.25
    [Fact] public void Counts_divide() => Assert.Equal(5, Build()["(O)604"][3]);                    // Wild Plum 40 / 2 x 0.25
    [Fact] public void Shop_dishes_use_the_price() => Assert.Equal(25, Build()["(O)196"][0]);       // 6000/220 = 27, cap 25
    [Fact] public void A_dish_ingredient_uses_its_own_row() => Assert.True(Build()["(O)213"][1] >= 1);
    [Fact] public void Unplaced_dish_has_no_row() => Assert.False(Build().ContainsKey("(O)999"));

    [Fact]
    public void A_dish_ingredient_reads_the_ingredient_dish_row()
    {
        // Tortilla: Corn 99 x 0.25 = 25, capped at 12 (effort 2 + TV unlock 3 = 5, the 4-6 band is 8).
        Assert.Equal(DishAskBasis.CapMidDish, Build()["(O)229"][1]);
        // Fish Taco: min(Tuna 16.9, Tortilla 8) x 0.25 = 2.
        Assert.Equal(2, Build()["(O)213"][1]);
    }

    [Fact]
    public void The_effort_cap_ignores_the_kitchen()
    {
        RawCookingRecipe egg = Data().CookingRecipes[0];
        Assert.Equal(2, CookedDishAvailability.EffortWithoutKitchen(egg, Data(), _ => 2));
        Assert.Equal(CookedDishAvailability.ExtremeEffort, CookedDishAvailability.EffortWithoutKitchen(egg, Data(), _ => null));
    }

    [Fact]
    public void Keep_kitchen_never_moves_the_table()
    {
        var data = new EffortData
        {
            Objects = new Dictionary<string, RawObjectEntry> { ["246"] = Obj(-25, "Wheat Flour"), ["9001"] = Obj(-7, "Flat Bread") },
            CookingRecipes = new List<RawCookingRecipe> { new("Flat Bread", new[] { "(O)246" }, "(O)9001", "default") },
        };
        ItemAvailabilityModel without = ItemAvailabilityBuilder.Build(new ItemPools(), effortData: data, hasKitchen: false);
        ItemAvailabilityModel with = ItemAvailabilityBuilder.Build(new ItemPools(), effortData: data, hasKitchen: true);
        Assert.True(without.DishBases.ContainsKey("(O)9001"));
        Assert.Equal(without.DishBases["(O)9001"], with.DishBases["(O)9001"]);
        Assert.Empty(ItemAvailabilityBuilder.Build(new ItemPools()).DishBases);
    }

    [Fact]
    public void Hand_rows_beat_the_generated_table()
    {
        // Ice Cream hand row 0/25/25/25 must win over any generated value.
        var model = new ItemAvailabilityModel(new Dictionary<string, ItemAvailability>())
            { DishBases = new Dictionary<string, double[]> { ["(O)233"] = new double[] { 9, 9, 9, 9 } } };
        Assert.Null(QuantityAskPass.BasisByDeadline("(O)233", Season.Spring, model));
        Assert.Equal(25, QuantityAskPass.BasisByDeadline("(O)233", Season.Summer, model));
    }

    [Fact]
    public void The_pass_reads_the_dish_table_by_deadline()
    {
        var model = new ItemAvailabilityModel(new Dictionary<string, ItemAvailability>())
            { DishBases = new Dictionary<string, double[]> { ["(O)194"] = new double[] { 0, 7, 7, 7 } } };
        Assert.Null(QuantityAskPass.BasisByDeadline("(O)194", Season.Spring, model));
        Assert.Equal(7, QuantityAskPass.BasisByDeadline("(O)194", Season.Fall, model));
        Assert.Null(QuantityAskPass.BasisByDeadline("(O)194", Season.Fall));
    }
}
