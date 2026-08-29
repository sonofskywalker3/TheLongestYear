using System.Collections.Generic;
using TheLongestYear.Core;
using TheLongestYear.Core.Availability;
using Xunit;

namespace TheLongestYear.Tests;

public class RecipeTimingTests
{
    private static EffortData Data() => new()
    {
        CookingChannel = new Dictionary<string, int> { ["Stir Fry"] = 1, ["Blackberry Cobbler"] = 26, ["Pizza"] = 17 },
        RecipePrices = new Dictionary<string, int> { ["(O)206"] = 150 },   // Pizza at the Saloon
    };

    [Theory]
    [InlineData("Stir Fry", "l 100", 1)]
    [InlineData("Vegetable Stew", "f Caroline 7", 8)]
    [InlineData("Tom Kha Soup", "f Sandy 7", 9)]
    [InlineData("Farmer's Lunch", "s Farming 3", 3)]
    [InlineData("Fried Egg", "default", 1)]
    [InlineData("Cookies", "null", 5)]
    public void Recipe_weeks(string name, string unlock, int week)
        => Assert.Equal(week, CookedDishAvailability.RecipeWeek(new RawCookingRecipe(name, new string[0], "(O)1", unlock), Data(), DifficultyStep.Normal));

    [Fact]
    public void A_year_2_episode_is_placed_by_the_sneak_peek_boost_on_normal()
        => Assert.Equal(10, CookedDishAvailability.RecipeWeek(
            new RawCookingRecipe("Blackberry Cobbler", new string[0], "(O)611", "l 100"), Data(), DifficultyStep.Normal));

    [Fact]
    public void A_year_2_episode_stays_out_of_year_1_on_easy()
        => Assert.Null(CookedDishAvailability.RecipeWeek(
            new RawCookingRecipe("Blackberry Cobbler", new string[0], "(O)611", "l 100"), Data(), DifficultyStep.Easy));

    [Fact]
    public void A_saloon_recipe_uses_its_price_even_when_its_episode_is_year_2()
        => Assert.Equal(1, CookedDishAvailability.RecipeWeek(new RawCookingRecipe("Pizza", new string[0], "(O)206", "l 20"), Data(), DifficultyStep.Normal));

    [Fact]
    public void A_kent_recipe_is_not_in_year_1()
        => Assert.Null(CookedDishAvailability.RecipeWeek(new RawCookingRecipe("Crispy Bass", new string[0], "(O)214", "f Kent 3"), Data(), DifficultyStep.Normal));

    // Fix round 1 (spec 2026-08-28-obtainable-board-4-boosts): the Sneak Peek note must only
    // appear when the year-2 episode route is what actually won the week - a recipe that is also
    // cheaper to buy in a shop is not a Boost goal, since the player can simply buy it.
    private static EffortData DishData(RawCookingRecipe recipe) => new()
    {
        CookingChannel = new Dictionary<string, int> { ["Blackberry Cobbler"] = 26, ["Pizza"] = 17 },
        RecipePrices = new Dictionary<string, int> { ["(O)206"] = 150 },   // Pizza at the Saloon, week 1
        CookingRecipes = new List<RawCookingRecipe> { recipe },
    };

    [Fact]
    public void A_year_2_episode_with_no_price_route_carries_the_sneak_peek_note()
    {
        var recipe = new RawCookingRecipe("Blackberry Cobbler", new string[0], "(O)611", "l 100");
        ItemEffort? result = CookedDishAvailability.Derive("(O)611", DishData(recipe), _ => null, hasKitchen: true, weekOf: null, DifficultyStep.Normal);
        Assert.Equal(10, result!.EarliestWeek);
        Assert.Contains(CookedDishAvailability.SneakPeekBasisMarker, result.Basis);
    }

    [Fact]
    public void A_year_2_episode_beaten_by_a_cheaper_price_carries_no_sneak_peek_note()
    {
        var recipe = new RawCookingRecipe("Pizza", new string[0], "(O)206", "l 20");
        ItemEffort? result = CookedDishAvailability.Derive("(O)206", DishData(recipe), _ => null, hasKitchen: true, weekOf: null, DifficultyStep.Normal);
        // The recipe itself is week 1 (its price beats the year-2 episode route); Derive's
        // EarliestWeek is still the later of that and the kitchen week (AvailabilityWeeks
        // .KitchenWeek = 6), which RecipeWeek alone does not see.
        Assert.Equal(AvailabilityWeeks.KitchenWeek, result!.EarliestWeek);
        Assert.DoesNotContain(CookedDishAvailability.SneakPeekBasisMarker, result.Basis);
    }
}
