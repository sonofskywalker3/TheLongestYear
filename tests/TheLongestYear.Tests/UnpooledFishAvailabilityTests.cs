using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;
using TheLongestYear.Core.Availability;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>Mod-support work, 2026-10-08: every Stardew Valley Expanded fish carries
/// ExcludeFromRandomSale, so the pool vet dropped it and the availability model, which only placed
/// pooled fish, left it unknown (week 13). Fixtures copy the live SVE 1.15.11 rows (patch export).</summary>
public class UnpooledFishAvailabilityTests
{
    private const string BullTrout = "(O)FlashShifter.StardewValleyExpandedCP_Bull_Trout";
    private const string BullTroutBare = "FlashShifter.StardewValleyExpandedCP_Bull_Trout";
    private const string HighlandsBass = "(O)FlashShifter.StardewValleyExpandedCP_Highlands_Bass";
    private const string BullTroutRow = "Bull Trout/45/mixed/12/25/600 2600/fall winter/both/690 .4/3/.22/.5/0/false";

    private static RawObjectEntry SveFish() => new("Fish", -4, 185, true, new[] { "fish_river" });

    /// <summary>The farm walks to the Forest and the Mountain, the Forest to SVE's Forest West; the
    /// Highlands have no door from anywhere a player walks (SVE enters them by script).</summary>
    private static LocationWeeks SveWorld() => LocationWeeks.Build(
        new[]
        {
            new RawLocationLink("Farm", "Forest"), new RawLocationLink("Forest", "Custom_ForestWest"),
            new RawLocationLink("Farm", "Backwoods"), new RawLocationLink("Backwoods", "Mountain"),
            new RawLocationLink("Custom_Highlands", "Custom_HighlandsOutpost"),
        },
        name => name.Contains("Island"));

    private static ItemPools Pools(IReadOnlyList<RawSpawnEntry> fish, Dictionary<string, RawObjectEntry> objects,
        LocationWeeks? weeks, Dictionary<string, RawFishEntry>? rows = null)
        => ItemPoolBuilder.Build(
            new List<RawCropEntry>(), objects, new List<RawSpawnEntry>(), fish, new HashSet<string>(),
            new List<RawMonsterDropEntry>(), new List<RawFruitTreeEntry>(), new List<RawGeodeDropEntry>(),
            new BundleGenerationTuning(), fishRows: rows, locationWeeks: weeks);

    private static IReadOnlyList<RawSpawnEntry> BullTroutRows() => new[]
    {
        new RawSpawnEntry(BullTrout, null, "LOCATION_Season Here Fall Winter", "Mountain"),
        new RawSpawnEntry(BullTrout, null, "LOCATION_Season Here Fall Winter", "Forest"),
        new RawSpawnEntry(BullTrout, null, "LOCATION_Season Here Summer Winter", "Custom_ForestWest"),
        new RawSpawnEntry(BullTrout, null, "LOCATION_Season Here Spring", "Custom_Highlands"),
    };

    [Fact]
    public void An_ExcludeFromRandomSale_Fish_Stays_Out_Of_The_Pool_But_Is_Listed_For_Availability()
    {
        ItemPools pools = Pools(BullTroutRows(), new() { [BullTroutBare] = SveFish() }, SveWorld());

        Assert.DoesNotContain(pools.Fish, p => p.ItemId == BullTrout);
        PoolItem trout = Assert.Single(pools.UnpooledFish);
        Assert.Equal(BullTrout, trout.ItemId);
        // The Highlands row (Spring) is not datable, so it neither adds Spring nor a location.
        Assert.Equal(new[] { Season.Summer, Season.Fall, Season.Winter }, trout.Seasons);
        Assert.Equal(new[] { "Custom_ForestWest", "Forest", "Mountain" }, trout.Locations);
    }

    [Fact]
    public void The_Model_Places_It_From_Its_Rows_And_Its_Data_Fish_Row()
    {
        ItemPools pools = Pools(BullTroutRows(), new() { [BullTroutBare] = SveFish() }, SveWorld(),
            new() { [BullTroutBare] = RawFishEntry.Parse(BullTroutBare, BullTroutRow) });

        ItemAvailabilityModel model = ItemAvailabilityBuilder.Build(pools);

        Assert.True(model.IsPlaced(BullTrout));
        ItemAvailability a = model.For(BullTrout);
        Assert.Equal(AvailabilityWeeks.FirstWeekOf(Season.Summer), a.PacingWeek);
        Assert.Contains("ExcludeFromRandomSale", a.Basis);
        Assert.Contains("difficulty 45", a.Basis);
    }

    [Fact]
    public void A_Fish_Caught_Only_Where_No_Door_Leads_Stays_Unknown()
    {
        var rows = new[] { new RawSpawnEntry(HighlandsBass, null, null, "Custom_Highlands") };
        ItemPools pools = Pools(rows, new() { [HighlandsBass.Substring(3)] = SveFish() }, SveWorld());

        Assert.Empty(pools.UnpooledFish);
        Assert.False(ItemAvailabilityBuilder.Build(pools).IsPlaced(HighlandsBass));
    }

    [Fact]
    public void A_Gated_Vanilla_Map_Dates_The_Fish_Even_Without_A_Door()
    {
        const string bass = "(O)FlashShifter.StardewValleyExpandedCP_Radioactive_Bass";
        var rows = new[] { new RawSpawnEntry(bass, null, "LOCATION_Season Here Fall Spring Summer Winter", "Sewer") };
        ItemPools pools = Pools(rows, new() { [bass.Substring(3)] = SveFish() }, SveWorld());

        ItemAvailability a = ItemAvailabilityBuilder.Build(pools).For(bass);
        Assert.Equal(AvailabilityWeeks.SewerWeek, a.PacingWeek);
    }

    [Fact]
    public void Without_Location_Weeks_Nothing_Is_Collected()
    {
        ItemPools pools = Pools(BullTroutRows(), new() { [BullTroutBare] = SveFish() }, weeks: null);

        Assert.Empty(pools.UnpooledFish);
        Assert.False(ItemAvailabilityBuilder.Build(pools).IsPlaced(BullTrout));
    }

    [Fact]
    public void A_Pooled_Fish_Is_Not_Listed_Again()
    {
        var rows = new[] { new RawSpawnEntry("(O)145", null, null, "Forest") };
        ItemPools pools = Pools(rows, new() { ["145"] = new RawObjectEntry("Fish", -4, 30, false, new string[0]) }, SveWorld());

        Assert.Single(pools.Fish);
        Assert.Empty(pools.UnpooledFish);
    }

    [Fact]
    public void A_Special_Order_Row_And_A_Built_In_Ban_Still_Keep_A_Fish_Out()
    {
        var rows = new[]
        {
            new RawSpawnEntry("(O)899", null, "PLAYER_SPECIAL_ORDER_RULE_ACTIVE Current LEGENDARY_FAMILY", "Town"),
            new RawSpawnEntry("(O)795", null, null, "Forest"),   // Void Salmon: built-in ban
        };
        ItemPools pools = Pools(rows, new()
        {
            ["899"] = new RawObjectEntry("Fish", -4, 1000, true, new string[0]),
            ["795"] = new RawObjectEntry("Fish", -4, 150, true, new string[0]),
        }, SveWorld());

        Assert.Empty(pools.UnpooledFish);
    }

    [Fact]
    public void A_Frog_Ingredient_Lets_Its_Dish_Be_Placed()
    {
        const string frog = "(O)FlashShifter.StardewValleyExpandedCP_Frog";
        const string legs = "(O)FlashShifter.StardewValleyExpandedCP_Frog_Legs";
        var rows = new[] { new RawSpawnEntry(frog, null, "LOCATION_Season Here Spring Summer", "Mountain") };
        var objects = new Dictionary<string, RawObjectEntry>
        {
            [frog.Substring(3)] = SveFish(),
            [legs.Substring(3)] = new RawObjectEntry("Cooking", -7, 400, true, new string[0]),
            ["247"] = new RawObjectEntry("Basic", -25, 100, false, new string[0]),
        };
        ItemPools pools = Pools(rows, objects, SveWorld());
        var data = new EffortData
        {
            Objects = objects,
            CookingRecipes = new[] { new RawCookingRecipe("Frog Legs", new[] { frog, "(O)247" }, legs, "null") },
        };

        ItemAvailabilityModel model = ItemAvailabilityBuilder.Build(pools, effortData: data);

        Assert.True(model.IsPlaced(legs));
        Assert.DoesNotContain("unrecognised", model.For(legs).Basis);
    }
}

public class LocationWeeksTests
{
    private static LocationWeeks World(params (string From, string To)[] links)
        => LocationWeeks.Build(links.Select(l => new RawLocationLink(l.From, l.To)).ToList(), n => n.Contains("Island"));

    [Fact]
    public void A_Map_Takes_The_Latest_Gate_On_Its_Easiest_Path()
    {
        LocationWeeks weeks = World(("Farm", "BusStop"), ("BusStop", "Desert"), ("Desert", "SandyHouse"),
            ("Farm", "Forest"), ("Forest", "Woods"), ("Woods", "ModGlade"));

        Assert.True(weeks.TryGet("SandyHouse", out PlaceWeek sandy));
        Assert.Equal(AvailabilityWeeks.SkullCavernWeek, sandy.Week);
        Assert.Equal(AvailabilityWeeks.DesertHardWeek, sandy.Hard);
        Assert.Equal(AvailabilityWeeks.DesertExtremeWeek, sandy.Extreme);
        Assert.True(weeks.TryGet("ModGlade", out PlaceWeek glade));
        Assert.Equal(4, glade.Week);   // behind the Secret Woods
    }

    [Fact]
    public void The_Easier_Of_Two_Paths_Wins()
    {
        LocationWeeks weeks = World(("Farm", "Forest"), ("Forest", "Woods"), ("Woods", "ModGlade"), ("Farm", "ModGlade"));
        Assert.True(weeks.TryGet("ModGlade", out PlaceWeek glade));
        Assert.Equal(1, glade.Week);
    }

    [Fact]
    public void Doors_Are_One_Way_And_Forbidden_Maps_Are_Never_Entered()
    {
        LocationWeeks weeks = World(("Farm", "Beach"), ("ModCave", "Farm"), ("Beach", "IslandSouth"), ("IslandSouth", "ModReef"));
        Assert.False(weeks.TryGet("ModCave", out _));
        Assert.False(weeks.TryGet("IslandSouth", out _));
        Assert.False(weeks.TryGet("ModReef", out _));
    }

    [Fact]
    public void A_Gated_Map_With_No_Door_Keeps_Its_Own_Gate_And_Farm_Keys_Are_The_Farm()
    {
        LocationWeeks weeks = World(("Farm", "Town"));
        Assert.True(weeks.TryGet("Sewer", out PlaceWeek sewer));
        Assert.Equal(AvailabilityWeeks.SewerWeek, sewer.Week);
        Assert.True(weeks.TryGet("Farm_Standard", out PlaceWeek farm));
        Assert.Equal(1, farm.Week);
        Assert.False(weeks.TryGet("Custom_Highlands", out _));
    }
}
