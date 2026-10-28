using System;
using System.Collections.Generic;
using System.Linq;
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

/// <summary>The stamp is a function of the inputs' content: Stardew Valley Expanded's location walk
/// visits its maps in one order on a fresh farm and another on a loaded save, and the same inputs
/// hashed two ways until every dictionary was projected in ordinal key order.</summary>
public class AvailabilityInputsOrderTests
{
    private static Dictionary<string, T> Insert<T>(bool reversed, params (string Key, T Value)[] rows)
    {
        var dictionary = new Dictionary<string, T>(StringComparer.Ordinal);
        foreach ((string key, T value) in reversed ? rows.Reverse() : rows) dictionary[key] = value;
        return dictionary;
    }

    private static AvailabilityInputs Inputs(bool reversed)
    {
        var week = new PlaceWeek(1, 1, 1);
        var later = new PlaceWeek(3, 2, 2);
        var pools = new ItemPools
        {
            FishRows = Insert(reversed,
                ("128", new RawFishEntry("(O)128", false, 50, "600 2000", "both", 4, 0)),
                ("129", new RawFishEntry("(O)129", false, 40, "600 2000", "sunny", 4, 0))),
            LocationWeeks = LocationWeeks.FromReached(Insert(reversed,
                ("Custom_AndyHouse", week), ("LeahHouse", week), ("Custom_MarnieShed", later))),
            ShopWeeks = ShopWeeks.FromWeeks(Insert(reversed, ("(O)472", week), ("(O)24", later))),
            CatchRates = Insert(reversed,
                ("(O)128", new CatchRate(new[] { 0.5 }, 0.5)), ("(O)129", new CatchRate(new[] { 0.25 }, 0.25))),
        };
        var effort = new EffortData
        {
            Objects = Insert(reversed,
                ("24", new RawObjectEntry("Basic", -75, 35, false, new[] { "season_spring" })),
                ("472", new RawObjectEntry("Seeds", -74, 20, false, new[] { "season_spring" }))),
            MachineUnlocks = Insert(reversed, ("(BC)12", "s Farming 8"), ("(BC)13", "default")),
            RecipePrices = Insert(reversed, ("(O)194", 100), ("(O)195", 200)),
            CookingChannel = Insert(reversed, ("Fried Egg", 1), ("Omelet", 2)),
        };
        return AvailabilityInputs.From(pools, effort,
            Insert(reversed, ("(O)24", Season.Spring), ("(O)16", Season.Spring)),
            Insert(reversed, ("(O)24", 2), ("(O)16", 3)),
            Insert(reversed, ("(O)24", 4), ("(O)16", 5)),
            yearTwoCapped: true);
    }

    [Fact]
    public void The_Same_Dictionaries_Inserted_In_Different_Orders_Hash_Equal()
        => Assert.Equal(Inputs(reversed: false).Hash(), Inputs(reversed: true).Hash());

    [Fact]
    public void Every_Dictionary_Is_Projected_In_Ordinal_Key_Order()
    {
        AvailabilityInputs inputs = Inputs(reversed: true);
        static void Sorted<T>(IReadOnlyDictionary<string, T>? dictionary)
        {
            Assert.NotNull(dictionary);
            Assert.Equal(dictionary!.Keys.OrderBy(k => k, StringComparer.Ordinal), dictionary.Keys);
        }
        Sorted(inputs.FishRows);
        Sorted(inputs.LocationWeeks);
        Sorted(inputs.ShopWeeks);
        Sorted(inputs.CatchRates);
        Sorted(inputs.SeasonOverrides);
        Sorted(inputs.EffortOverrides);
        Sorted(inputs.WeekOverrides);
        Sorted(inputs.EffortData!.Objects);
        Sorted(inputs.EffortData.MachineUnlocks);
        Sorted(inputs.EffortData.RecipePrices);
        Sorted(inputs.EffortData.CookingChannel);
    }

    /// <summary>The ordered copy of the effort data carries every member: a member added to
    /// <see cref="EffortData"/> and left out of the copy would silently drop out of the stamp.</summary>
    [Fact]
    public void The_Ordered_Effort_Data_Carries_Every_Member()
    {
        var source = new EffortData
        {
            Objects = new Dictionary<string, RawObjectEntry> { ["24"] = new("Basic", -75, 35, false, new[] { "season_spring" }) },
            MachineUnlocks = new Dictionary<string, string> { ["(BC)12"] = "default" },
            RecipePrices = new Dictionary<string, int> { ["(O)194"] = 100 },
            CookingChannel = new Dictionary<string, int> { ["Fried Egg"] = 1 },
        };
        EffortData ordered = source.Ordered();
        foreach (System.Reflection.PropertyInfo property in typeof(EffortData).GetProperties())
        {
            object? before = property.GetValue(source);
            object? after = property.GetValue(ordered);
            bool dictionary = property.PropertyType.IsGenericType
                && property.PropertyType.GetGenericTypeDefinition() == typeof(IReadOnlyDictionary<,>);
            if (!dictionary)
            {
                Assert.Same(before, after);
                continue;
            }
            // Every dictionary member is filled above, so one the copy forgot reads as empty here.
            int count = ((System.Collections.IEnumerable)before!).Cast<object>().Count();
            Assert.True(count > 0, $"{property.Name}: fill it in this test");
            Assert.Equal(count, ((System.Collections.IEnumerable)after!).Cast<object>().Count());
        }
    }
}
