using System;

namespace TheLongestYear.Core;

/// <summary>Row arithmetic for the book pages' scrolled lists: a list entry sits on the screen row
/// given by its index minus the scroll offset.</summary>
public static class BookRows
{
    /// <summary>Whether a screen row shows an entry of a list with <paramref name="count"/> entries.</summary>
    public static bool HoldsEntry(int row, int scroll, int count) => row >= 0 && scroll + row < count;

    /// <summary>The screen row showing a slot, or the nearest row when the slot is scrolled off.</summary>
    public static int RowForSlot(int slot, int scroll, int rowsPerPage)
        => Math.Max(0, Math.Min(rowsPerPage - 1, slot - scroll));
}
