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
}
