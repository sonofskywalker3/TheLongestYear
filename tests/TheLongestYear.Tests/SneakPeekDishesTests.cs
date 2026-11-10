using System;
using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;
using TheLongestYear.Core.Availability;
using TheLongestYear.Core.Lexicon;
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
    // Easy (Jeff, 2026-11-10): Easy's own answers leave the year-2 dishes unplaced; a banked recipe
    // places its dish by the lexicon's stored Normal answer, read, never worked out.
    private static AvailabilityAnswers Answers(params (string Id, ItemEffort Answer)[] rows) => new(
        new Dictionary<string, ItemAvailability>(),
        rows.ToDictionary(r => r.Id, r => r.Answer),
        new Dictionary<string, double[]> { [FriedEgg] = new double[] { 4, 4, 4, 4 } },
        new Dictionary<string, int>());

    private static ItemAvailabilityModel EasyModel()
    {
        AvailabilityAnswers easy = Answers(
            (PinkCake, new ItemEffort(3, "dish Pink Cake: recipe not in year 1, week unknown")),
            (Cobbler, new ItemEffort(3, "dish Blackberry Cobbler: recipe not in year 1, week unknown")),
            (FriedEgg, new ItemEffort(1, "dish Fried Egg: recipe week 1", 6, Season.Spring)));
        AvailabilityAnswers normal = Answers(
            (PinkCake, new ItemEffort(3, "dish Pink Cake: recipe week 7, " + CookedDishAvailability.SneakPeekBasisMarker, 7, Season.Summer)),
            (Cobbler, new ItemEffort(3, "dish Blackberry Cobbler: recipe week 10, " + CookedDishAvailability.SneakPeekBasisMarker, 10, Season.Summer)),
            (FriedEgg, new ItemEffort(1, "dish Fried Egg: recipe week 1", 6, Season.Spring)));
        return ItemAvailabilityBuilder.FromAnswers(easy, null, null, WeekMode.Pacing, DifficultyStep.Easy)
            .WithBankableDishes(SneakPeekDishes.Bankable(easy, normal));
    }

    private static PoolItem Dish(string id) => new(id, 100, 1, new List<Season>(), new List<string>());

    [Fact]
    public void An_easy_board_may_ask_for_a_banked_sneak_peek_dish_no_earlier_than_its_episode()
    {
        ItemAvailabilityModel board = EasyModel().WithBankedDishes(new[] { PinkCake });

        Assert.Equal(new[] { FriedEgg, PinkCake },
            BundlePoolRecipes.Placeable(new[] { Dish(FriedEgg), Dish(PinkCake) }, board).Select(p => p.ItemId));
        Assert.Empty(SneakPeekDishes.ExcludedFor(DifficultyStep.Easy, board, new[] { PinkCake }));
        Assert.Equal(7, board.For(PinkCake).Week);
        Season due = BundleDeadlines.For(new[] { FriedEgg, PinkCake }, board)[PinkCake];
        Assert.True(due >= board.For(PinkCake).Gate, $"due {due}, gate {board.For(PinkCake).Gate}");
        Assert.True(due >= Season.Summer);
    }

    [Fact]
    public void An_easy_board_leaves_out_a_sneak_peek_dish_whose_recipe_is_not_banked()
    {
        ItemAvailabilityModel easy = EasyModel();
        ItemAvailabilityModel board = easy.WithBankedDishes(new[] { PinkCake });

        Assert.Equal(new[] { FriedEgg, PinkCake },
            BundlePoolRecipes.Placeable(new[] { Dish(FriedEgg), Dish(PinkCake), Dish(Cobbler) }, board).Select(p => p.ItemId));
        Assert.Same(easy, easy.WithBankedDishes(null));
        Assert.Same(easy, easy.WithBankedDishes(new[] { FriedEgg }));
        Assert.False(easy.IsPlaced(PinkCake));
    }

    [Fact]
    public void Only_easy_has_bankable_dishes_since_every_other_step_already_places_them()
    {
        var placed = (PinkCake, new ItemEffort(3, "dish Pink Cake: recipe week 7, " + CookedDishAvailability.SneakPeekBasisMarker, 7, Season.Summer));
        Assert.Empty(SneakPeekDishes.Bankable(Answers(placed), Answers(placed)));
        Assert.Empty(SneakPeekDishes.Bankable(Answers(placed), null));
        Assert.Equal(new[] { PinkCake, Cobbler }, EasyModel().BankableDishes.Keys.OrderBy(k => k, StringComparer.Ordinal));
    }
}
