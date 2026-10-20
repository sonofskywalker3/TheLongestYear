using System;
using System.Collections.Generic;

namespace TheLongestYear.Core;

/// <summary>Which goal lines show their full ask beside the lowered one ("13 (was 27)"), so the
/// player can see the theme week discount is temporary. Display only: the discount itself lives
/// in <see cref="WeeklyGoalDiscount"/>.</summary>
public static class LoweredAsk
{
    /// <summary>The full ask to show beside <paramref name="slot"/>'s lowered one, or null when the
    /// line is not lowered (no discount on this difficulty, a line at or under the floor) or is
    /// already donated (a donated line stays done at the lowered ask, nothing is temporary).</summary>
    public static int? FullAsk(BonusSlot? slot)
        => slot != null && !slot.Deposited && slot.OriginalStack > slot.Stack ? slot.OriginalStack : null;

    /// <summary>This week's goal lines from both lists (the second is only filled on a double week).</summary>
    public static IEnumerable<BonusSlot> WeekSlots(RunState? run)
    {
        if (run == null) yield break;
        foreach (BonusSlot s in run.CurrentWeekBonusSlots ?? new List<BonusSlot>())
            if (s != null) yield return s;
        foreach (BonusSlot s in run.SecondWeekBonusSlots ?? new List<BonusSlot>())
            if (s != null) yield return s;
    }

    /// <summary>The full ask of bundle line (<paramref name="bundleIndex"/>, <paramref name="ingredientIndex"/>)
    /// when this week's discount lowered it, or null. <paramref name="liveStack"/> is what the board
    /// asks for now: a line another mod rewrote underneath no longer holds the lowered ask, so it
    /// gets no "was".</summary>
    public static int? AtLine(IEnumerable<BonusSlot> weekSlots, int bundleIndex, int ingredientIndex, int liveStack)
    {
        foreach (BonusSlot s in weekSlots ?? Array.Empty<BonusSlot>())
            if (s.BundleIndex == bundleIndex && s.IngredientIndex == ingredientIndex && s.Stack == liveStack)
                return FullAsk(s);
        return null;
    }

    /// <summary>As <see cref="AtLine"/>, for a list that knows the bundle and item but not the line.</summary>
    public static int? ForItem(IEnumerable<BonusSlot> weekSlots, int bundleIndex, string itemId, int liveStack)
    {
        if (string.IsNullOrEmpty(itemId)) return null;
        string id = BundleParsing.NormalizeItemId(itemId);
        foreach (BonusSlot s in weekSlots ?? Array.Empty<BonusSlot>())
            if (s.BundleIndex == bundleIndex && s.Stack == liveStack
                && string.Equals(BundleParsing.NormalizeItemId(s.ItemId), id, StringComparison.Ordinal)
                && FullAsk(s) is int full)
                return full;
        return null;
    }
}
