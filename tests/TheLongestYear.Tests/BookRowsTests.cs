using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>Where the controller selection lands in the book pages, and which rows A can choose.</summary>
public class BookRowsTests
{
    [Theory]
    [InlineData(0, 0, 3, true)]
    [InlineData(2, 0, 3, true)]    // the last entry
    [InlineData(3, 0, 3, false)]   // the first blank row under a short list
    [InlineData(0, 0, 0, false)]   // an empty list
    [InlineData(2, 5, 8, true)]    // scrolled: entry 7, the last
    [InlineData(3, 5, 8, false)]   // scrolled: entry 8 does not exist
    [InlineData(-1, 0, 3, false)]
    public void HoldsEntry_is_true_only_for_rows_that_draw_an_entry(int row, int scroll, int count, bool expected)
        => Assert.Equal(expected, BookRows.HoldsEntry(row, scroll, count));

    [Theory]
    [InlineData(3, 0, 8, 3)]    // on screen, not scrolled
    [InlineData(5, 2, 8, 3)]    // on screen, scrolled
    [InlineData(0, 2, 8, 0)]    // scrolled off the top
    [InlineData(20, 0, 8, 7)]   // scrolled off the bottom
    [InlineData(-1, 0, 8, 0)]   // no pending slot
    public void RowForSlot_finds_the_slot_or_the_nearest_row(int slot, int scroll, int rowsPerPage, int expected)
        => Assert.Equal(expected, BookRows.RowForSlot(slot, scroll, rowsPerPage));
}
