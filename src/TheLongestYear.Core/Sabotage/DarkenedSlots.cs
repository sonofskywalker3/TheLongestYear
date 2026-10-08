using System;
using System.Collections.Generic;

namespace TheLongestYear.Core.Sabotage;

/// <summary>The bundle slots a reversion emptied (designer, 2026-10-08): the bundle menu marks each
/// in the tainted purple until the player fills it again. Kept on <see cref="RunState.DarkenedSlots"/>,
/// so the mark survives a save and reload, and cleared at the loop reset with the rest of the run.
/// A slot is marked at most once; filling it clears its mark, and a mark on a slot that is filled
/// anyway (a bundle completed some other way) is never drawn and is dropped at the next tidy.</summary>
public static class DarkenedSlots
{
    /// <summary>Mark a slot the darkness just emptied. Returns false when it was already marked.</summary>
    public static bool Mark(List<DonatedSlot> marks, int bundleIndex, int ingredientIndex, string itemId)
    {
        if (marks is null) throw new ArgumentNullException(nameof(marks));
        if (IsMarked(marks, bundleIndex, ingredientIndex)) return false;
        marks.Add(new DonatedSlot { BundleIndex = bundleIndex, IngredientIndex = ingredientIndex, ItemId = itemId ?? "" });
        return true;
    }

    public static bool IsMarked(IReadOnlyList<DonatedSlot>? marks, int bundleIndex, int ingredientIndex)
    {
        if (marks is null) return false;
        foreach (DonatedSlot m in marks)
            if (m != null && m.BundleIndex == bundleIndex && m.IngredientIndex == ingredientIndex) return true;
        return false;
    }

    /// <summary>Is the mark drawn on this slot? Only while it is marked and still empty.</summary>
    public static bool Shows(IReadOnlyList<DonatedSlot>? marks, int bundleIndex, int ingredientIndex, bool slotFilled)
        => !slotFilled && IsMarked(marks, bundleIndex, ingredientIndex);

    /// <summary>Drop the marks on every slot that is filled again. Returns how many were dropped.</summary>
    public static int ClearFilled(List<DonatedSlot>? marks, Func<int, int, bool> slotFilled)
    {
        if (marks is null) return 0;
        if (slotFilled is null) throw new ArgumentNullException(nameof(slotFilled));
        return marks.RemoveAll(m => m == null || slotFilled(m.BundleIndex, m.IngredientIndex));
    }
}
