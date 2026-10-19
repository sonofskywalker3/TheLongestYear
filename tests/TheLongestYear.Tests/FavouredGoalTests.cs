using TheLongestYear.Core;

namespace TheLongestYear.Tests;

/// <summary>The favoured goal (spec 2026-10-19-focus-cards, "The favoured goal (x2)"): at most one
/// line per theme card, the hardest hard roll, pays double per-item JP.</summary>
public class FavouredGoalTests
{
    private static List<BonusSlot> Slots(params (string id, int? effort)[] lines)
        => lines.Select((l, i) => new BonusSlot { BundleIndex = 0, IngredientIndex = i, ItemId = l.id }).ToList();

    private readonly Dictionary<string, int?> _efforts = new();

    private List<BonusSlot> SlotsWith(params (string id, int? effort)[] lines)
    {
        foreach (var (id, effort) in lines) _efforts[id] = effort;
        return Slots(lines);
    }

    private Func<string, int?> Effort(List<BonusSlot> _) => id => _efforts.TryGetValue(id, out int? e) ? e : null;

    [Fact]
    public void Hardest_hard_roll_is_the_one_favoured()
    {
        var g = SlotsWith(("a", 3), ("b", 7), ("c", 9));
        FavouredGoal.Mark(g, Effort(g));
        Assert.Equal(new[] { false, false, true }, g.Select(s => s.Favoured));
    }

    [Fact]
    public void No_hard_roll_no_favourite()
    {
        var g = SlotsWith(("a", 2), ("b", 5));
        FavouredGoal.Mark(g, Effort(g));
        Assert.DoesNotContain(g, s => s.Favoured);
    }

    [Fact]
    public void A_stretch_line_qualifies_even_when_easy()
    {
        var g = SlotsWith(("a1", 1));
        g[0].Stretch = true;
        FavouredGoal.Mark(g, Effort(g));
        Assert.True(g[0].Favoured);
    }

    [Fact]
    public void A_stretch_line_with_no_effort_ranks_below_any_numbered_hard_roll()
    {
        var g = SlotsWith(("n", null), ("h", 6));
        g[0].Stretch = true;
        FavouredGoal.Mark(g, Effort(g));
        Assert.Equal(new[] { false, true }, g.Select(s => s.Favoured));
    }

    [Fact]
    public void Ties_go_to_the_first_line()
    {
        var g = SlotsWith(("t1", 8), ("t2", 8));
        FavouredGoal.Mark(g, Effort(g));
        Assert.Equal(new[] { true, false }, g.Select(s => s.Favoured));
    }

    [Fact]
    public void Remarking_after_dedupe_moves_the_flag()
    {
        var g = SlotsWith(("r1", 9), ("r2", 7));
        FavouredGoal.Mark(g, Effort(g));
        g.RemoveAt(0);
        FavouredGoal.Mark(g, Effort(g));
        Assert.True(g[0].Favoured);
    }

    [Fact]
    public void Marking_clears_a_stale_flag()
    {
        var g = SlotsWith(("s1", 2), ("s2", 3));
        g[1].Favoured = true;
        FavouredGoal.Mark(g, Effort(g));
        Assert.DoesNotContain(g, s => s.Favoured);
    }

    [Fact]
    public void Hard_roll_test_is_stretch_or_a_hard_effort()
    {
        Assert.True(FavouredGoal.IsHardRoll(new BonusSlot(), 6));
        Assert.False(FavouredGoal.IsHardRoll(new BonusSlot(), 5));
        Assert.False(FavouredGoal.IsHardRoll(new BonusSlot(), null));
        Assert.True(FavouredGoal.IsHardRoll(new BonusSlot { Stretch = true }, null));
    }

    [Fact]
    public void Apply_doubles_only_when_favoured()
    {
        Assert.Equal(30, FavouredGoal.Apply(15, true));
        Assert.Equal(15, FavouredGoal.Apply(15, false));
        Assert.Equal(0, FavouredGoal.Apply(0, true));
    }
}
