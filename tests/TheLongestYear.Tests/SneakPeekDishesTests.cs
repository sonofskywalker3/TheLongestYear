using System;
using System.Collections.Generic;
using TheLongestYear.Core;
using TheLongestYear.Core.Availability;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>Sneak-Peek-only dishes on the CC board (Jeff, 2026-11-10, Nijah's report).</summary>
public class SneakPeekDishesTests
{
    private const string PinkCake = "(O)221";
    private const string Cobbler = "(O)611";
    private const string FriedEgg = "(O)194";

    private static ItemAvailabilityModel Model(DifficultyStep step) => new(
        new Dictionary<string, ItemAvailability>(),
        effortDerived: new Dictionary<string, ItemEffort>
        {
            [PinkCake] = new(3, "dish Pink Cake: recipe week 7, " + CookedDishAvailability.SneakPeekBasisMarker, 7, Season.Summer),
            [Cobbler] = new(3, "dish Blackberry Cobbler: recipe week 10, " + CookedDishAvailability.SneakPeekBasisMarker, 10, Season.Summer),
            [FriedEgg] = new(1, "dish Fried Egg: recipe week 1", 6, Season.Spring),
        },
        step: step);

    [Fact]
    public void A_normal_board_leaves_out_every_sneak_peek_dish_whose_recipe_is_not_banked()
        => Assert.Equal(new[] { Cobbler },
            SneakPeekDishes.ExcludedFor(DifficultyStep.Normal, Model(DifficultyStep.Normal), new[] { PinkCake }));

    [Theory]
    [InlineData(DifficultyStep.Hard)]
    [InlineData(DifficultyStep.Extreme)]
    public void Hard_and_extreme_boards_may_ask_for_any_sneak_peek_dish(DifficultyStep step)
        => Assert.Empty(SneakPeekDishes.ExcludedFor(step, Model(step), null));

    [Fact]
    public void Banked_recipes_map_to_the_dishes_they_make()
    {
        var cookingRecipes = new Dictionary<string, string>
        {
            ["Pink Cake"] = "246 1 262 1 245 1 422 1/1 10/221/l 100/",
            ["Fried Egg"] = "-5 1/10 10/194/default/",
            ["Broken"] = "246 1",
        };
        Assert.Equal(new List<string> { FriedEgg, PinkCake },
            SneakPeekDishes.DishIdsFor(new[] { "Pink Cake", "Fried Egg", "Gone Mod Dish", "Broken" }, cookingRecipes));
    }
}
