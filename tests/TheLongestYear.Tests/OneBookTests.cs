using System.Collections.Generic;
using TheLongestYear.Core;
using TheLongestYear.Core.Interactables;
using static TheLongestYear.Core.Interactables.BookSpot;
using static TheLongestYear.Core.Interactables.BookTab;

namespace TheLongestYear.Tests;

public class OneBookTests
{
    private static BookCopy New(BookSpot spot) => new(false, spot);
    private static BookCopy Old(BookSpot spot) => new(true, spot);

    [Fact]
    public void Book_id_is_stable_and_differs_from_the_retired_three()
    {
        Assert.Equal("sonofskywalker3.TheLongestYear_Book", OneBook.BookId);
        Assert.Equal(new[] { BookKit.CookbookId, BookKit.CraftbookId, BookKit.HerdBookId }, OneBook.RetiredIds);
        Assert.DoesNotContain(OneBook.BookId, OneBook.RetiredIds);
        Assert.False(OneBook.IsRetired(BookKit.BundleLogId));
        Assert.True(OneBook.IsRetired(BookKit.HerdBookId));
    }

    [Fact]
    public void Each_retired_book_maps_to_its_tab()
    {
        Assert.Equal(Cookbook, OneBook.TabForRetired(BookKit.CookbookId));
        Assert.Equal(Craftbook, OneBook.TabForRetired(BookKit.CraftbookId));
        Assert.Equal(HerdBook, OneBook.TabForRetired(BookKit.HerdBookId));
        Assert.Null(OneBook.TabForRetired(BookKit.BundleLogId));
    }

    [Theory]
    [InlineData(true, true, true, new[] { Cookbook, Craftbook, HerdBook })]
    [InlineData(true, false, false, new[] { Cookbook })]
    [InlineData(false, true, true, new[] { Craftbook, HerdBook })]
    [InlineData(false, false, true, new[] { HerdBook })]
    [InlineData(false, false, false, new BookTab[0])]
    public void Only_owned_sections_get_a_tab_in_book_order(bool cook, bool craft, bool herd, BookTab[] expected)
        => Assert.Equal(expected, OneBook.Tabs(cook, craft, herd));

    [Theory]
    [InlineData(0, 0, false)]
    [InlineData(4, 0, true)]
    [InlineData(0, 2, true)]   // a section that still holds something keeps its tab
    public void A_section_is_owned_with_a_slot_or_something_in_it(int slots, int held, bool owned)
        => Assert.Equal(owned, OneBook.Owns(slots, held));

    [Fact]
    public void A_fresh_save_owns_all_three_tabs_through_the_free_slots()
        => Assert.Equal(new[] { Cookbook, Craftbook, HerdBook }, OneBook.TabsFor(new MetaState()));

    [Fact]
    public void Resolve_opens_the_wanted_tab_or_falls_back_to_the_first()
    {
        Assert.Equal(HerdBook, OneBook.Resolve(new[] { Cookbook, HerdBook }, HerdBook));
        Assert.Equal(Cookbook, OneBook.Resolve(new[] { Cookbook, HerdBook }, Craftbook));
        Assert.Null(OneBook.Resolve(new BookTab[0], Cookbook));
    }

    [Fact]
    public void Step_wraps_both_ways()
    {
        BookTab[] tabs = { Cookbook, Craftbook, HerdBook };
        Assert.Equal(Craftbook, OneBook.Step(tabs, Cookbook, +1));
        Assert.Equal(Cookbook, OneBook.Step(tabs, HerdBook, +1));
        Assert.Equal(HerdBook, OneBook.Step(tabs, Cookbook, -1));
        Assert.Equal(HerdBook, OneBook.Step(new[] { HerdBook }, HerdBook, +1));
    }

    [Fact]
    public void Exactly_one_book_is_a_no_op()
    {
        Assert.True(OneBook.Plan(new[] { New(Carried) }, owned: true).IsNoOp);
        Assert.True(OneBook.Plan(new[] { New(Placed) }, owned: true).IsNoOp);
        Assert.True(OneBook.Plan(new[] { New(Stored) }, owned: true).IsNoOp);
    }

    [Fact]
    public void Nothing_at_all_grants_one()
    {
        BookReconcilePlan plan = OneBook.Plan(new List<BookCopy>(), owned: true);
        Assert.True(plan.Grant);
        Assert.Null(plan.ReplaceAt);
        Assert.Empty(plan.Remove);
    }

    [Fact]
    public void An_old_save_swaps_the_placed_book_and_removes_the_other_two()
    {
        // Cookbook in the bag, Craftbook on a shelf, Herd Book in a chest.
        var copies = new[] { Old(Carried), Old(Placed), Old(Stored) };
        BookReconcilePlan plan = OneBook.Plan(copies, owned: true);
        Assert.Equal(1, plan.ReplaceAt);
        Assert.Equal(new[] { 0, 2 }, plan.Remove);
        Assert.False(plan.Grant);
    }

    [Fact]
    public void An_old_save_with_all_three_carried_swaps_the_first_slot()
    {
        BookReconcilePlan plan = OneBook.Plan(new[] { Old(Carried), Old(Carried), Old(Carried) }, owned: true);
        Assert.Equal(0, plan.ReplaceAt);
        Assert.Equal(new[] { 1, 2 }, plan.Remove);
    }

    [Fact]
    public void Retired_books_in_chest_and_bag_prefer_the_chest()
    {
        BookReconcilePlan plan = OneBook.Plan(new[] { Old(Carried), Old(Stored) }, owned: true);
        Assert.Equal(1, plan.ReplaceAt);
        Assert.Equal(new[] { 0 }, plan.Remove);
    }

    [Fact]
    public void With_the_new_book_present_every_retired_copy_just_goes()
    {
        BookReconcilePlan plan = OneBook.Plan(new[] { Old(Placed), New(Carried), Old(Carried) }, owned: true);
        Assert.Null(plan.ReplaceAt);
        Assert.False(plan.Grant);
        Assert.Equal(new[] { 0, 2 }, plan.Remove);
    }

    [Fact]
    public void Duplicate_new_books_keep_the_placed_one()
    {
        BookReconcilePlan plan = OneBook.Plan(new[] { New(Carried), New(Stored), New(Placed), New(Carried) }, owned: true);
        Assert.Equal(new[] { 0, 1, 3 }, plan.Remove);
        Assert.False(plan.Grant);
        Assert.Null(plan.ReplaceAt);
    }

    [Fact]
    public void Not_owned_removes_every_copy_and_grants_nothing()
    {
        BookReconcilePlan plan = OneBook.Plan(new[] { New(Placed), Old(Carried) }, owned: false);
        Assert.Equal(new[] { 0, 1 }, plan.Remove);
        Assert.Null(plan.ReplaceAt);
        Assert.False(plan.Grant);
    }

    [Fact]
    public void Not_owned_and_empty_is_a_no_op()
        => Assert.True(OneBook.Plan(new List<BookCopy>(), owned: false).IsNoOp);
}
