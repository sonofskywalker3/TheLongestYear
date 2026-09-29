using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;

namespace TheLongestYear.Tests;

/// <summary>Applying and reverting the theme week discount on a board (spec
/// 2026-09-29-theme-week-discount): only the week's goal lines change, and only for that week.</summary>
public class WeeklyGoalDiscountBoardTests
{
    private const string CropsKey = "Pantry/0";
    private const string FishKey = "Fish Tank/5";
    private const string Crops = "Spring Crops/O 465 20/24 31 0 188 14 1 190 8 0 192 40 2/0/3//Spring Crops";
    private const string Fish = "River Fish/O 685 30/145 25 0 143 12 0/6/2//River Fish";

    private static Dictionary<string, string> Board() => new()
    {
        [CropsKey] = Crops,
        [FishKey] = Fish,
    };

    private static BonusSlot Slot(int bundle, int ingredient, string id, int stack, int quality = 0)
        => new() { BundleIndex = bundle, IngredientIndex = ingredient, ItemId = id, Stack = stack, Quality = quality };

    [Fact]
    public void StackAt_and_WithStack_address_one_ingredient()
    {
        Assert.Equal(14, WeeklyGoalDiscount.StackAt(Crops, 1));
        Assert.Null(WeeklyGoalDiscount.StackAt(Crops, 4));
        Assert.Equal(
            "Spring Crops/O 465 20/24 31 0 188 10 1 190 8 0 192 40 2/0/3//Spring Crops",
            WeeklyGoalDiscount.WithStack(Crops, 1, 10));
        Assert.Null(WeeklyGoalDiscount.WithStack(Crops, 1, 14));
    }

    [Fact]
    public void Apply_lowers_only_the_goal_lines_and_leaves_quality_alone()
    {
        var board = Board();
        var slots = new List<BonusSlot> { Slot(0, 0, "(O)24", 31), Slot(0, 3, "(O)192", 40, 2), Slot(0, 2, "(O)190", 8) };

        var edits = WeeklyGoalDiscount.Apply(slots, board, 0.25);
        var updates = WeeklyGoalDiscount.ApplyEdits(board, edits);

        Assert.Equal(
            "Spring Crops/O 465 20/24 23 0 188 14 1 190 8 0 192 30 2/0/3//Spring Crops",
            updates[CropsKey]);
        Assert.False(updates.ContainsKey(FishKey));
        Assert.Equal(new[] { 23, 30, 8 }, slots.Select(s => s.Stack));
        Assert.Equal(new[] { 31, 40, 0 }, slots.Select(s => s.OriginalStack));
        Assert.Equal(2, slots[1].Quality);
    }

    [Fact]
    public void Apply_with_no_discount_changes_nothing()
    {
        var slots = new List<BonusSlot> { Slot(0, 0, "(O)24", 31) };

        Assert.Empty(WeeklyGoalDiscount.Apply(slots, Board(), 0.0));
        Assert.Equal(31, slots[0].Stack);
        Assert.Equal(0, slots[0].OriginalStack);
    }

    [Fact]
    public void Apply_twice_does_not_discount_a_line_twice()
    {
        var board = Board();
        var slots = new List<BonusSlot> { Slot(5, 0, "(O)145", 25) };
        foreach (var kv in WeeklyGoalDiscount.ApplyEdits(board, WeeklyGoalDiscount.Apply(slots, board, 0.5)))
            board[kv.Key] = kv.Value;

        Assert.Empty(WeeklyGoalDiscount.Apply(slots, board, 0.5));
        Assert.Equal(12, slots[0].Stack);
        Assert.Equal(25, slots[0].OriginalStack);
    }

    [Fact]
    public void Apply_skips_a_slot_whose_line_now_names_another_item()
    {
        var slots = new List<BonusSlot> { Slot(0, 0, "(O)400", 31) };

        Assert.Empty(WeeklyGoalDiscount.Apply(slots, Board(), 0.5));
        Assert.Equal(0, slots[0].OriginalStack);
    }

    [Fact]
    public void Revert_restores_exactly_what_apply_wrote()
    {
        var board = Board();
        var slots = new List<BonusSlot> { Slot(0, 0, "(O)24", 31), Slot(5, 0, "(O)145", 25) };
        foreach (var kv in WeeklyGoalDiscount.ApplyEdits(board, WeeklyGoalDiscount.Apply(slots, board, 0.5)))
            board[kv.Key] = kv.Value;

        var edits = WeeklyGoalDiscount.Revert(slots, board, _ => false);
        foreach (var kv in WeeklyGoalDiscount.ApplyEdits(board, edits))
            board[kv.Key] = kv.Value;

        Assert.Equal(Board(), board);
        Assert.Equal(new[] { 31, 25 }, slots.Select(s => s.Stack));
        Assert.All(slots, s => Assert.Equal(0, s.OriginalStack));
    }

    [Fact]
    public void Revert_leaves_a_donated_line_at_its_discounted_stack()
    {
        var board = Board();
        var slots = new List<BonusSlot> { Slot(0, 0, "(O)24", 31), Slot(5, 0, "(O)145", 25) };
        foreach (var kv in WeeklyGoalDiscount.ApplyEdits(board, WeeklyGoalDiscount.Apply(slots, board, 0.5)))
            board[kv.Key] = kv.Value;

        var edits = WeeklyGoalDiscount.Revert(slots, board, s => s.BundleIndex == 0);
        foreach (var kv in WeeklyGoalDiscount.ApplyEdits(board, edits))
            board[kv.Key] = kv.Value;

        Assert.Equal(15, WeeklyGoalDiscount.StackAt(board[CropsKey], 0));
        Assert.Equal(25, WeeklyGoalDiscount.StackAt(board[FishKey], 0));
        Assert.Equal(15, slots[0].Stack);
        Assert.All(slots, s => Assert.Equal(0, s.OriginalStack));
    }

    /// <summary>Another mod (Challenging CC Bundles) swapped the board mid-week: the line no longer
    /// holds the discounted value, so the revert must not write over it.</summary>
    [Fact]
    public void Revert_skips_a_line_whose_live_stack_changed_underneath()
    {
        var board = Board();
        var slots = new List<BonusSlot> { Slot(0, 0, "(O)24", 31) };
        foreach (var kv in WeeklyGoalDiscount.ApplyEdits(board, WeeklyGoalDiscount.Apply(slots, board, 0.5)))
            board[kv.Key] = kv.Value;
        board[CropsKey] = WeeklyGoalDiscount.WithStack(board[CropsKey], 0, 7)!;

        Assert.Empty(WeeklyGoalDiscount.Revert(slots, board, _ => false));
        Assert.Equal(7, WeeklyGoalDiscount.StackAt(board[CropsKey], 0));
        Assert.Equal(0, slots[0].OriginalStack);
    }

    /// <summary>The same edits applied to the stored engine board keep the load-time check matching,
    /// after the apply and after the revert (otherwise a mid-week save drops to the legacy path).</summary>
    [Fact]
    public void Edits_mirrored_into_the_stored_board_keep_the_manifest_check_matching()
    {
        var live = Board();
        // The stored copy can differ in the display name only (a Content Patcher rename).
        var stored = new Dictionary<string, string>
        {
            [CropsKey] = Crops.Replace("//Spring Crops", "//Spring Crop Things"),
            [FishKey] = Fish,
        };
        var slots = new List<BonusSlot> { Slot(0, 0, "(O)24", 31), Slot(5, 1, "(O)143", 12) };

        var edits = WeeklyGoalDiscount.Apply(slots, live, 0.5);
        foreach (var kv in WeeklyGoalDiscount.ApplyEdits(live, edits)) live[kv.Key] = kv.Value;
        foreach (var kv in WeeklyGoalDiscount.ApplyEdits(stored, edits)) stored[kv.Key] = kv.Value;
        Assert.True(EngineManifestCheck.MatchesIgnoringDisplayName(stored, live));
        Assert.NotEqual(Crops, live[CropsKey]);

        var back = WeeklyGoalDiscount.Revert(slots, live, _ => false);
        foreach (var kv in WeeklyGoalDiscount.ApplyEdits(live, back)) live[kv.Key] = kv.Value;
        foreach (var kv in WeeklyGoalDiscount.ApplyEdits(stored, back)) stored[kv.Key] = kv.Value;
        Assert.True(EngineManifestCheck.MatchesIgnoringDisplayName(stored, live));
        Assert.Equal(Crops, live[CropsKey]);
    }

    [Fact]
    public void ApplyEdits_ignores_keys_the_board_does_not_have()
    {
        var edits = new[] { new WeeklyGoalDiscount.StackEdit("Vault/23", 0, 10) };

        Assert.Empty(WeeklyGoalDiscount.ApplyEdits(Board(), edits));
    }
}

/// <summary>The planning hub's preview shows the discounted numbers before the pick (Jeff, 2026-09-29).</summary>
public class WeeklyGoalDiscountPreviewTests
{
    private static BonusSlot Slot(int bundle, int ingredient, int stack, int original = 0)
        => new() { BundleIndex = bundle, IngredientIndex = ingredient, ItemId = "(O)24", Stack = stack, OriginalStack = original };

    [Fact]
    public void Preview_shows_the_discounted_stack_without_touching_the_sample()
    {
        var sample = new List<BonusSlot> { Slot(0, 0, 31), Slot(0, 1, 8) };

        var shown = WeeklyGoalDiscount.Preview(sample, new List<BonusSlot>(), 0.25);

        Assert.Equal(new[] { 23, 8 }, shown.Select(s => s.Stack));
        Assert.Equal(new[] { 31, 8 }, sample.Select(s => s.Stack));
        Assert.Equal("(O)24", shown[0].ItemId);
    }

    /// <summary>Reopening the hub mid-week: this week's goal lines are already lowered on the board,
    /// so the preview starts from their full ask instead of lowering them twice.</summary>
    [Fact]
    public void Preview_starts_a_line_already_lowered_this_week_from_its_full_ask()
    {
        var current = new List<BonusSlot> { Slot(0, 0, 23, original: 31) };
        var sample = new List<BonusSlot> { Slot(0, 0, 23) };

        var shown = WeeklyGoalDiscount.Preview(sample, current, 0.25);

        Assert.Equal(23, shown[0].Stack);
        Assert.Equal(15, WeeklyGoalDiscount.Preview(sample, current, 0.5)[0].Stack);
        Assert.Equal(31, WeeklyGoalDiscount.Preview(sample, current, 0.0)[0].Stack);
    }
}
