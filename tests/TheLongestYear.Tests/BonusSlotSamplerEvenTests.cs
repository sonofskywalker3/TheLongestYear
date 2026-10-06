using System.Linq;
using System.Collections.Generic;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class BonusSlotSamplerEvenTests
{
    private static BonusSlot Slot(string id, int bundle, int line, bool due = false)
        => new() { ItemId = id, BundleIndex = bundle, IngredientIndex = line, BundleName = $"B{bundle}", Due = due };
    private static Rarity Common(string _) => Rarity.Common;
    private static GoalSamplingRules Even(Season s) => new(s, 0, id => id.StartsWith("X") ? 10 : 1, Even: true);

    [Fact]
    public void Even_mode_can_draw_an_extreme_item_in_spring()
    {
        var pool = new[] { Slot("X1", 1, 0), Slot("E1", 2, 0) };
        bool sawExtreme = Enumerable.Range(1, 200).Any(seed =>
            BonusSlotSampler.SampleSlots(seed, 3, Theme.Mining, pool, Common, 1, rules: Even(Season.Spring))
                .Any(s => s.ItemId == "X1"));
        Assert.True(sawExtreme);
    }

    [Fact]
    public void Even_mode_does_not_put_due_lines_first()
    {
        var pool = new[] { Slot("D1", 1, 0, due: true), Slot("F1", 2, 0), Slot("F2", 3, 0), Slot("F3", 4, 0) };
        bool sawNoDue = Enumerable.Range(1, 200).Any(seed =>
            BonusSlotSampler.SampleSlots(seed, 3, Theme.Mining, pool, Common, 1, rules: Even(Season.Summer))
                .All(s => !s.Due));
        Assert.True(sawNoDue);
    }

    [Fact]
    public void Even_mode_keeps_the_bundle_need_cap()
    {
        var pool = new[] { Slot("A", 1, 0), Slot("B", 1, 1), Slot("C", 1, 2) };
        var got = BonusSlotSampler.SampleSlots(1, 3, Theme.Mining, pool, Common, 3,
            remainingNeedForBundle: _ => 1, rules: Even(Season.Summer));
        Assert.Single(got);
    }

    [Fact]
    public void Even_mode_keeps_group_caps()
    {
        var pool = new[] { Slot("T1", 1, 0), Slot("T2", 2, 0), Slot("Z", 3, 0) };
        var caps = new[] { new GoalGroupCap(new HashSet<string> { "T1", "T2" }, 1) };
        var got = BonusSlotSampler.SampleSlots(1, 3, Theme.Mining, pool, Common, 3, caps: caps, rules: Even(Season.Summer));
        Assert.True(got.Count(s => s.ItemId.StartsWith("T")) <= 1);
    }

    [Fact]
    public void Default_rules_are_unchanged()
    {
        var pool = new[] { Slot("D1", 1, 0, due: true), Slot("F1", 2, 0) };
        var a = BonusSlotSampler.SampleSlots(7, 3, Theme.Mining, pool, Common, 1, rules: new(Season.Spring, 0, _ => 1));
        Assert.All(a, s => Assert.True(s.Due));
    }
}
