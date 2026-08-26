using System;
using System.Collections.Generic;

namespace TheLongestYear.Core;

/// <summary>
/// Whether a weekly theme goal slot counts as done.
///
/// The live Community Center flag alone is not enough. Vanilla blanket-sets EVERY ingredient flag
/// in a bundle the moment that bundle completes (decompile: JunimoNoteMenu.cs lines 1009-1011, and
/// again at 1085), so in a bundle that only requires n of its m listed items, finishing it with the
/// other items flips the goal slot's flag too. Reported by @ggrace67 on emmalution's Summer video
/// (2026-08-26): "if you complete a bundle it counts all items in it as used for the weekly theme
/// even if you didn't donate them so it still completes and lifts the drawback."
///
/// So a goal needs BOTH the live flag (still the authority on whether the slot is filled) and a
/// recorded deposit into that exact slot.
/// </summary>
public static class WeeklyGoalCredit
{
    /// <summary>True when this goal slot counts: the player deposited into it AND the live CC
    /// state agrees the slot is filled.</summary>
    public static bool IsSatisfied(BonusSlot slot, bool liveComplete) =>
        slot != null && slot.Deposited && liveComplete;

    /// <summary>Record a real deposit against this week's goals. Returns true when the deposit
    /// landed on one of them (callers use it for logging only).</summary>
    public static bool RecordDeposit(IReadOnlyList<BonusSlot> slots, int bundleIndex, int ingredientIndex)
    {
        if (slots == null) return false;
        bool hit = false;
        for (int i = 0; i < slots.Count; i++)
        {
            BonusSlot slot = slots[i];
            if (slot == null || slot.BundleIndex != bundleIndex || slot.IngredientIndex != ingredientIndex)
                continue;
            slot.Deposited = true;
            hit = true;
        }
        return hit;
    }

    /// <summary>One-time trust pass at load: a save written before this rule existed has no deposit
    /// records, so any goal already complete in live CC state would silently un-tick. Credit those
    /// once and let the rule govern from there. Returns how many slots were grandfathered.</summary>
    public static int GrandfatherCompleted(IReadOnlyList<BonusSlot> slots, Func<BonusSlot, bool> liveComplete)
    {
        if (slots == null || liveComplete == null) return 0;
        int credited = 0;
        for (int i = 0; i < slots.Count; i++)
        {
            BonusSlot slot = slots[i];
            if (slot == null || slot.Deposited || !liveComplete(slot)) continue;
            slot.Deposited = true;
            credited++;
        }
        return credited;
    }
}
