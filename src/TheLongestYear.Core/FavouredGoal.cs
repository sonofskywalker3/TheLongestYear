using System;
using System.Collections.Generic;

namespace TheLongestYear.Core;

/// <summary>The favoured goal (spec 2026-10-19-focus-cards, "The favoured goal (x2)"): each theme
/// card's goal list flags at most one line, its hardest hard roll, and that line's per-item JP is
/// doubled. Players only ever see the bonus; nothing says the line is hard.</summary>
public static class FavouredGoal
{
    /// <summary>The per-item JP factor for a favoured line.</summary>
    public const double JpFactor = 2.0;

    /// <summary>A line qualifies when it is a stretch line or its item's effort is Hard or Extreme.
    /// The obtainability "chance only" clause is not on this branch.</summary>
    public static bool IsHardRoll(BonusSlot slot, int? effort)
        => slot.Stretch || (effort is int e && EffortTiers.IsHard(e));

    /// <summary>Clears every flag in <paramref name="cardGoals"/>, then flags the qualifying line with
    /// the highest effort. A null effort ranks below every number; a tie keeps the earliest line.
    /// No qualifying line, no flag. Call again after the list loses lines (GoalLists.Dedupe).</summary>
    public static void Mark(IList<BonusSlot>? cardGoals, Func<string, int?>? effortOf)
    {
        if (cardGoals == null) return;
        BonusSlot? best = null;
        int? bestEffort = null;
        foreach (BonusSlot slot in cardGoals)
        {
            if (slot == null) continue;
            slot.Favoured = false;
            int? effort = effortOf?.Invoke(slot.ItemId);
            if (!IsHardRoll(slot, effort)) continue;
            if (best == null || Ranks(effort) > Ranks(bestEffort))
            {
                best = slot;
                bestEffort = effort;
            }
        }
        if (best != null) best.Favoured = true;
    }

    /// <summary>Per-item JP after the favoured factor, rounded away from zero.</summary>
    public static long Apply(long jp, bool favoured)
        => favoured ? (long)Math.Round(jp * JpFactor, MidpointRounding.AwayFromZero) : jp;

    /// <summary>True when <paramref name="slot"/> shows the x2 badge: favoured and not yet deposited.</summary>
    public static bool ShowsBadge(BonusSlot? slot) => slot != null && slot.Favoured && !slot.Deposited;

    /// <summary>True when the bundle slot (bundle, ingredient) is a committed favoured line still
    /// waiting for its deposit. Reads the stored lists the way the donation payout does: the first
    /// list while a theme is selected, the second only on a double week.</summary>
    public static bool BadgeAt(RunState? run, int bundleIndex, int ingredientIndex)
    {
        if (run == null) return false;
        if (run.CurrentSelection.HasValue && ShowsBadge(SlotAt(run.CurrentWeekBonusSlots, bundleIndex, ingredientIndex)))
            return true;
        return run.SecondSelection.HasValue && ShowsBadge(SlotAt(run.SecondWeekBonusSlots, bundleIndex, ingredientIndex));
    }

    /// <summary>The stored goal list of <paramref name="theme"/> when it is already picked this week,
    /// or null. A view of a picked theme reads this, never a fresh sample: donated lines drop out of
    /// the open pool, so a re-sample can land on a different favoured line.</summary>
    public static IReadOnlyList<BonusSlot>? StoredListFor(RunState? run, Theme theme)
    {
        if (run == null) return null;
        if (run.CurrentSelection == theme && run.CurrentWeekBonusSlots is { Count: > 0 } first)
            return first;
        if (run.SecondSelection == theme && run.SecondWeekBonusSlots is { Count: > 0 } second)
            return second;
        return null;
    }

    private static BonusSlot? SlotAt(List<BonusSlot>? slots, int bundleIndex, int ingredientIndex)
    {
        if (slots == null) return null;
        foreach (BonusSlot s in slots)
            if (s != null && s.BundleIndex == bundleIndex && s.IngredientIndex == ingredientIndex)
                return s;
        return null;
    }

    private static long Ranks(int? effort) => effort ?? long.MinValue;
}
