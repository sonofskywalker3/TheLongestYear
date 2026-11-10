using System;
using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class BoardProgressTests
{
    // Value: name / reward / ingredients / color / numberOfSlots / sprite / displayName.
    private static readonly Dictionary<string, string> Board = new()
    {
        ["Pantry/0"] = "Spring Crops/O 465 20/24 1 0 188 1 0 190 1 0 192 1 0/0/3//Spring Crops",   // pick 3 of 4
        ["Pantry/1"] = "Summer Crops/O 465 20/256 1 0 260 1 0/0///Summer Crops",                    // all 2
        ["Vault/23"] = "2,500g/O 220 3/-1 2500 2500/4///2,500g",
        ["Vault/24"] = "5,000g/O 369 30/-1 5000 5000/4///5,000g",
        ["Abandoned Joja Mart/36"] = "The Missing Bundle/O 74 1/348 1 1 807 1 0/6/5//The Missing Bundle",
    };

    private static IReadOnlyList<BundleProgress> Read(Dictionary<int, bool[]> slots, params int[] vaultPaid)
        => BoardProgress.Read(Board, i => slots.TryGetValue(i, out bool[]? s) ? s : null, vaultPaid);

    [Fact]
    public void A_pick_x_of_y_bundle_still_needs_x_minus_what_is_in()
    {
        IReadOnlyList<BundleProgress> board = Read(new() { [0] = new[] { true, false, false, false } });
        BundleProgress spring = board.Single(b => b.Index == 0);
        Assert.False(spring.Done);
        Assert.Equal(2, spring.StillNeeded);
    }

    [Fact]
    public void A_finished_pick_x_of_y_bundle_needs_nothing_more()
    {
        IReadOnlyList<BundleProgress> board = Read(new() { [0] = new[] { true, true, false, true } });
        BundleProgress spring = board.Single(b => b.Index == 0);
        Assert.True(spring.Done);
        Assert.Equal(0, spring.StillNeeded);
    }

    [Fact]
    public void A_vault_bundle_is_one_payment_paid_by_the_run_ledger_or_its_slot()
    {
        IReadOnlyList<BundleProgress> board = Read(new() { [24] = new[] { true, false, false } }, 23);
        Assert.True(board.Single(b => b.Index == 23).Done);
        Assert.True(board.Single(b => b.Index == 24).Done);

        IReadOnlyList<BundleProgress> unpaid = Read(new());
        Assert.Equal(1, unpaid.Single(b => b.Index == 23).StillNeeded);
    }

    [Fact]
    public void The_abandoned_joja_mart_is_left_out()
        => Assert.Equal(new[] { 0, 1, 23, 24 }, Read(new()).Select(b => b.Index));
}
