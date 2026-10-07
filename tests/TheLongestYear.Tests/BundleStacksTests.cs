using System.Collections.Generic;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>Reddit report 2026-10-07: the Bundle Log showed fewer Tulips than the CC asked for. The Log
/// read stacks captured at save load; the theme week discount rewrites the board mid-session. The Log
/// now reads the live board through <see cref="BundleStacks.ForBundle"/>.</summary>
public class BundleStacksTests
{
    [Fact]
    public void Of_keeps_the_largest_stack_per_item_and_skips_categories()
    {
        var ingredients = BundleParsing.ParseIngredients("388 99 0 591 12 0 388 50 0 -5 1 0");

        Dictionary<string, int> stacks = BundleStacks.Of(ingredients);

        Assert.Equal(99, stacks["(O)388"]);
        Assert.Equal(12, stacks["(O)591"]);
        Assert.Equal(2, stacks.Count);
    }

    [Fact]
    public void ForBundle_reads_the_board_entry_with_that_index()
    {
        var board = new Dictionary<string, string>
        {
            ["Pantry/0"] = "Spring Crops/O 465 20/591 12 0 24 1 0/0/2//Spring Crops",
            ["Crafts Room/13"] = "Spring Foraging/O 495 30/591 30 0/0/1//Spring Foraging",
        };

        IReadOnlyDictionary<string, int>? stacks = BundleStacks.ForBundle(board, 13);

        Assert.NotNull(stacks);
        Assert.Equal(30, stacks!["(O)591"]);
    }

    [Fact]
    public void ForBundle_sees_a_stack_restored_after_a_discount()
    {
        var board = new Dictionary<string, string> { ["Pantry/0"] = "Spring Crops/O 465 20/591 12 0/0/1//Spring Crops" };
        string restored = WeeklyGoalDiscount.WithStack(board["Pantry/0"], 0, 16)!;
        board["Pantry/0"] = restored;

        Assert.Equal(16, BundleStacks.ForBundle(board, 0)!["(O)591"]);
    }

    [Fact]
    public void ForBundle_is_null_when_the_index_is_missing()
    {
        var board = new Dictionary<string, string> { ["Pantry/0"] = "Spring Crops/O 465 20/591 12 0/0/1//Spring Crops" };

        Assert.Null(BundleStacks.ForBundle(board, 7));
        Assert.Null(BundleStacks.ForBundle(null, 0));
    }
}
