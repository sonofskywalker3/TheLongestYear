using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;
using static TheLongestYear.Core.HerdSlotKind;

namespace TheLongestYear.Tests;

public class HerdBookRulesTests
{
    private static HerdEntry Entry(int slot, long id, string type = "White Chicken", string name = "Clucky", int friendship = 400)
        => new(slot, id, type, name, null, friendship, 200, 10, 12, false, true);

    private static HerdAnimal Animal(long id, string type, string name, int friendship = 0)
        => new(id, type, name, friendship);

    private static readonly IReadOnlyList<HerdSlotKind> FourSlots = new[] { Chicken, Chicken, Cow, Cow };

    [Theory]
    [InlineData(0, 0)]
    [InlineData(199, 0)]
    [InlineData(200, 1)]
    [InlineData(999, 4)]
    [InlineData(1000, 5)]
    [InlineData(1200, 5)]
    [InlineData(-5, 0)]
    public void Hearts_are_friendship_over_200_capped_at_5(int friendship, int hearts)
        => Assert.Equal(hearts, HerdBookRules.Hearts(friendship));

    [Theory]
    [InlineData(0, 3, 3)]
    [InlineData(10, 3, 10)]
    [InlineData(5, 5, 5)]
    public void AdultAge_never_leaves_an_animal_a_baby(int savedAge, int daysToMature, int expected)
        => Assert.Equal(expected, HerdBookRules.AdultAge(savedAge, daysToMature));

    [Fact]
    public void SlotsFor_meta_reads_the_highest_owned_tier()
    {
        var meta = new MetaState { OwnedUpgrades = { "herdbook_1", "herdbook_2" } };
        Assert.Equal(new[] { Chicken, Chicken, Cow }, HerdBookRules.SlotsFor(meta));
        Assert.Single(HerdBookRules.SlotsFor(new MetaState()));
    }

    [Fact]
    public void Register_fills_an_empty_slot()
    {
        var book = new List<HerdEntry>();
        Assert.Equal(HerdRegisterResult.Registered, HerdBookRules.Register(book, FourSlots, Entry(0, 1)));
        Assert.Equal(1, HerdBookRules.EntryAt(book, 0)!.AnimalId);
    }

    [Fact]
    public void Register_refuses_the_wrong_kind()
    {
        var book = new List<HerdEntry>();
        Assert.Equal(HerdRegisterResult.WrongKind, HerdBookRules.Register(book, FourSlots, Entry(0, 1, "Void Chicken")));
        Assert.Empty(book);
    }

    [Fact]
    public void Register_refuses_a_slot_not_owned()
    {
        var book = new List<HerdEntry>();
        Assert.Equal(HerdRegisterResult.SlotNotOwned, HerdBookRules.Register(book, FourSlots, Entry(4, 1, "Duck")));
        Assert.Equal(HerdRegisterResult.SlotNotOwned, HerdBookRules.Register(book, FourSlots, Entry(-1, 1)));
        Assert.Empty(book);
    }

    [Fact]
    public void Register_refuses_an_animal_already_in_another_slot()
    {
        var book = new List<HerdEntry> { Entry(0, 7) };
        Assert.Equal(HerdRegisterResult.AlreadyRegistered, HerdBookRules.Register(book, FourSlots, Entry(1, 7)));
        Assert.Single(book);
    }

    [Fact]
    public void Register_replaces_what_the_slot_held_and_keeps_the_book_in_slot_order()
    {
        var book = new List<HerdEntry> { Entry(2, 5, "White Cow"), Entry(0, 1) };
        Assert.Equal(HerdRegisterResult.Registered, HerdBookRules.Register(book, FourSlots, Entry(0, 9, name: "Nugget")));
        Assert.Equal(new long[] { 9, 5 }, book.Select(e => e.AnimalId));
    }

    [Fact]
    public void Remove_empties_the_slot()
    {
        var book = new List<HerdEntry> { Entry(0, 1), Entry(1, 2) };
        Assert.True(HerdBookRules.Remove(book, 0));
        Assert.False(HerdBookRules.Remove(book, 0));
        Assert.Null(HerdBookRules.EntryAt(book, 0));
        Assert.NotNull(HerdBookRules.EntryAt(book, 1));
    }

    [Fact]
    public void Candidates_are_fitting_unregistered_animals_sorted_by_name()
    {
        var book = new List<HerdEntry> { Entry(0, 1) };
        var live = new[]
        {
            Animal(1, "White Chicken", "Already"),
            Animal(2, "Brown Chicken", "Zed"),
            Animal(3, "Blue Chicken", "Amy"),
            Animal(4, "Void Chicken", "Shade"),
            Animal(5, "White Cow", "Moo"),
        };
        Assert.Equal(new long[] { 3, 2 }, HerdBookRules.Candidates(Chicken, live, book).Select(a => a.Id));
    }

    [Fact]
    public void ShouldOffer_when_an_empty_slot_has_a_fitting_animal()
        => Assert.True(HerdBookRules.ShouldOfferAtReset(FourSlots, new List<HerdEntry>(), new[] { Animal(5, "White Cow", "Moo") }));

    [Fact]
    public void ShouldOffer_not_when_every_slot_is_full()
    {
        var book = new List<HerdEntry> { Entry(0, 1), Entry(1, 2), Entry(2, 3, "White Cow"), Entry(3, 4, "Brown Cow") };
        Assert.False(HerdBookRules.ShouldOfferAtReset(FourSlots, book, new[] { Animal(9, "White Chicken", "Spare") }));
    }

    [Fact]
    public void ShouldOffer_not_when_no_animal_fits_an_empty_slot()
        => Assert.False(HerdBookRules.ShouldOfferAtReset(new[] { Chicken }, new List<HerdEntry>(), new[] { Animal(5, "White Cow", "Moo") }));

    [Fact]
    public void ShouldOffer_not_when_the_only_fitting_animal_is_already_registered()
    {
        var book = new List<HerdEntry> { Entry(0, 1) };
        Assert.False(HerdBookRules.ShouldOfferAtReset(new[] { Chicken, Chicken }, book, new[] { Animal(1, "White Chicken", "Clucky") }));
    }

    [Fact]
    public void Refresh_takes_the_live_snapshot_and_keeps_the_slot()
    {
        var book = new List<HerdEntry> { Entry(1, 7, friendship: 300) };
        var live = new Dictionary<long, HerdEntry> { [7] = Entry(-1, 7, friendship: 950) with { Age = 40 } };
        HerdEntry refreshed = HerdBookRules.Refresh(book, live).Single();
        Assert.Equal(1, refreshed.SlotIndex);
        Assert.Equal(950, refreshed.Friendship);
        Assert.Equal(40, refreshed.Age);
    }

    [Fact]
    public void Refresh_keeps_the_last_snapshot_when_the_animal_is_gone()
    {
        var book = new List<HerdEntry> { Entry(0, 7, friendship: 640) };
        HerdEntry kept = HerdBookRules.Refresh(book, new Dictionary<long, HerdEntry>()).Single();
        Assert.Equal(book[0], kept);
    }

    [Fact]
    public void Used_counts_only_owned_slots()
    {
        var book = new List<HerdEntry> { Entry(0, 1), Entry(3, 2, "White Cow") };
        Assert.Equal(1, HerdBookRules.Used(book, 2));
        Assert.Equal(2, HerdBookRules.Used(book, 4));
    }
}
