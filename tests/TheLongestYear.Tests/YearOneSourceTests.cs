using System;
using System.Collections.Generic;
using TheLongestYear.Core;
using TheLongestYear.Core.Availability;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>Nexus post Thrippa, 2026-09-25: Cornucopia's Spring Rose was asked for in a year-1
/// loop. Its starter is sold only by ALL_ITEMS item-query shop lines behind "YEAR 2", which TLY
/// could not read, so the starter looked source-less and was allowed. These pin the rules that
/// read such lines the way any mod might write them.</summary>
public class YearOneSourceTests
{
    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("SEASON spring", true)]
    [InlineData("YEAR 1", true)]
    [InlineData("SEASON spring, YEAR 1 1", true)]
    [InlineData("!YEAR 2", true)]
    [InlineData("!YEAR 2, SEASON winter", true)]
    [InlineData("YEAR 2", false)]
    [InlineData("SEASON spring, YEAR 2", false)]
    [InlineData("year 2", false)]
    [InlineData("YEAR 25, SYNCED_RANDOM day teaset .05", false)]
    [InlineData("YEAR 2 3", false)]
    [InlineData("!YEAR 1", false)]
    [InlineData("ANY \"YEAR 2\" \"SEASON spring\"", true)]   // an OR: only top-level clauses decide
    [InlineData("SEASON spring, ANY \"YEAR 2, SEASON fall\" \"DAY_OF_MONTH 3\"", true)]
    public void Year_one_reads_only_top_level_year_clauses(string? condition, bool allowed)
        => Assert.Equal(allowed, YearOneCondition.Allows(condition));

    [Theory]
    [InlineData("(O)24", ShopItemQueryKind.PlainId)]
    [InlineData("24", ShopItemQueryKind.PlainId)]
    [InlineData("Cornucopia_RoseSpringSeeds", ShopItemQueryKind.PlainId)]
    [InlineData("ALL_ITEMS (O)", ShopItemQueryKind.Resolve)]
    [InlineData("ALL_ITEMS", ShopItemQueryKind.Resolve)]
    [InlineData("FLAVORED_ITEM Wine (O)398", ShopItemQueryKind.Resolve)]
    [InlineData("ALL_ITEMS (F)", ShopItemQueryKind.Skip)]             // not objects
    [InlineData("ALL_ITEMS (O) @isRandomSale", ShopItemQueryKind.Skip)]
    [InlineData("RANDOM_ITEMS (O)", ShopItemQueryKind.Skip)]          // a lottery, not a listing
    [InlineData("RANDOM_ITEMS (O) 2 789 @requirePrice @isRandomSale", ShopItemQueryKind.Skip)]
    [InlineData("DISH_OF_THE_DAY", ShopItemQueryKind.Skip)]
    [InlineData("(BC)211", ShopItemQueryKind.Skip)]
    [InlineData("", ShopItemQueryKind.Skip)]
    public void Item_query_kinds(string itemId, ShopItemQueryKind kind)
        => Assert.Equal(kind, ItemQueryRules.Classify(itemId));

    private const string Town = "SeedShop";
    private static readonly RawShopPlacement[] Placements = { new("Pierre", Town) };

    private static SourceReachability Build(params RawShopListing[] listings) => new(
        new HashSet<string>(StringComparer.Ordinal), listings, Placements,
        new[] { new RawCropEntry("(O)Cornucopia_RoseSpring", new[] { Season.Spring }, null, "Cornucopia_RoseSpringSeeds") },
        Array.Empty<RawRecipeEntry>(), new HashSet<string>(StringComparer.Ordinal));

    [Fact]
    public void Seed_sold_only_from_year_two_condemns_its_crop()
    {
        var rule = Build(new RawShopListing("(O)Cornucopia_RoseSpringSeeds", "Pierre", LockedAfterYearOne: true));
        Assert.True(rule.IsUnreachable("(O)Cornucopia_RoseSpringSeeds"));
        Assert.True(rule.IsUnreachable("(O)Cornucopia_RoseSpring"));
    }

    [Fact]
    public void A_year_one_listing_beside_the_locked_one_keeps_it()
    {
        var rule = Build(
            new RawShopListing("(O)Cornucopia_RoseSpringSeeds", "Pierre", LockedAfterYearOne: true),
            new RawShopListing("(O)Cornucopia_RoseSpringSeeds", "Pierre"));
        Assert.False(rule.IsUnreachable("(O)Cornucopia_RoseSpring"));
    }

    [Fact]
    public void An_unplaced_seller_still_gives_the_benefit_of_the_doubt()
    {
        var rule = Build(
            new RawShopListing("(O)Cornucopia_RoseSpringSeeds", "Pierre", LockedAfterYearOne: true),
            new RawShopListing("(O)Cornucopia_RoseSpringSeeds", "SomeModShop"));
        Assert.False(rule.IsUnreachable("(O)Cornucopia_RoseSpring"));
    }

    [Fact]
    public void Tly_unlocks_its_own_year_two_seeds()
    {
        Assert.Contains("(O)476", YearTwoCrops.TlyUnlockedSeedIds);
        Assert.Contains("(O)485", YearTwoCrops.TlyUnlockedSeedIds);
        Assert.Contains("(O)489", YearTwoCrops.TlyUnlockedSeedIds);
        Assert.DoesNotContain("(O)273", YearTwoCrops.TlyUnlockedSeedIds);
    }
}
