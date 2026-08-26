using System.Collections.Generic;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>
/// Nexus/YouTube report (@ggrace67, 2026-08-26): completing a bundle credited weekly goal slots the
/// player never donated to. Vanilla blanket-sets every ingredient flag in a bundle when the bundle
/// completes (JunimoNoteMenu.cs:1009-1011), so an n-of-m bundle finished with OTHER items flips the
/// goal slot's flag too. A goal now needs both the live flag AND a recorded deposit.
/// </summary>
public class WeeklyGoalCreditTests
{
    private static BonusSlot Slot(int bundle, int ingredient, bool deposited = false) =>
        new() { BundleIndex = bundle, IngredientIndex = ingredient, Deposited = deposited };

    [Fact]
    public void Live_flag_without_a_recorded_deposit_does_not_count()
    {
        Assert.False(WeeklyGoalCredit.IsSatisfied(Slot(3, 2), liveComplete: true));
    }

    [Fact]
    public void Live_flag_plus_a_recorded_deposit_counts()
    {
        Assert.True(WeeklyGoalCredit.IsSatisfied(Slot(3, 2, deposited: true), liveComplete: true));
    }

    [Fact]
    public void A_recorded_deposit_alone_does_not_count()
    {
        // Defensive: the live CC state stays the authority on whether the slot is actually filled.
        Assert.False(WeeklyGoalCredit.IsSatisfied(Slot(3, 2, deposited: true), liveComplete: false));
    }

    [Fact]
    public void Null_slot_never_counts()
    {
        Assert.False(WeeklyGoalCredit.IsSatisfied(null, liveComplete: true));
    }

    [Fact]
    public void RecordDeposit_marks_only_the_matching_slot()
    {
        var slots = new List<BonusSlot> { Slot(3, 2), Slot(3, 5), Slot(7, 2) };

        bool hit = WeeklyGoalCredit.RecordDeposit(slots, bundleIndex: 3, ingredientIndex: 5);

        Assert.True(hit);
        Assert.False(slots[0].Deposited);
        Assert.True(slots[1].Deposited);
        Assert.False(slots[2].Deposited);
    }

    [Fact]
    public void RecordDeposit_on_a_slot_that_is_not_a_goal_is_a_no_op()
    {
        var slots = new List<BonusSlot> { Slot(3, 2) };

        Assert.False(WeeklyGoalCredit.RecordDeposit(slots, bundleIndex: 9, ingredientIndex: 9));
        Assert.False(slots[0].Deposited);
    }

    [Fact]
    public void RecordDeposit_tolerates_a_null_list()
    {
        Assert.False(WeeklyGoalCredit.RecordDeposit(null, 1, 1));
    }

    [Fact]
    public void Grandfather_credits_slots_already_complete_at_load_time()
    {
        // Upgrading mid-week must not un-tick goals the player legitimately finished before the
        // fix existed: at load, anything already complete in live CC state is trusted once.
        var slots = new List<BonusSlot> { Slot(3, 2), Slot(4, 1) };

        int credited = WeeklyGoalCredit.GrandfatherCompleted(slots, s => s.BundleIndex == 3);

        Assert.Equal(1, credited);
        Assert.True(slots[0].Deposited);
        Assert.False(slots[1].Deposited);
    }

    [Fact]
    public void Grandfather_does_not_re_credit_an_already_recorded_slot()
    {
        var slots = new List<BonusSlot> { Slot(3, 2, deposited: true) };

        Assert.Equal(0, WeeklyGoalCredit.GrandfatherCompleted(slots, _ => true));
    }
}
