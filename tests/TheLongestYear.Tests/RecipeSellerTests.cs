using System.Collections.Generic;
using TheLongestYear.Core;
using TheLongestYear.Core.Availability;
using TheLongestYear.Core.Lexicon;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>A recipe a shop sells is dated by that shop as well as its price: a row at a shop the walk
/// never places (a festival stall, an unreached map) or a flagged shop teaches nothing in year 1, and a
/// row at a placed shop is learned no earlier than the week the shop opens.</summary>
public class RecipeSellerTests
{
    private const string Crafted = "(O)333";
    private const string Dish = "(O)9746";
    private const string Stall = "Festival_SpiritsEve_Pierre";
    private const string Krobus = "ShadowShop";
    private const string Robin = "Carpenter";
    private static readonly PlaceWeek SewerWeek = new(7, 7, 7);
    private static readonly PlaceWeek WeekTwo = new(2, 2, 2);

    private static ShopWeeks Placed(params (string Shop, PlaceWeek Week)[] shops)
    {
        var map = new Dictionary<string, PlaceWeek>();
        foreach ((string shop, PlaceWeek week) in shops) map[shop] = week;
        return ShopWeeks.FromWeeks(new Dictionary<string, PlaceWeek>(), null, map);
    }

    private static EffortData Data(string outputId, params RawRecipeShopRow[] rows)
    {
        var prices = new Dictionary<string, int>();
        foreach (RawRecipeShopRow row in rows)
            prices[row.ItemId] = prices.TryGetValue(row.ItemId, out int p) ? System.Math.Min(p, row.Price) : row.Price;
        return new EffortData
        {
            CraftingRecipes = new[] { new CraftingRecipe("Crystal Floor", Crafted, new[] { new RecipeIngredient("(O)338", 1) }, "l 0") },
            CookingRecipes = new[] { new RawCookingRecipe("Festival Dish", new string[0], Dish, "l 100") },
            RecipePrices = prices,
            RecipeShopRows = rows,
        };
    }

    [Fact]
    public void A_crafting_recipe_sold_only_at_a_festival_stall_is_not_placed_by_its_price()
        => Assert.Null(CookedDishAvailability.CraftingRecipeWeek("l 0", Crafted,
            Data(Crafted, new RawRecipeShopRow(Crafted, Stall, 500)), DifficultyStep.Normal, Placed()));

    [Fact]
    public void A_crafting_recipe_at_a_placed_shop_waits_for_the_shop()
        => Assert.Equal(7, CookedDishAvailability.CraftingRecipeWeek("l 0", Crafted,
            Data(Crafted, new RawRecipeShopRow(Crafted, Krobus, 500)), DifficultyStep.Normal, Placed((Krobus, SewerWeek))));

    [Fact]
    public void A_crafting_recipe_at_a_placed_shop_waits_for_its_price()
        => Assert.Equal(UnlockWeeks.ForCost(30000), CookedDishAvailability.CraftingRecipeWeek("l 0", Crafted,
            Data(Crafted, new RawRecipeShopRow(Crafted, Robin, 30000)), DifficultyStep.Normal, Placed((Robin, WeekTwo))));

    [Fact]
    public void A_cheaper_row_at_an_unplaced_shop_loses_to_a_placed_one()
        => Assert.Equal(7, CookedDishAvailability.CraftingRecipeWeek("l 0", Crafted,
            Data(Crafted, new RawRecipeShopRow(Crafted, Stall, 100), new RawRecipeShopRow(Crafted, Krobus, 500)),
            DifficultyStep.Normal, Placed((Krobus, SewerWeek))));

    [Fact]
    public void The_earliest_placed_row_wins()
        => Assert.Equal(2, CookedDishAvailability.CraftingRecipeWeek("l 0", Crafted,
            Data(Crafted, new RawRecipeShopRow(Crafted, Krobus, 500), new RawRecipeShopRow(Crafted, Robin, 2000)),
            DifficultyStep.Normal, Placed((Krobus, SewerWeek), (Robin, WeekTwo))));

    [Fact]
    public void Without_the_shop_weeks_no_row_places()
        => Assert.Null(CookedDishAvailability.CraftingRecipeWeek("l 0", Crafted,
            Data(Crafted, new RawRecipeShopRow(Crafted, Krobus, 500)), DifficultyStep.Normal, shopWeeks: null));

    [Fact]
    public void A_dish_recipe_sold_only_at_a_festival_stall_is_not_placed_by_its_price()
    {
        EffortData data = Data(Dish, new RawRecipeShopRow(Dish, Stall, 2000));
        Assert.Null(CookedDishAvailability.RecipeWeek(data.CookingRecipes[0], data, DifficultyStep.Normal, Placed()));
    }

    [Fact]
    public void A_dish_recipe_at_a_placed_shop_takes_the_later_of_price_and_shop()
    {
        EffortData data = Data(Dish, new RawRecipeShopRow(Dish, Krobus, 2000));
        Assert.Equal(7, CookedDishAvailability.RecipeWeek(data.CookingRecipes[0], data, DifficultyStep.Normal, Placed((Krobus, SewerWeek))));
    }

    [Fact]
    public void The_composer_reads_the_shop_weeks()
    {
        EffortData data = Data(Crafted, new RawRecipeShopRow(Crafted, Stall, 500));
        var seasonDerived = new Dictionary<string, ItemAvailability> { ["(O)338"] = new(Season.Spring, 1, "ore", EarliestWeek: 1) };
        var composer = new EffortComposer(data, seasonDerived, hasKitchen: true, shopWeeks: Placed());
        Assert.Null(composer.Derive(Crafted)!.EarliestWeek);
        var placed = new EffortComposer(data, seasonDerived, hasKitchen: true, shopWeeks: Placed((Stall, WeekTwo)));
        Assert.Equal(2, placed.Derive(Crafted)!.EarliestWeek);
    }

    // ---- ShopWeeks records which shops it placed -------------------------------------------

    private static ShopWeeks Built(IReadOnlySet<string>? flagged = null)
        => ShopWeeks.Build(
            new[] { new RawShopListing("(O)769", Krobus), new RawShopListing("(O)333", Krobus, IsRecipe: true) },
            new[] { new RawShopPlacement(Krobus, "Sewer"), new RawShopPlacement("Custom_Island", "IslandSouth") },
            LocationWeeks.Build(new[] { new RawLocationLink("Farm", "Town"), new RawLocationLink("Town", "Sewer") }, _ => false),
            flagged);

    [Fact]
    public void A_placed_shop_is_recorded()
    {
        Assert.True(Built().TryGetShop(Krobus, out PlaceWeek week));
        Assert.Equal(Built().Weeks["(O)769"], week);
    }

    [Fact]
    public void An_unplaced_shop_is_not_recorded()
        => Assert.False(Built().TryGetShop("Custom_Island", out _));

    [Fact]
    public void A_flagged_shop_is_not_recorded()
        => Assert.False(Built(new HashSet<string> { Krobus }).TryGetShop(Krobus, out _));

    [Fact]
    public void The_placed_shops_are_in_the_stamp_and_come_back_from_it()
    {
        AvailabilityInputs inputs = AvailabilityInputs.From(new ItemPools { ShopWeeks = Built() }, null, null, null, null, false);
        Assert.True(inputs.ShopPlacements!.ContainsKey(Krobus));
        Assert.True(inputs.Pools().ShopWeeks!.TryGetShop(Krobus, out _));
        AvailabilityInputs flagged = AvailabilityInputs.From(
            new ItemPools { ShopWeeks = Built(new HashSet<string> { Krobus }) }, null, null, null, null, false);
        Assert.NotEqual(inputs.Hash(), flagged.Hash());
    }
}
