using System;
using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class CappedAsksTests
{
    private static PoolItem Item(string id) => new(id, 50, 3, Array.Empty<Season>(), Array.Empty<string>());

    [Theory]
    [InlineData(DifficultyStep.Easy, 0)]
    [InlineData(DifficultyStep.Normal, 1)]
    [InlineData(DifficultyStep.Hard, 2)]
    [InlineData(DifficultyStep.Extreme, 3)]
    public void Allowance_follows_stack_size(DifficultyStep step, int expected)
        => Assert.Equal(expected, CappedAsks.BoardAllowance(step));

    [Fact]
    public void A_capped_slot_asks_for_one()
    {
        Assert.Equal(1, CappedAsks.ClampStack("(O)MysteryBox", 5));
        Assert.Equal(1, CappedAsks.ClampStack("74", 3));   // bare id normalises
        Assert.Equal(7, CappedAsks.ClampStack("(O)60", 7));
    }

    [Fact]
    public void ClampBundle_lowers_only_capped_slots()
    {
        var spec = new BundleSpec("Bulletin Board", 3, "Helper's", "Helper's", "O 1 1", 0, 2,
            new List<BundleSlotSpec> { new("(O)PrizeTicket", 1, 0), new("(O)MysteryBox", 5, 0) });
        BundleSpec clamped = CappedAsks.ClampBundle(spec);
        Assert.Equal(new[] { 1, 1 }, clamped.Slots.Select(s => s.Stack));
        Assert.Same(clamped, CappedAsks.ClampBundle(clamped));
    }

    [Fact]
    public void Enforce_keeps_what_the_board_has_left_and_swaps_the_rest()
    {
        var chosen = new List<PoolItem> { Item("(O)74"), Item("(O)60"), Item("(O)MysteryBox") };
        var candidates = new List<PoolItem> { Item("(O)74"), Item("(O)60"), Item("(O)62"), Item("(O)MysteryBox"), Item("(O)64") };
        var remaining = new Dictionary<string, int> { ["(O)74"] = 1, ["(O)MysteryBox"] = 0 };
        CappedAsks.Enforce(chosen, candidates, remaining, new Random(1));
        Assert.Equal(3, chosen.Count);
        Assert.Contains(chosen, c => c.ItemId == "(O)74");
        Assert.DoesNotContain(chosen, c => c.ItemId == "(O)MysteryBox");
    }

    [Fact]
    public void CountOnBoard_sums_stacks()
    {
        var a = new BundleSpec("R", 1, "A", "A", "O 1 1", 0, 1, new List<BundleSlotSpec> { new("(O)74", 1, 0) });
        var b = new BundleSpec("R", 2, "B", "B", "O 1 1", 0, 1, new List<BundleSlotSpec> { new("(O)74", 2, 0) });
        Assert.Equal(3, CappedAsks.CountOnBoard("(O)74", new[] { a, b }));
    }

    [Fact]
    public void RepairBundleValue_lowers_a_capped_ask_to_one_and_leaves_the_rest()
    {
        const string value = "Helper's/O TreasureTotem 5/PrizeTicket 5 0 MysteryBox 5 0/4/2//Helper's";
        Assert.Equal(
            "Helper's/O TreasureTotem 5/PrizeTicket 5 0 MysteryBox 1 0/4/2//Helper's",
            CappedAsks.RepairBundleValue(value));
    }

    [Fact]
    public void RepairBundleValue_returns_null_when_nothing_needs_lowering()
    {
        Assert.Null(CappedAsks.RepairBundleValue("Helper's/O TreasureTotem 5/PrizeTicket 5 0 MysteryBox 1 0/4/2//Helper's"));
        Assert.Null(CappedAsks.RepairBundleValue("Vault/-1 2500 2500/4/1"));
    }
}
