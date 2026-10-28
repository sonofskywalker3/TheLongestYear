using System;
using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;
using TheLongestYear.Core.Availability;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>Mod-support work, 2026-10-08: only vanilla's six orchard fruits had a week (a ruled
/// table), so every modded fruit tree (SVE Nectarine and Pear, Cornucopia's Fig, Grapefruit,
/// Lemon, Lime, Pistachio, Durian...) read as unknown. Fixtures copy the live Data/FruitTrees and
/// Data/Shops rows (SVE 1.15.11, Cornucopia 1.6.3).</summary>
public class FruitTreeAvailabilityTests
{
    private const string Nectarine = "(O)FlashShifter.StardewValleyExpandedCP_Nectarine";
    private const string Pear = "(O)FlashShifter.StardewValleyExpandedCP_Pear";
    private const string Fig = "(O)Cornucopia_Fig";
    private const string Grapefruit = "(O)Cornucopia_Grapefruit";
    private const string Durian = "(O)Cornucopia_Durian";

    private static readonly RawFruitTree[] Trees =
    {
        new("FlashShifter.StardewValleyExpandedCP_Nectarine_Sapling", new[] { Nectarine }, new[] { Season.Summer }),
        new("FlashShifter.StardewValleyExpandedCP_Pear_Sapling", new[] { Pear }, new[] { Season.Spring }),
        new("Cornucopia_FigSapling", new[] { Fig }, new[] { Season.Winter }),
        new("Cornucopia_GrapefruitSapling", new[] { Grapefruit }, new[] { Season.Fall }),
        new("Cornucopia_DurianSapling", new[] { Durian }, new[] { Season.Summer }),
        new("630", new[] { "(O)635" }, new[] { Season.Summer }),
    };

    /// <summary>Pierre's shop is in the SeedShop, entered by a door from Town; the island trader
    /// opens on Ginger Island, which the run never enters.</summary>
    private static ShopWeeks Shops()
    {
        LocationWeeks walk = LocationWeeks.Build(
            new[]
            {
                new RawLocationLink("Farm", "BusStop"), new RawLocationLink("BusStop", "Town"),
                new RawLocationLink("Town", "SeedShop"), new RawLocationLink("Town", "Beach"),
                new RawLocationLink("Beach", "IslandSouth"), new RawLocationLink("IslandSouth", "IslandNorth"),
            },
            name => name.Contains("Island"));
        return ShopWeeks.Build(
            new[]
            {
                new RawShopListing("FlashShifter.StardewValleyExpandedCP_Nectarine_Sapling", "SeedShop"),
                new RawShopListing("FlashShifter.StardewValleyExpandedCP_Pear_Sapling", "SeedShop"),
                new RawShopListing("(O)Cornucopia_FigSapling", "SeedShop"),
                new RawShopListing("(O)Cornucopia_GrapefruitSapling", "SeedShop"),
                new RawShopListing("Cornucopia_DurianSapling", "IslandTrade"),
                new RawShopListing("630", "SeedShop"),
            },
            new[] { new RawShopPlacement("SeedShop", "SeedShop") },
            walk);
    }

    [Theory]
    [InlineData(Nectarine, 5)]    // Summer tree from a week-1 sapling, like Orange and Peach
    [InlineData(Grapefruit, 9)]   // Fall tree, like Apple
    [InlineData(Fig, 13)]         // Winter tree: fruits from Winter 1
    public void A_Modded_Tree_Fruits_In_Its_First_Season_After_Maturing(string fruit, int week)
    {
        ItemEffort? effort = FruitTreeAvailability.Derive(fruit, Trees, Shops());
        Assert.NotNull(effort);
        Assert.Equal(week, effort!.EarliestWeek);
        Assert.StartsWith("tree fruit", effort.Basis);
        Assert.False(AvailabilityWeeks.IsJudgementBasis(effort.Basis));
    }

    [Fact]
    public void A_Spring_Tree_Has_No_Year_One_Fruit()
        => Assert.Null(FruitTreeAvailability.Derive(Pear, Trees, Shops()));

    [Fact]
    public void A_Sapling_Only_The_Island_Sells_Places_Nothing()
        => Assert.Null(FruitTreeAvailability.Derive(Durian, Trees, Shops()));

    [Fact]
    public void Vanilla_Orchard_Fruit_Keeps_Its_Ruled_Row()
        => Assert.Null(FruitTreeAvailability.Derive("(O)635", Trees, Shops()));

    [Fact]
    public void Without_Shop_Weeks_Nothing_Is_Placed()
        => Assert.Null(FruitTreeAvailability.Derive(Nectarine, Trees, shopWeeks: null));

    [Fact]
    public void A_Later_Sapling_Pushes_The_Fruit_Later()
    {
        Assert.Equal(9, FruitTreeAvailability.FirstFruitWeek(5, new[] { Season.Summer, Season.Fall }));
        Assert.Equal(5, FruitTreeAvailability.FirstFruitWeek(1, new Season[0]));
        Assert.Null(FruitTreeAvailability.FirstFruitWeek(13, new[] { Season.Winter }));
    }

    [Fact]
    public void The_Model_Places_A_Dish_Made_From_Modded_Fruit()
    {
        const string bread = "(O)FlashShifter.StardewValleyExpandedCP_Nectarine_Fruit_Bread";
        var objects = new Dictionary<string, RawObjectEntry>
        {
            [Nectarine.Substring(3)] = new("Fruit", -79, 150, false, new string[0]),
            [bread.Substring(3)] = new("Cooking", -7, 300, false, new string[0]),
        };
        var data = new EffortData
        {
            Objects = objects,
            FruitTrees = Trees,
            CookingRecipes = new[] { new RawCookingRecipe("Nectarine Fruit Bread", new[] { Nectarine }, bread, "f Claire 3") },
        };
        var pools = new ItemPools { ShopWeeks = Shops() };

        ItemAvailabilityModel model = ItemAvailabilityBuilder.Build(pools, effortData: data);

        Assert.Equal(5, model.For(Nectarine).PacingWeek);
        Assert.True(model.IsPlaced(bread));
    }
}

public class ShopWeeksTests
{
    [Fact]
    public void A_Code_Opened_Vanilla_Shop_Is_Placed_At_Its_Map()
    {
        LocationWeeks walk = LocationWeeks.Build(new[] { new RawLocationLink("Farm", "BusStop") }, _ => false);
        ShopWeeks shops = ShopWeeks.Build(
            new[] { new RawShopListing("FlashShifter.StardewValleyExpandedCP_Gold_Carrot_Seed", "DesertTrade") },
            new RawShopPlacement[0], walk);

        Assert.True(shops.TryGet("(O)FlashShifter.StardewValleyExpandedCP_Gold_Carrot_Seed", out PlaceWeek week));
        Assert.Equal(AvailabilityWeeks.SkullCavernWeek, week.Week);
        Assert.Equal(AvailabilityWeeks.DesertHardWeek, week.Hard);
    }

    [Fact]
    public void Recipes_Year_Two_Lines_And_Unplaced_Shops_Prove_Nothing()
    {
        LocationWeeks walk = LocationWeeks.Build(new[] { new RawLocationLink("Farm", "Town") }, _ => false);
        ShopWeeks shops = ShopWeeks.Build(
            new[]
            {
                new RawShopListing("(O)A", "Town", IsRecipe: true),
                new RawShopListing("(O)B", "Town", LockedAfterYearOne: true),
                new RawShopListing("(O)C", "Traveler"),
                new RawShopListing("(O)D", "ModShop"),
            },
            new[] { new RawShopPlacement("Town", "Town"), new RawShopPlacement("ModShop", "Custom_Nowhere") },
            walk);

        Assert.Empty(shops.Weeks);
    }

    [Fact]
    public void The_Cheapest_In_Time_Shop_Wins()
    {
        LocationWeeks walk = LocationWeeks.Build(
            new[] { new RawLocationLink("Farm", "Town"), new RawLocationLink("Farm", "Forest"), new RawLocationLink("Forest", "Woods") },
            _ => false);
        ShopWeeks shops = ShopWeeks.Build(
            new[] { new RawShopListing("(O)E", "WoodsShop"), new RawShopListing("(O)E", "TownShop") },
            new[] { new RawShopPlacement("WoodsShop", "Woods"), new RawShopPlacement("TownShop", "Town") },
            walk);

        Assert.True(shops.TryGet("(O)E", out PlaceWeek week));
        Assert.Equal(1, week.Week);
    }

    /// <summary>A shop a layer flags as year-2 content (SVE's Joja Emporium vendors) sells nothing
    /// in year 1, even when the walk reaches the map it stands on.</summary>
    [Fact]
    public void A_Flagged_Shop_On_A_Reached_Map_Places_Nothing()
    {
        LocationWeeks walk = LocationWeeks.Build(new[] { new RawLocationLink("Farm", "Town") }, _ => false);
        ShopWeeks shops = ShopWeeks.Build(
            new[] { new RawShopListing("(O)F", "JojaVendor"), new RawShopListing("(O)G", "TownShop") },
            new[] { new RawShopPlacement("JojaVendor", "Town"), new RawShopPlacement("TownShop", "Town") },
            walk,
            new HashSet<string>(StringComparer.Ordinal) { "JojaVendor" });

        Assert.False(shops.TryGet("(O)F", out _));
        Assert.True(shops.TryGet("(O)G", out _));
    }

    /// <summary>A seed listed only at a shop no walk places, or only at a flagged one, is recorded
    /// as sold outside year 1; one also sold at a reached shop is not, and a recipe or year-2 line
    /// is a different fact.</summary>
    [Fact]
    public void Items_Sold_Only_At_Unplaced_Or_Flagged_Shops_Are_Recorded()
    {
        LocationWeeks walk = LocationWeeks.Build(new[] { new RawLocationLink("Farm", "Town") }, _ => false);
        ShopWeeks shops = ShopWeeks.Build(
            new[]
            {
                new RawShopListing("(O)Island", "Custom_IslandShop"),
                new RawShopListing("(O)Joja", "JojaVendor"),
                new RawShopListing("(O)Both", "Custom_IslandShop"),
                new RawShopListing("(O)Both", "TownShop"),
                new RawShopListing("(O)Recipe", "Custom_IslandShop", IsRecipe: true),
                new RawShopListing("(O)Locked", "TownShop", LockedAfterYearOne: true),
                new RawShopListing("Bare", "Custom_IslandShop"),
            },
            new[] { new RawShopPlacement("JojaVendor", "Town"), new RawShopPlacement("TownShop", "Town") },
            walk,
            new HashSet<string>(StringComparer.Ordinal) { "JojaVendor" });

        Assert.Equal(new[] { "(O)Bare", "(O)Island", "(O)Joja" }, shops.UnplacedItems.OrderBy(id => id, StringComparer.Ordinal));
        Assert.True(shops.TryGet("(O)Both", out _));
    }

    /// <summary>A map the forbidden predicate rejects (a layer's year-2 map) is not reached, and
    /// neither is any map reached only through it.</summary>
    [Fact]
    public void A_Forbidden_Map_And_The_Maps_Only_Behind_It_Are_Not_Reached()
    {
        LocationWeeks walk = LocationWeeks.Build(
            new[]
            {
                new RawLocationLink("Farm", "Forest"), new RawLocationLink("Forest", "Custom_Spring"),
                new RawLocationLink("Custom_Spring", "Custom_Cave"), new RawLocationLink("Farm", "Town"),
            },
            name => name == "Custom_Spring");

        Assert.Contains("Forest", walk.Reached.Keys);
        Assert.Contains("Town", walk.Reached.Keys);
        Assert.DoesNotContain("Custom_Spring", walk.Reached.Keys);
        Assert.DoesNotContain("Custom_Cave", walk.Reached.Keys);
    }
}

/// <summary>Mod-support work, 2026-10-08: SVE's Gold Carrot seed is sold only by the Desert
/// Trader, yet the crop rule read its seasons (Spring/Summer/Fall, 6 days) alone and placed it as a
/// week-1 Spring harvest.</summary>
public class CropSeedShopWeekTests
{
    private const string GoldCarrot = "(O)FlashShifter.StardewValleyExpandedCP_Gold_Carrot";
    private const string GoldCarrotSeed = "(O)FlashShifter.StardewValleyExpandedCP_Gold_Carrot_Seed";

    private static ShopWeeks Shops(params RawShopListing[] listings)
        => ShopWeeks.Build(listings, new[] { new RawShopPlacement("SeedShop", "SeedShop") },
            LocationWeeks.Build(new[] { new RawLocationLink("Farm", "Town"), new RawLocationLink("Town", "SeedShop") }, _ => false));

    private static readonly RawCropGrowth[] GoldCarrotCrop =
        { new(GoldCarrot, 6, false, false, new[] { Season.Spring, Season.Summer, Season.Fall }, GoldCarrotSeed) };

    [Fact]
    public void A_Seed_Sold_Only_In_The_Desert_Waits_For_The_Desert()
    {
        ShopWeeks shops = Shops(new RawShopListing(GoldCarrotSeed, "DesertTrade"));

        ItemEffort pacing = CropForageAvailability.DeriveCrop(GoldCarrot, GoldCarrotCrop, WeekMode.Pacing, shops)!;
        Assert.Equal(AvailabilityWeeks.SkullCavernWeek, pacing.EarliestWeek);
        Assert.Equal(AvailabilityWeeks.DesertHardWeek, pacing.HardWeek);
        Assert.Contains("seed sold from week 9", pacing.Basis);
        ItemEffort extreme = CropForageAvailability.DeriveCrop(GoldCarrot, GoldCarrotCrop, WeekMode.HardAll, shops)!;
        Assert.Equal(AvailabilityWeeks.DesertExtremeWeek, extreme.HardWeek);
    }

    [Fact]
    public void A_Seed_From_Pierre_Or_From_Nowhere_Keeps_The_Season_Arithmetic()
    {
        Assert.Equal(1, CropForageAvailability.DeriveCrop(GoldCarrot, GoldCarrotCrop, WeekMode.Pacing,
            Shops(new RawShopListing(GoldCarrotSeed, "SeedShop")))!.EarliestWeek);
        Assert.Equal(1, CropForageAvailability.DeriveCrop(GoldCarrot, GoldCarrotCrop, WeekMode.Pacing, Shops())!.EarliestWeek);
        Assert.Equal(1, CropForageAvailability.DeriveCrop(GoldCarrot, GoldCarrotCrop)!.EarliestWeek);
    }

    [Fact]
    public void A_Spring_Crop_Whose_Seed_Arrives_After_Spring_Is_Not_Placed()
    {
        var crop = new[] { new RawCropGrowth("(O)Mod_Leek", 6, false, false, new[] { Season.Spring }, "(O)Mod_LeekSeed") };
        Assert.Null(CropForageAvailability.DeriveCrop("(O)Mod_Leek", crop, WeekMode.Pacing,
            Shops(new RawShopListing("(O)Mod_LeekSeed", "DesertTrade"))));
    }

    /// <summary>Vanilla Pineapple: its seed is sold only by the Island Trader, so the crop has
    /// no year-1 week, though its seasons alone read as a Summer harvest.</summary>
    [Fact]
    public void A_Seed_Sold_Only_Outside_Year_One_Leaves_The_Crop_Unplaced()
    {
        var pineapple = new[] { new RawCropGrowth("(O)832", 14, true, false, new[] { Season.Summer }, "(O)833") };
        ShopWeeks shops = Shops(new RawShopListing("(O)833", "Custom_IslandShop"));
        Assert.Contains("(O)833", shops.UnplacedItems);

        ItemEffort effort = CropForageAvailability.DeriveCrop("(O)832", pineapple, WeekMode.Pacing, shops)!;
        Assert.Null(effort.EarliestWeek);
        Assert.Null(effort.HardWeek);
        Assert.Equal("crop, seed sold only outside year 1", effort.Basis);

        Assert.NotNull(CropForageAvailability.DeriveCrop("(O)832", pineapple, WeekMode.Pacing, Shops())!.EarliestWeek);
    }

    [Fact]
    public void A_Ruled_Seed_Source_Row_Wins_Over_The_Shop()
    {
        var beet = new[] { new RawCropGrowth("(O)284", 6, false, false, new[] { Season.Fall }, "(O)487") };
        ItemEffort effort = CropForageAvailability.DeriveCrop("(O)284", beet, WeekMode.Pacing,
            Shops(new RawShopListing("(O)487", "DesertTrade")))!;
        Assert.Equal(AvailabilityWeeks.SeedSourceWeeks["(O)284"].Week, effort.EarliestWeek);
        Assert.DoesNotContain("seed sold", effort.Basis);
    }
}
