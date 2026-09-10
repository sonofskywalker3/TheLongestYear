using System;
using System.Collections.Generic;
using TheLongestYear.Core;
using TheLongestYear.Core.Availability;
using Xunit;

namespace TheLongestYear.Tests;

public class SourceReachabilityTests
{
    private const string IslandShop = "ConstanceSeeds";
    private const string TownShop = "Pierre";
    private const string IslandMap = "TheFishmonger_GI_Inside";
    private const string TownMap = "SeedShop";

    private static readonly IReadOnlySet<string> Unreachable =
        new HashSet<string>(StringComparer.Ordinal) { IslandMap, "IslandSouth" };

    private static readonly RawShopPlacement[] Placements =
    {
        new(IslandShop, IslandMap),
        new(TownShop, TownMap),
    };

    private static readonly IReadOnlySet<string> NoSpawns = new HashSet<string>(StringComparer.Ordinal);

    private static SourceReachability Build(params RawShopListing[] listings) => new(
        Unreachable, listings, Placements,
        Array.Empty<RawCropEntry>(), Array.Empty<RawRecipeEntry>(), NoSpawns);

    [Fact]
    public void Item_sold_only_in_an_unreachable_shop_is_unreachable()
    {
        var rule = Build(new RawShopListing("(O)FishmongerSeed", IslandShop));
        Assert.True(rule.IsUnreachable("(O)FishmongerSeed"));
    }

    [Fact]
    public void Item_sold_somewhere_reachable_too_is_allowed()
    {
        var rule = Build(
            new RawShopListing("(O)FishmongerSeed", IslandShop),
            new RawShopListing("(O)FishmongerSeed", TownShop));
        Assert.False(rule.IsUnreachable("(O)FishmongerSeed"));
    }

    [Fact]
    public void Item_with_no_known_source_is_allowed()
    {
        // The conservative rule (Jeff, 2026-09-10): untraceable means allowed.
        var rule = Build();
        Assert.False(rule.IsUnreachable("(O)16"));
    }

    [Fact]
    public void Shop_with_no_known_location_leaves_the_item_allowed()
    {
        var rule = Build(new RawShopListing("(O)Mystery", "ShopNobodyPlaced"));
        Assert.False(rule.IsUnreachable("(O)Mystery"));
    }

    [Fact]
    public void Recipe_listing_does_not_count_as_selling_the_item()
    {
        // Buying a recipe teaches you to cook it; it does not hand you the dish.
        var rule = Build(new RawShopListing("(O)Dish", TownShop, IsRecipe: true));
        Assert.False(rule.IsUnreachable("(O)Dish"));
    }

    [Fact]
    public void Unqualified_ids_are_normalised()
    {
        var rule = Build(new RawShopListing("FishmongerSeed", IslandShop));
        Assert.True(rule.IsUnreachable("(O)FishmongerSeed"));
    }

    [Fact]
    public void Item_with_a_reachable_spawn_is_never_condemned()
    {
        // A forageable or fishable item that a mod ALSO lists in an island shop must stay.
        var spawns = new HashSet<string>(StringComparer.Ordinal) { "(O)Forageable" };
        var rule = new SourceReachability(
            Unreachable, new[] { new RawShopListing("(O)Forageable", IslandShop) }, Placements,
            Array.Empty<RawCropEntry>(), Array.Empty<RawRecipeEntry>(), spawns);
        Assert.False(rule.IsUnreachable("(O)Forageable"));
    }

    [Fact]
    public void Unreachable_shop_plus_an_unplaced_shop_leaves_the_item_allowed()
    {
        // The Traveling Cart and festival vendors are opened from code, so they have no
        // discoverable placement. An unplaced shop is an unknown, and unknown means allowed.
        var rule = Build(
            new RawShopListing("(O)Seed", IslandShop),
            new RawShopListing("(O)Seed", "ShopNobodyPlaced"));
        Assert.False(rule.IsUnreachable("(O)Seed"));
    }

    [Fact]
    public void Reason_names_the_rule_that_dropped_the_item()
    {
        var rule = Build(new RawShopListing("(O)FishmongerSeed", IslandShop));
        rule.IsUnreachable("(O)FishmongerSeed");
        Assert.Contains("shop", rule.Reasons["(O)FishmongerSeed"], StringComparison.OrdinalIgnoreCase);
    }

    private static SourceReachability WithCrops(
        IReadOnlyList<RawCropEntry> crops, params RawShopListing[] listings) => new(
        Unreachable, listings, Placements, crops, Array.Empty<RawRecipeEntry>(), NoSpawns);

    [Fact]
    public void Crop_whose_seed_is_unreachable_is_unreachable()
    {
        var crops = new[] { new RawCropEntry("(O)FishmongerCrop", new[] { Season.Fall }, null, "(O)FishmongerSeed") };
        var rule = WithCrops(crops, new RawShopListing("(O)FishmongerSeed", IslandShop));
        Assert.True(rule.IsUnreachable("(O)FishmongerCrop"));
        Assert.Contains("seed", rule.Reasons["(O)FishmongerCrop"], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Crop_whose_seed_is_reachable_is_allowed()
    {
        var crops = new[] { new RawCropEntry("(O)Parsnip", new[] { Season.Spring }, null, "(O)ParsnipSeed") };
        var rule = WithCrops(crops, new RawShopListing("(O)ParsnipSeed", TownShop));
        Assert.False(rule.IsUnreachable("(O)Parsnip"));
    }

    [Fact]
    public void Crop_also_sold_somewhere_reachable_is_allowed()
    {
        var crops = new[] { new RawCropEntry("(O)FishmongerCrop", new[] { Season.Fall }, null, "(O)FishmongerSeed") };
        var rule = WithCrops(crops,
            new RawShopListing("(O)FishmongerSeed", IslandShop),
            new RawShopListing("(O)FishmongerCrop", TownShop));
        Assert.False(rule.IsUnreachable("(O)FishmongerCrop"));
    }

    // BOTH orderings, deliberately. A single ordering only catches HALF the scalar bug: with the
    // reachable seed last, a "last row wins" scalar dictionary keeps the reachable one and the test
    // still passes, hiding exactly the defect it was written to catch. One test per ordering means
    // a scalar fails whichever way the rows enumerate.
    [Fact]
    public void A_reachable_alternative_seed_rescues_the_crop_reachable_first()
    {
        var crops = new[]
        {
            new RawCropEntry("(O)Shared", new[] { Season.Spring }, null, "(O)ParsnipSeed"),
            new RawCropEntry("(O)Shared", new[] { Season.Fall }, null, "(O)FishmongerSeed"),
        };
        var rule = WithCrops(crops,
            new RawShopListing("(O)FishmongerSeed", IslandShop),
            new RawShopListing("(O)ParsnipSeed", TownShop));
        Assert.False(rule.IsUnreachable("(O)Shared"));
    }

    [Fact]
    public void A_reachable_alternative_seed_rescues_the_crop_reachable_last()
    {
        var crops = new[]
        {
            new RawCropEntry("(O)Shared", new[] { Season.Fall }, null, "(O)FishmongerSeed"),
            new RawCropEntry("(O)Shared", new[] { Season.Spring }, null, "(O)ParsnipSeed"),
        };
        var rule = WithCrops(crops,
            new RawShopListing("(O)FishmongerSeed", IslandShop),
            new RawShopListing("(O)ParsnipSeed", TownShop));
        Assert.False(rule.IsUnreachable("(O)Shared"));
    }

    [Fact]
    public void Crop_with_no_recorded_seed_is_allowed()
    {
        var crops = new[] { new RawCropEntry("(O)MysteryCrop", new[] { Season.Spring }) };
        var rule = WithCrops(crops);
        Assert.False(rule.IsUnreachable("(O)MysteryCrop"));
    }

    private static SourceReachability WithRecipes(
        IReadOnlyList<RawRecipeEntry> recipes, params RawShopListing[] listings) => new(
        Unreachable, listings, Placements, Array.Empty<RawCropEntry>(), recipes, NoSpawns);

    [Fact]
    public void Dish_with_an_unreachable_ingredient_is_unreachable()
    {
        var recipes = new[] { new RawRecipeEntry("(O)Sauce", new[] { "(O)FishmongerSeed", "(O)246" }, "default") };
        var rule = WithRecipes(recipes, new RawShopListing("(O)FishmongerSeed", IslandShop));
        Assert.True(rule.IsUnreachable("(O)Sauce"));
        Assert.Contains("ingredient", rule.Reasons["(O)Sauce"], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Dish_with_vanilla_ingredients_but_an_unlearnable_recipe_is_unreachable()
    {
        // Baked Red Snapper Curry: Red Snapper, Potato, Hot Pepper, all vanilla. Only Constance
        // teaches it, and its unlock field is "none".
        var recipes = new[] { new RawRecipeEntry("(O)Curry", new[] { "(O)150", "(O)192", "(O)260" }, "none") };
        var rule = WithRecipes(recipes, new RawShopListing("(O)Curry", IslandShop, IsRecipe: true));
        Assert.True(rule.IsUnreachable("(O)Curry"));
        Assert.Contains("recipe", rule.Reasons["(O)Curry"], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Dish_taught_by_a_reachable_shop_is_allowed()
    {
        var recipes = new[] { new RawRecipeEntry("(O)Curry", new[] { "(O)150" }, "none") };
        var rule = WithRecipes(recipes, new RawShopListing("(O)Curry", TownShop, IsRecipe: true));
        Assert.False(rule.IsUnreachable("(O)Curry"));
    }

    [Theory]
    [InlineData("default")]
    [InlineData("s Farming 3")]
    [InlineData("l")]
    [InlineData("f Robin 7")]
    public void Dish_with_a_normal_unlock_route_is_allowed(string unlock)
    {
        var recipes = new[] { new RawRecipeEntry("(O)Dish", new[] { "(O)150" }, unlock) };
        var rule = WithRecipes(recipes);
        Assert.False(rule.IsUnreachable("(O)Dish"));
    }

    // BOTH orderings, for the same reason as the seed tests: with the cookable recipe last, a
    // "last row wins" scalar dictionary keeps it and the test passes despite the bug.
    [Fact]
    public void A_reachable_alternative_recipe_rescues_the_dish_cookable_first()
    {
        var recipes = new[]
        {
            new RawRecipeEntry("(O)Dish", new[] { "(O)150" }, "s Farming 3"),
            new RawRecipeEntry("(O)Dish", new[] { "(O)FishmongerSeed" }, "none"),
        };
        var rule = WithRecipes(recipes, new RawShopListing("(O)FishmongerSeed", IslandShop));
        Assert.False(rule.IsUnreachable("(O)Dish"));
    }

    [Fact]
    public void A_reachable_alternative_recipe_rescues_the_dish_cookable_last()
    {
        var recipes = new[]
        {
            new RawRecipeEntry("(O)Dish", new[] { "(O)FishmongerSeed" }, "none"),
            new RawRecipeEntry("(O)Dish", new[] { "(O)150" }, "s Farming 3"),
        };
        var rule = WithRecipes(recipes, new RawShopListing("(O)FishmongerSeed", IslandShop));
        Assert.False(rule.IsUnreachable("(O)Dish"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("null")]
    public void Dish_with_an_empty_or_null_unlock_field_is_allowed(string unlock)
    {
        // Vanilla Cookies carries the literal string "null" and is taught by Evelyn's event.
        var recipes = new[] { new RawRecipeEntry("(O)Cookies", new[] { "(O)150" }, unlock) };
        var rule = WithRecipes(recipes);
        Assert.False(rule.IsUnreachable("(O)Cookies"));
    }

    [Fact]
    public void Recipe_cycles_terminate_and_do_not_condemn()
    {
        var recipes = new[]
        {
            new RawRecipeEntry("(O)A", new[] { "(O)B" }, "default"),
            new RawRecipeEntry("(O)B", new[] { "(O)A" }, "default"),
        };
        var rule = WithRecipes(recipes);
        Assert.False(rule.IsUnreachable("(O)A"));
    }
}
