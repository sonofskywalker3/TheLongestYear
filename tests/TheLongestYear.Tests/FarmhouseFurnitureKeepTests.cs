using System.Linq;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class FarmhouseFurnitureKeepTests
{
    [Theory]
    [InlineData("(F)1120")]                    // table
    [InlineData("(F)2018")]                    // vanilla dresser
    [InlineData("(F)SomeMod_Chair")]           // another mod's furniture
    public void Furniture_is_kept(string id) => Assert.True(FarmhouseFurnitureKeep.IsKeptPiece(id));

    [Theory]
    [InlineData("(F)sonofskywalker3.TheLongestYear_Cookbook")]   // TLY's own books are re-granted
    [InlineData("(BC)130")]                                      // chest: an Object, never kept
    [InlineData("(O)72")]
    [InlineData("(H)0")]                                         // cosmetic, but not furniture
    [InlineData("")]
    [InlineData(null)]
    public void Non_furniture_and_mod_items_are_not_kept(string id) => Assert.False(FarmhouseFurnitureKeep.IsKeptPiece(id));

    [Theory]
    [InlineData("(H)0", true)]
    [InlineData("(S)1000", true)]
    [InlineData("(P)0", true)]
    [InlineData("(F)1364", true)]       // the starter bowl on the table
    [InlineData("(WP)5", true)]
    [InlineData("(FL)5", true)]
    [InlineData("(O)517", false)]       // a ring in a dresser
    [InlineData("(O)128", false)]       // a fish in a tank
    [InlineData("(B)504", false)]       // boots
    [InlineData("(TR)ParrotEgg", false)]
    public void Contents_follow_the_stash_nesting_rule(string id, bool kept)
        => Assert.Equal(kept, FarmhouseFurnitureKeep.KeepsContent(id));

    [Theory]
    [InlineData(true, true, HouseFurnitureOutcome.Place)]
    [InlineData(true, false, HouseFurnitureOutcome.DropByDoor)]
    [InlineData(false, true, HouseFurnitureOutcome.DropByDoor)]
    [InlineData(false, false, HouseFurnitureOutcome.DropByDoor)]
    public void A_piece_is_placed_only_when_its_room_exists_and_it_fits(bool roomExists, bool fits, HouseFurnitureOutcome expected)
        => Assert.Equal(expected, FarmhouseFurnitureKeep.Decide(roomExists, fits));

    [Fact]
    public void Rugs_go_down_first_and_the_rest_keep_their_order()
    {
        var order = FarmhouseFurnitureKeep.PlacementOrder(new[] { false, true, false, true, false });
        Assert.Equal(new[] { 1, 3, 0, 2, 4 }, order.ToArray());
    }

    [Theory]
    [InlineData(true, false, BedFallback.None)]               // a kept bed is in the house
    [InlineData(true, true, BedFallback.None)]
    [InlineData(false, true, BedFallback.KeptBedAtStarterSpot)] // the kept bed had no room: try the starter spot
    [InlineData(false, false, BedFallback.StarterBed)]          // no kept bed at all: the starter bed comes back
    public void The_house_always_ends_with_a_bed(bool bedPlaced, bool keptBedUnplaced, BedFallback expected)
        => Assert.Equal(expected, FarmhouseFurnitureKeep.ForBed(bedPlaced, keptBedUnplaced));
}

[Collection("i18n")]
public class FarmhouseFurnitureKeepCatalogTests
{
    [Fact]
    public void Catalog_row_is_one_level_250_jp_in_buildings()
    {
        UpgradeDefinition row = UpgradeCatalog.All.Single(u => u.Id == FarmhouseFurnitureKeep.UpgradeId);
        Assert.Equal("keep_farmhouse_furniture", row.Id);
        Assert.Equal(250L, row.Cost);
        Assert.Equal(UpgradeCategory.Buildings, row.Category);
        Assert.Null(row.PrerequisiteId);
        Assert.Null(row.RunReachRequirement);
    }

    [Fact]
    public void Catalog_row_sits_next_to_keep_farm_decor()
    {
        var ids = UpgradeCatalog.All.Select(u => u.Id).ToList();
        Assert.Equal(ids.IndexOf(FarmDecorKeep.UpgradeId) + 1, ids.IndexOf(FarmhouseFurnitureKeep.UpgradeId));
    }
}
