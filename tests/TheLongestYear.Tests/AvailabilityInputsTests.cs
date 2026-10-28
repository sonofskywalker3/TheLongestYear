using System;
using System.Collections.Generic;
using TheLongestYear.Core;
using TheLongestYear.Core.Availability;
using TheLongestYear.Core.Lexicon;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>The items sold only outside year 1 are part of the availability stamp, and an offline
/// rebuild of the pools carries them back into the shop weeks the crop rule reads.</summary>
public class AvailabilityInputsUnplacedShopItemsTests
{
    private static ShopWeeks Shops(params RawShopListing[] listings)
        => ShopWeeks.Build(listings, new[] { new RawShopPlacement("TownShop", "Town") },
            LocationWeeks.Build(new[] { new RawLocationLink("Farm", "Town") }, _ => false));

    private static AvailabilityInputs InputsFor(ShopWeeks shops)
        => AvailabilityInputs.From(new ItemPools { ShopWeeks = shops }, null, null, null, null, false);

    [Fact]
    public void The_Unplaced_Items_Are_Sorted_And_Null_When_Empty()
    {
        AvailabilityInputs inputs = InputsFor(Shops(
            new RawShopListing("(O)Z", "Custom_IslandShop"), new RawShopListing("(O)A", "Custom_IslandShop")));
        Assert.Equal(new[] { "(O)A", "(O)Z" }, inputs.UnplacedShopItems);
        Assert.Null(InputsFor(Shops(new RawShopListing("(O)A", "TownShop"))).UnplacedShopItems);
    }

    [Fact]
    public void The_Hash_Differs_When_The_Unplaced_Items_Differ()
    {
        AvailabilityInputs with = InputsFor(Shops(
            new RawShopListing("(O)A", "TownShop"), new RawShopListing("(O)833", "Custom_IslandShop")));
        AvailabilityInputs without = InputsFor(Shops(new RawShopListing("(O)A", "TownShop")));
        Assert.Equal(with.ShopWeeks!.Keys, without.ShopWeeks!.Keys);
        Assert.NotEqual(with.Hash(), without.Hash());
    }

    [Fact]
    public void Pools_Restores_The_Unplaced_Items()
    {
        AvailabilityInputs inputs = InputsFor(Shops(new RawShopListing("(O)833", "Custom_IslandShop")));
        Assert.Contains("(O)833", inputs.Pools().ShopWeeks!.UnplacedItems);
    }
}
