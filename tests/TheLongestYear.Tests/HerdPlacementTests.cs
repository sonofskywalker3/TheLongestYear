using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;

namespace TheLongestYear.Tests;

public class HerdPlacementTests
{
    // Slot kinds by index: 0,1 Chicken; 2,3 Cow; 4,5 Duck; 14 Void Chicken.
    private static HerdEntry Entry(int slot, long id, string type)
        => new(slot, id, type, "A" + id, null, 500, 200, 20, 20, false, true);

    [Fact]
    public void Places_each_entry_in_the_first_house_of_its_family_with_room()
    {
        var book = new[] { Entry(0, 1, "White Chicken"), Entry(2, 2, "White Cow") };
        var houses = new[] { new HerdHouse("Barn", 4), new HerdHouse("Coop", 4) };
        List<HerdAssignment> result = HerdPlacement.Assign(book, 4, houses);
        Assert.Equal(new[] { 1, 0 }, result.Select(r => r.HouseIndex));
        Assert.All(result, r => Assert.Equal(HerdSkip.None, r.Skip));
    }

    [Fact]
    public void A_full_house_leaves_the_later_slot_waiting_with_no_room()
    {
        var book = new[] { Entry(1, 2, "Brown Chicken"), Entry(0, 1, "White Chicken") };
        List<HerdAssignment> result = HerdPlacement.Assign(book, 2, new[] { new HerdHouse("Coop", 1) });
        Assert.Equal(0, result[0].Entry.SlotIndex);
        Assert.Equal(HerdSkip.None, result[0].Skip);
        Assert.Equal(1, result[1].Entry.SlotIndex);
        Assert.Equal(HerdSkip.NoRoom, result[1].Skip);
        Assert.Equal(-1, result[1].HouseIndex);
    }

    [Fact]
    public void Room_already_taken_by_starting_animals_counts()
    {
        List<HerdAssignment> result = HerdPlacement.Assign(new[] { Entry(0, 1, "White Chicken") }, 1, new[] { new HerdHouse("Coop", 0) });
        Assert.Equal(HerdSkip.NoRoom, result.Single().Skip);
    }

    [Fact]
    public void No_building_of_the_family_is_NoBuilding()
    {
        List<HerdAssignment> result = HerdPlacement.Assign(new[] { Entry(2, 5, "White Cow") }, 4, new[] { new HerdHouse("Deluxe Coop", 12) });
        Assert.Equal(HerdSkip.NoBuilding, result.Single().Skip);
    }

    [Fact]
    public void A_tier_too_low_is_NoBuilding()
    {
        List<HerdAssignment> result = HerdPlacement.Assign(new[] { Entry(4, 5, "Duck") }, 6, new[] { new HerdHouse("Coop", 4) });
        Assert.Equal(HerdSkip.NoBuilding, result.Single().Skip);
    }

    [Fact]
    public void A_higher_tier_takes_a_lower_tier_animal()
    {
        List<HerdAssignment> result = HerdPlacement.Assign(new[] { Entry(0, 1, "White Chicken") }, 1, new[] { new HerdHouse("Deluxe Coop", 12) });
        Assert.Equal(0, result.Single().HouseIndex);
    }

    [Fact]
    public void A_slot_not_owned_is_skipped_and_uses_no_room()
    {
        var book = new[] { Entry(14, 9, "Void Chicken"), Entry(0, 1, "White Chicken") };
        List<HerdAssignment> result = HerdPlacement.Assign(book, 2, new[] { new HerdHouse("Coop", 1) });
        Assert.Equal(HerdSkip.None, result.Single(r => r.Entry.SlotIndex == 0).Skip);
        Assert.Equal(HerdSkip.SlotNotOwned, result.Single(r => r.Entry.SlotIndex == 14).Skip);
    }

    [Fact]
    public void Spills_into_a_second_house_of_the_same_family()
    {
        var book = new[] { Entry(0, 1, "White Chicken"), Entry(1, 2, "Brown Chicken") };
        var houses = new[] { new HerdHouse("Coop", 1), new HerdHouse("Big Coop", 3) };
        Assert.Equal(new[] { 0, 1 }, HerdPlacement.Assign(book, 2, houses).Select(r => r.HouseIndex));
    }
}
