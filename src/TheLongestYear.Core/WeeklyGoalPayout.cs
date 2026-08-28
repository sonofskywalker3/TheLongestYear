using System;
using System.Collections.Generic;

namespace TheLongestYear.Core;

/// <summary>Rule D (activity-themes spec 2026-08-28): the weekly bonus is split evenly across the
/// week's goals and paid as each one lands. <see cref="BonusSlot.Paid"/> is the idempotency
/// guard, so a save and reload never pays a goal twice and a one-goal Winter week pays its share
/// (120 / 7 = 17), not the full 120. The drawback still lifts only when every goal is done.</summary>
public static class WeeklyGoalPayout
{
    public static long PerGoal(long weekBonus, int goalCount)
        => goalCount <= 0
            ? 0
            : (long)Math.Round(weekBonus / (double)goalCount, MidpointRounding.AwayFromZero);

    /// <summary>Marks every complete, unpaid slot as paid and returns how many it marked.</summary>
    public static int MarkPaid(IReadOnlyList<BonusSlot> slots, Func<BonusSlot, bool> isComplete)
    {
        if (slots == null || isComplete == null) return 0;
        int marked = 0;
        foreach (BonusSlot slot in slots)
        {
            if (slot == null || slot.Paid || !isComplete(slot)) continue;
            slot.Paid = true;
            marked++;
        }
        return marked;
    }
}
