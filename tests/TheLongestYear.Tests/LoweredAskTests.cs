using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;

namespace TheLongestYear.Tests;

/// <summary>Which goal lines show "13 (27)": the ones the theme week discount lowered, before
/// the pick on the hub card and during the week everywhere the ask is shown.</summary>
public class LoweredAskTests
{
    private static BonusSlot Slot(int bundle, int ingredient, int stack, int original = 0, string id = "(O)192", int quality = 0)
        => new() { BundleIndex = bundle, IngredientIndex = ingredient, ItemId = id, Stack = stack, OriginalStack = original, Quality = quality };

    [Theory]
    [InlineData(0.5, 13, 27)]
    [InlineData(0.25, 20, 27)]
    public void Preview_carries_the_full_ask_of_a_lowered_line(double discount, int lowered, int full)
    {
        var shown = WeeklyGoalDiscount.Preview(new List<BonusSlot> { Slot(0, 0, 27) }, new List<BonusSlot>(), discount);

        Assert.Equal(lowered, shown[0].Stack);
        Assert.Equal(full, LoweredAsk.FullAsk(shown[0]));
    }

    /// <summary>Hard and Extreme give no discount, and a line at or under the floor is never lowered:
    /// neither shows a full ask.</summary>
    [Fact]
    public void No_discount_or_a_small_line_shows_no_full_ask()
    {
        var sample = new List<BonusSlot> { Slot(0, 0, 27), Slot(0, 1, 10), Slot(0, 2, 11) };

        var hard = WeeklyGoalDiscount.Preview(sample, new List<BonusSlot>(), 0.0);
        var easy = WeeklyGoalDiscount.Preview(sample, new List<BonusSlot>(), 0.5);

        Assert.All(hard, s => Assert.Null(LoweredAsk.FullAsk(s)));
        Assert.Equal(new int?[] { 27, null, 11 }, easy.Select(LoweredAsk.FullAsk));
    }

    /// <summary>Reopening the hub mid-week: the line is already lowered on the board, and the card
    /// still shows the real full ask, not the lowered one as the full one.</summary>
    [Fact]
    public void Preview_mid_week_shows_the_real_full_ask()
    {
        var current = new List<BonusSlot> { Slot(0, 0, 13, original: 27) };

        var shown = WeeklyGoalDiscount.Preview(new List<BonusSlot> { Slot(0, 0, 13) }, current, 0.5);

        Assert.Equal(13, shown[0].Stack);
        Assert.Equal(27, LoweredAsk.FullAsk(shown[0]));
    }

    [Fact]
    public void A_donated_line_shows_no_full_ask()
    {
        BonusSlot slot = Slot(0, 0, 13, original: 27);
        slot.Deposited = true;

        Assert.Null(LoweredAsk.FullAsk(slot));
        Assert.Null(LoweredAsk.FullAsk(null));
    }

    [Fact]
    public void Apply_then_revert_turns_the_full_ask_on_and_off()
    {
        var board = new Dictionary<string, string> { ["Pantry/0"] = "Spring Crops/O 465 20/192 27 0 24 8 0/0/2//Spring Crops" };
        var slots = new List<BonusSlot> { Slot(0, 0, 27), Slot(0, 1, 8, id: "(O)24") };

        WeeklyGoalDiscount.Apply(slots, board, 0.5);
        Assert.Equal(27, LoweredAsk.AtLine(slots, 0, 0, 13));
        Assert.Null(LoweredAsk.AtLine(slots, 0, 1, 8));

        WeeklyGoalDiscount.Revert(slots, board, _ => false);
        Assert.Null(LoweredAsk.AtLine(slots, 0, 0, 27));
    }

    /// <summary>A line another mod rewrote no longer holds the lowered ask: no full ask against a
    /// number the board is not asking for.</summary>
    [Fact]
    public void A_line_rewritten_underneath_shows_no_full_ask()
    {
        var slots = new List<BonusSlot> { Slot(3, 2, 13, original: 27) };

        Assert.Equal(27, LoweredAsk.AtLine(slots, 3, 2, 13));
        Assert.Null(LoweredAsk.AtLine(slots, 3, 2, 30));
        Assert.Null(LoweredAsk.AtLine(slots, 3, 1, 13));
        Assert.Null(LoweredAsk.AtLine(slots, 4, 2, 13));
    }

    [Fact]
    public void ForItem_matches_by_bundle_and_item_with_or_without_the_qualifier()
    {
        var slots = new List<BonusSlot> { Slot(3, 2, 13, original: 27, id: "(O)192") };

        Assert.Equal(27, LoweredAsk.ForItem(slots, 3, "(O)192", 0, 13));
        Assert.Equal(27, LoweredAsk.ForItem(slots, 3, "192", 0, 13));
        Assert.Null(LoweredAsk.ForItem(slots, 3, "(O)24", 0, 13));
        Assert.Null(LoweredAsk.ForItem(slots, 2, "(O)192", 0, 13));
        Assert.Null(LoweredAsk.ForItem(slots, 3, "", 0, 13));
    }

    /// <summary>The same item on two lines of one bundle at different qualities, both asking the
    /// same stack: each quality reads its own line's full ask, not whichever line comes first.</summary>
    [Fact]
    public void ForItem_tells_two_lines_of_the_same_item_apart_by_quality()
    {
        var slots = new List<BonusSlot>
        {
            Slot(3, 0, 5, original: 10, quality: 0),
            Slot(3, 1, 5, original: 7, quality: 2),
        };

        Assert.Equal(10, LoweredAsk.ForItem(slots, 3, "(O)192", 0, 5));
        Assert.Equal(7, LoweredAsk.ForItem(slots, 3, "(O)192", 2, 5));
        Assert.Null(LoweredAsk.ForItem(slots, 3, "(O)192", 1, 5));
    }

    /// <summary>A double week's second list counts too.</summary>
    [Fact]
    public void WeekSlots_reads_both_lists()
    {
        var run = new RunState();
        run.CurrentWeekBonusSlots.Add(Slot(0, 0, 13, original: 27));
        run.SecondWeekBonusSlots.Add(Slot(5, 1, 15, original: 31));

        List<BonusSlot> week = LoweredAsk.WeekSlots(run).ToList();

        Assert.Equal(2, week.Count);
        Assert.Equal(31, LoweredAsk.AtLine(week, 5, 1, 15));
        Assert.Empty(LoweredAsk.WeekSlots(null));
    }
}
