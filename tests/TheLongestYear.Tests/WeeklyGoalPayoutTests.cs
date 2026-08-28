using System.Collections.Generic;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class WeeklyGoalPayoutTests
{
    [Fact]
    public void A_three_goal_summer_week_pays_forty_five_in_three_fifteens()
    {
        var jp = new JpCalculator(new JpSettings());
        long week = jp.WeeklyQuestBonus(5);           // Summer week 1: 30 x 1.5
        Assert.Equal(45, week);
        Assert.Equal(15, WeeklyGoalPayout.PerGoal(week, 3));
        Assert.Equal(17, WeeklyGoalPayout.PerGoal(120, 7));
        Assert.Equal(0, WeeklyGoalPayout.PerGoal(120, 0));
    }

    [Fact]
    public void Completing_the_same_slot_twice_pays_once()
    {
        var slots = new List<BonusSlot> { new() { ItemId = "(O)1", Deposited = true }, new() { ItemId = "(O)2" } };
        Assert.Equal(1, WeeklyGoalPayout.MarkPaid(slots, s => s.Deposited));
        Assert.Equal(0, WeeklyGoalPayout.MarkPaid(slots, s => s.Deposited));
        slots[1].Deposited = true;
        Assert.Equal(1, WeeklyGoalPayout.MarkPaid(slots, s => s.Deposited));
        Assert.All(slots, s => Assert.True(s.Paid));
    }
}
