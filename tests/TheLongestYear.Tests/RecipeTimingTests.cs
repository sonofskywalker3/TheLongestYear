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
        => Assert.Equal(week, CookedDishAvailability.RecipeWeek(new RawCookingRecipe(name, new string[0], "(O)1", unlock), Data()));

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
        => Assert.Equal(1, CookedDishAvailability.RecipeWeek(new RawCookingRecipe("Pizza", new string[0], "(O)206", "l 20"), Data()));

    [Fact]
    public void A_kent_recipe_is_not_in_year_1()
        => Assert.Null(CookedDishAvailability.RecipeWeek(new RawCookingRecipe("Crispy Bass", new string[0], "(O)214", "f Kent 3"), Data()));
}
