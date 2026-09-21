using System.Linq;
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

    // --- The Wednesday preview window (Jeff, 2026-09-21) -----------------------------------
    // Sneak Peek airs year-2 episodes in the Wednesday rerun slot, showing the episode of the
    // Sunday just gone. These three pin the consequence: weeks 1 to 15 have such a Wednesday,
    // week 16 does not, so episode 32 has no route and its dish is banned from the pools.

    private static EffortData LastEpisodeData() => new()
    {
        CookingChannel = new Dictionary<string, int> { ["Bruschetta"] = 31, ["Shrimp Cocktail"] = 32 },
        RecipePrices = new Dictionary<string, int>(),
    };

    [Fact]
    public void The_last_reachable_year_2_episode_is_placed_at_its_wednesday_week()
        => Assert.Equal(15, CookedDishAvailability.RecipeWeek(
            new RawCookingRecipe("Bruschetta", new string[0], "(O)618", "l 100"), LastEpisodeData(), DifficultyStep.Normal));

    [Fact]
    public void Episode_32_has_no_route_because_no_wednesday_follows_winter_28()
        => Assert.Null(CookedDishAvailability.RecipeWeek(
            new RawCookingRecipe("Shrimp Cocktail", new string[0], "(O)733", "l 100"), LastEpisodeData(), DifficultyStep.Normal));

    [Fact]
    public void Shrimp_cocktail_is_banned_from_the_pools()
        => Assert.Contains("(O)733", ItemPoolBuilder.BuiltInExcludedItemIds);

    /// <summary>The arithmetic the patch relies on, stated once: a Wednesday is day 3, 10, 17 or
    /// 24 of a season, the game's week index is DaysPlayed / 7, and vanilla hides the channel for
    /// the first week of play. Weeks 1 to 15 each get a Wednesday; week 16 never does.</summary>
    [Fact]
    public void Wednesdays_cover_every_week_but_the_last()
    {
        var weeks = new SortedSet<int>();
        for (int day = 1; day <= Calendar.DaysPerYear; day++)
        {
            if (day % 7 != 3) continue;      // Wednesday: day 1 of a season is a Monday.
            if (day <= 7) continue;          // Vanilla: no rerun channel before DaysPlayed > 7.
            weeks.Add(day / 7);
        }
        Assert.Equal(Enumerable.Range(1, AvailabilityWeeks.YearTwoLastReachableEpisode - AvailabilityWeeks.YearOneEpisodes), weeks);
        Assert.DoesNotContain(16, weeks);
    }

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
