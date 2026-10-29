using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;
using TheLongestYear.Core.Availability;
using TheLongestYear.Core.Lexicon;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>A layer's daily shop pick (Cornucopia's exclusive cart seeds, an id Content Patcher rolls per
/// save and day) is listed in full for the shop weeks, so the availability stamp does not depend on
/// which id rolled today.</summary>
public class RandomShopPicksTests
{
    private const string Cond = "true, SYNCED_RANDOM day cornucopia_exclusive_seeds 0.8";

    private static readonly RandomShopItem Exclusive = new("Traveler", Cond,
        new[] { "(O)Cornucopia_Soybeans", "(O)Cornucopia_PeppercornSeeds", "(O)Cornucopia_VanillaSeeds" });

    private static ShopWeeks Weeks(IReadOnlyList<RawShopListing> listings)
        => ShopWeeks.Build(listings, new[] { new RawShopPlacement("TownShop", "Town") },
            LocationWeeks.Build(new[] { new RawLocationLink("Farm", "Town") }, _ => false));

    [Fact]
    public void A_Rolled_Listing_Becomes_The_Whole_Pick_Marked_Random()
    {
        var listed = RandomShopPicks.Expand(
            new[] { new RawShopListing("(O)Cornucopia_VanillaSeeds", "Traveler", Condition: Cond) }, new[] { Exclusive });
        Assert.Equal(Exclusive.ItemIds, listed.Select(l => l.ItemId));
        Assert.All(listed, l => Assert.True(l.RandomPick));
        Assert.All(listed, l => Assert.Equal("Traveler", l.ShopId));
    }

    [Fact]
    public void The_Pick_Is_Listed_Once_When_Two_Lines_Rolled_From_It()
    {
        var listed = RandomShopPicks.Expand(new[]
        {
            new RawShopListing("(O)Cornucopia_VanillaSeeds", "Traveler", Condition: Cond),
            new RawShopListing("(O)Cornucopia_Soybeans", "Traveler", Condition: Cond),
        }, new[] { Exclusive });
        Assert.Equal(Exclusive.ItemIds, listed.Select(l => l.ItemId));
    }

    [Fact]
    public void Another_Shop_Or_Condition_Or_Item_Is_Left_Alone()
    {
        var unrelated = new[]
        {
            new RawShopListing("(O)Cornucopia_VanillaSeeds", "SeedShop", Condition: Cond),
            new RawShopListing("(O)Cornucopia_VanillaSeeds", "Traveler", Condition: "SYNCED_RANDOM day other 0.5"),
            new RawShopListing("(O)Cornucopia_KiwiSeeds", "Traveler", Condition: Cond),
        };
        Assert.Equal(unrelated, RandomShopPicks.Expand(unrelated, new[] { Exclusive }));
    }

    [Fact]
    public void A_Pick_With_No_Condition_Matches_Any_Condition()
    {
        var any = Exclusive with { Condition = null };
        var listed = RandomShopPicks.Expand(
            new[] { new RawShopListing("Cornucopia_Soybeans", "Traveler", Condition: "whatever") }, new[] { any });
        Assert.Equal(Exclusive.ItemIds, listed.Select(l => l.ItemId));
    }

    [Fact]
    public void No_Picks_Returns_The_Listings_Unchanged()
    {
        var listings = new[] { new RawShopListing("(O)A", "TownShop") };
        Assert.Same(listings, RandomShopPicks.Expand(listings, null));
        Assert.Same(listings, RandomShopPicks.Expand(listings, new RandomShopItem[0]));
    }

    [Fact]
    public void The_Unplaced_Items_Do_Not_Depend_On_Which_Id_Rolled()
    {
        // Peppercorn is also sold at an unplaced shop: whether the cart rolled it decided whether it was unplaced.
        RawShopListing island = new("(O)Cornucopia_PeppercornSeeds", "Custom_IslandShop");
        IReadOnlySet<string> Unplaced(string rolled) => Weeks(RandomShopPicks.Expand(
            new[] { island, new RawShopListing(rolled, "Traveler", Condition: Cond) }, new[] { Exclusive })).UnplacedItems;
        Assert.Equal(Unplaced("(O)Cornucopia_PeppercornSeeds"), Unplaced("(O)Cornucopia_VanillaSeeds"));
        Assert.DoesNotContain("(O)Cornucopia_PeppercornSeeds", Unplaced("(O)Cornucopia_VanillaSeeds"));
    }

    [Fact]
    public void The_Cart_Line_Gives_No_Week_Before_Or_After()
    {
        var rolled = new[] { new RawShopListing("(O)Cornucopia_VanillaSeeds", "Traveler", Condition: Cond) };
        Assert.Empty(Weeks(rolled).Weeks);
        Assert.Empty(Weeks(RandomShopPicks.Expand(rolled, new[] { Exclusive })).Weeks);
    }

    [Fact]
    public void A_Placed_Shop_Line_Replaced_By_A_Pick_Gives_No_Week()
    {
        var pick = new RandomShopItem("TownShop", null, new[] { "(O)A", "(O)B" });
        var rolled = new[] { new RawShopListing("(O)A", "TownShop") };
        Assert.True(Weeks(rolled).TryGet("(O)A", out _));
        ShopWeeks expanded = Weeks(RandomShopPicks.Expand(rolled, new[] { pick }));
        Assert.Empty(expanded.Weeks);
        Assert.Empty(expanded.UnplacedItems);
    }
}
