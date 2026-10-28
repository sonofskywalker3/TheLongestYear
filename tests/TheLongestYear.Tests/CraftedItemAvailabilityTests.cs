using System.Collections.Generic;
using TheLongestYear.Core;
using TheLongestYear.Core.Availability;
using Xunit;

namespace TheLongestYear.Tests;

public class CraftedItemAvailabilityTests
{
    private const string Acorn = "(O)309";
    private const string MapleSeed = "(O)310";
    private const string SeedCookie = "(O)FlashShifter.StardewValleyExpandedCP_Seed_Cookie";
    private const string Unplaceable = "(O)9999";

    private static readonly Dictionary<string, int> Efforts = new() { [Acorn] = 2, [MapleSeed] = 3, [Unplaceable] = 4 };
    private static readonly Dictionary<string, int> Weeks = new() { [Acorn] = 1, [MapleSeed] = 1 };

    private static int? EffortOf(string id) => Efforts.TryGetValue(id, out int e) ? e : null;
    private static int? WeekOf(string id) => Weeks.TryGetValue(id, out int w) ? w : null;

    private static EffortData Data(params CraftingRecipe[] recipes) => new() { CraftingRecipes = recipes };

    private static CraftingRecipe Cookie(string unlock = "s Foraging 3", params RecipeIngredient[] ingredients)
        => new("Seed Cookie", SeedCookie,
            ingredients.Length > 0 ? ingredients : new[] { new RecipeIngredient(Acorn, 1), new RecipeIngredient(MapleSeed, 1) },
            unlock);

    [Fact]
    public void A_seed_cookie_is_dated_from_its_skill_unlock_and_costed_from_its_hardest_ingredient()
    {
        ItemEffort? result = CraftedItemAvailability.Derive(SeedCookie, Data(Cookie()), EffortOf, WeekOf, DifficultyStep.Normal);
        Assert.NotNull(result);
        Assert.Equal(3, result!.EarliestWeek);
        Assert.Equal(3 + 1, result.Effort);
        Assert.Equal($"crafted Seed Cookie: hardest ingredient {MapleSeed} (3) + unlock 's Foraging 3' (1), week 3, effort 4", result.Basis);
    }

    [Fact]
    public void An_unplaced_ingredient_leaves_the_crafted_item_unplaced_and_says_which()
    {
        CraftingRecipe recipe = Cookie("default", new RecipeIngredient(Acorn, 1), new RecipeIngredient(Unplaceable, 1));
        ItemEffort? result = CraftedItemAvailability.Derive(SeedCookie, Data(recipe), EffortOf, WeekOf, DifficultyStep.Normal);
        Assert.NotNull(result);
        Assert.Null(result!.EarliestWeek);
        Assert.Contains($"ingredient {Unplaceable} not in year 1", result.Basis);
    }

    [Fact]
    public void An_unrecognised_ingredient_is_extreme_and_unplaced()
    {
        CraftingRecipe recipe = Cookie("default", new RecipeIngredient("(O)404404", 1));
        ItemEffort? result = CraftedItemAvailability.Derive(SeedCookie, Data(recipe), EffortOf, WeekOf, DifficultyStep.Normal);
        Assert.Null(result!.EarliestWeek);
        Assert.Equal(CookedDishAvailability.ExtremeEffort, result.Effort);
        Assert.Contains("ingredient (O)404404 unrecognised", result.Basis);
    }

    [Fact]
    public void A_default_unlock_is_known_from_week_1()
    {
        ItemEffort? result = CraftedItemAvailability.Derive(SeedCookie, Data(Cookie("default")), EffortOf, WeekOf, DifficultyStep.Normal);
        Assert.Equal(1, result!.EarliestWeek);
        Assert.Equal(3, result.Effort);
        Assert.DoesNotContain("not in year 1", result.Basis);
    }

    [Fact]
    public void A_null_unlock_outside_the_event_table_with_no_price_is_unplaced()
    {
        ItemEffort? result = CraftedItemAvailability.Derive(SeedCookie, Data(Cookie("null")), EffortOf, WeekOf, DifficultyStep.Normal);
        Assert.Null(result!.EarliestWeek);
        Assert.Contains("unlock 'null'", result.Basis);
        Assert.Contains("recipe not in year 1", result.Basis);
    }

    [Fact]
    public void An_event_taught_null_recipe_is_dated_from_the_event_friendship()
    {
        const string WildBait = "(O)774";
        var recipe = new CraftingRecipe("Wild Bait", WildBait,
            new[] { new RecipeIngredient(Acorn, 1), new RecipeIngredient(MapleSeed, 1) }, "null");
        ItemEffort? result = CraftedItemAvailability.Derive(WildBait, Data(recipe), EffortOf, WeekOf, DifficultyStep.Normal);
        int friendshipWeek = UnlockWeeks.ForFriendship("Linus", 4)!.Value;
        Assert.Equal(System.Math.Max(1, friendshipWeek), result!.EarliestWeek);
        Assert.Equal(3 + CookedDishAvailability.UnlockEffort("f Linus 4"), result.Effort);
        Assert.Contains("unlock 'null → f Linus 4'", result.Basis);
    }

    [Fact]
    public void An_unlock_outside_year_1_leaves_it_unplaced()
    {
        ItemEffort? result = CraftedItemAvailability.Derive(SeedCookie, Data(Cookie("l 2")), EffortOf, WeekOf, DifficultyStep.Normal);
        Assert.Null(result!.EarliestWeek);
        Assert.Contains("recipe not in year 1", result.Basis);
    }

    [Fact]
    public void A_recipe_sold_in_a_shop_takes_its_price_week_when_that_is_earlier()
    {
        var data = new EffortData
        {
            CraftingRecipes = new[] { Cookie("s Foraging 9") },
            RecipePrices = new Dictionary<string, int> { [SeedCookie] = 100 },
        };
        ItemEffort? result = CraftedItemAvailability.Derive(SeedCookie, data, EffortOf, WeekOf, DifficultyStep.Normal);
        Assert.Equal(UnlockWeeks.ForCost(100), result!.EarliestWeek);
    }

    [Fact]
    public void A_recipe_with_an_any_wild_seed_ingredient_is_skipped()
    {
        CraftingRecipe recipe = Cookie("default", new RecipeIngredient("-777", 2));
        Assert.Null(CraftedItemAvailability.Derive(SeedCookie, Data(recipe), EffortOf, WeekOf, DifficultyStep.Normal));
    }

    [Fact]
    public void Other_outputs_are_not_claimed()
        => Assert.Null(CraftedItemAvailability.Derive("(O)1", Data(Cookie()), EffortOf, WeekOf, DifficultyStep.Normal));

    [Fact]
    public void The_composer_dates_a_crafted_object_through_its_ingredients()
    {
        var seasonDerived = new Dictionary<string, ItemAvailability>
        {
            [Acorn] = new(Season.Spring, 2, "seed", EarliestWeek: 1),
            [MapleSeed] = new(Season.Spring, 3, "seed", EarliestWeek: 1),
        };
        var composer = new EffortComposer(Data(Cookie()), seasonDerived, hasKitchen: false);
        ItemEffort? result = composer.Derive(SeedCookie);
        Assert.Equal(3, result!.EarliestWeek);
        Assert.StartsWith("crafted Seed Cookie", result.Basis);
    }

    [Fact]
    public void An_output_an_earlier_rule_already_places_cheaper_keeps_that_answer()
    {
        const string Trash = "(O)168";
        var seasonDerived = new Dictionary<string, ItemAvailability>
        {
            [Acorn] = new(Season.Spring, 2, "seed", EarliestWeek: 1),
            [MapleSeed] = new(Season.Spring, 3, "seed", EarliestWeek: 1),
        };
        var recipe = new CraftingRecipe("Trash Maker", Trash,
            new[] { new RecipeIngredient(Acorn, 1), new RecipeIngredient(MapleSeed, 1) }, "default");
        var composer = new EffortComposer(Data(recipe), seasonDerived, hasKitchen: false);
        ItemEffort? result = composer.Derive(Trash);
        Assert.StartsWith("fishing trash", result!.Basis);
    }
}
