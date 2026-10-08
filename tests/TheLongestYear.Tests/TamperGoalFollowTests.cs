using System.Collections.Generic;
using TheLongestYear.Core;
using TheLongestYear.Core.Sabotage;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>Designer, 2026-10-08 (Winter save): after a tamper the weekly goals still asked for the
/// tainted Super Cucumbers. A goal on a rewritten slot now follows the slot.</summary>
public class TamperGoalFollowTests
{
    private static BonusSlot Goal(int bundle, int slot, string item, int stack = 1) =>
        new() { BundleIndex = bundle, IngredientIndex = slot, ItemId = item, Stack = stack, BundleName = "Ocean Fish" };

    [Fact]
    public void A_goal_on_the_rewritten_slot_names_the_new_item_and_stack()
    {
        var first = new List<BonusSlot> { Goal(7, 1, "(O)155"), Goal(7, 2, "(O)131") };
        int changed = TamperRule.FollowGoals(new IList<BonusSlot>[] { first, null }, 7, new[] { 1 }, "(O)150", 3, 0);
        Assert.Equal(1, changed);
        Assert.Equal("(O)150", first[0].ItemId);
        Assert.Equal(3, first[0].Stack);
        Assert.Equal("(O)131", first[1].ItemId);
        Assert.Equal(2, first.Count);
    }

    [Fact]
    public void Both_lists_follow_on_a_double_week()
    {
        var first = new List<BonusSlot> { Goal(3, 0, "(O)388") };
        var second = new List<BonusSlot> { Goal(3, 1, "(O)388") };
        Assert.Equal(2, TamperRule.FollowGoals(new IList<BonusSlot>[] { first, second }, 3, new[] { 0, 1 }, "(O)390", 5, 0));
        Assert.Equal("(O)390", first[0].ItemId);
        Assert.Equal("(O)390", second[0].ItemId);
    }

    [Fact]
    public void Another_bundle_with_the_same_slot_number_is_left_alone()
    {
        var first = new List<BonusSlot> { Goal(8, 1, "(O)155") };
        Assert.Equal(0, TamperRule.FollowGoals(new IList<BonusSlot>[] { first }, 7, new[] { 1 }, "(O)150", 3, 0));
        Assert.Equal("(O)155", first[0].ItemId);
    }

    [Fact]
    public void The_discount_and_old_item_tags_are_dropped_so_the_week_end_revert_cannot_restore_the_old_ask()
    {
        BonusSlot g = Goal(7, 1, "(O)155", stack: 2);
        g.OriginalStack = 4;
        g.Stretch = true;
        g.RouteTag = "Boost: Sneak Peek";
        g.Due = true;
        TamperRule.FollowGoals(new IList<BonusSlot>[] { new List<BonusSlot> { g } }, 7, new[] { 1 }, "(O)150", 3, 0);
        Assert.Equal(0, g.OriginalStack);
        Assert.False(g.Stretch);
        Assert.Null(g.RouteTag);
        Assert.True(g.Due);
    }
}
