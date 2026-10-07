using System.Linq;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class GoalListsTests
{
    private static BonusSlot Slot(int bundle, int ingredient, string item = "x")
        => new() { BundleIndex = bundle, IngredientIndex = ingredient, ItemId = item };

    [Fact]
    public void A_line_on_both_lists_stays_with_the_first_card()
    {
        var first = new[] { Slot(1, 0), Slot(2, 3) };
        var second = new[] { Slot(5, 1, "a"), Slot(2, 3, "shared"), Slot(6, 0, "b") };

        var kept = GoalLists.Dedupe(first, second);

        Assert.Equal(new[] { "a", "b" }, kept.Select(s => s.ItemId));
    }

    [Fact]
    public void Same_bundle_different_line_is_not_shared()
    {
        var kept = GoalLists.Dedupe(new[] { Slot(2, 3) }, new[] { Slot(2, 4, "keep"), Slot(3, 3, "keep2") });
        Assert.Equal(new[] { "keep", "keep2" }, kept.Select(s => s.ItemId));
    }

    [Fact]
    public void No_overlap_keeps_the_second_list_in_order()
    {
        var second = new[] { Slot(9, 0, "c"), Slot(4, 2, "a"), Slot(7, 1, "b") };
        var kept = GoalLists.Dedupe(new[] { Slot(1, 1) }, second);
        Assert.Equal(new[] { "c", "a", "b" }, kept.Select(s => s.ItemId));
    }

    [Fact]
    public void Empty_or_null_inputs_are_safe()
    {
        Assert.Empty(GoalLists.Dedupe(new[] { Slot(1, 1) }, null));
        Assert.Single(GoalLists.Dedupe(null, new[] { Slot(1, 1) }));
        Assert.Empty(GoalLists.Dedupe(new[] { Slot(1, 1) }, new[] { Slot(1, 1) }));
    }
}
