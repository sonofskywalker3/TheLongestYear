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

    private static long Ranks(int? effort) => effort ?? long.MinValue;
}
