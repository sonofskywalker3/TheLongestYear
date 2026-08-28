using System;
using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class BonusSlotSamplerTests
{
    // (O)426 Goat Cheese — confirmed present in CcItemCatalog.EarlyGameAvoid; "(O)24" Parsnip is
    // not in that set.
    private const string AVOID_ID = "(O)426";

    private static BonusSlot Slot(string id, int bundleIndex = 0, int ingredientIndex = 0,
        int stack = 1, int quality = 0, string bundleName = "B")
        => new() { ItemId = id, BundleIndex = bundleIndex, IngredientIndex = ingredientIndex,
                   Stack = stack, Quality = quality, BundleName = bundleName };

    private static Rarity CommonRarity(string _) => Rarity.Common;

    [Fact]
    public void Sample_is_deterministic_for_same_inputs()
    {
        var pool = new List<BonusSlot>
        {
            Slot("(O)1", 0, 0), Slot("(O)2", 0, 1), Slot("(O)3", 1, 0),
            Slot("(O)4", 1, 1), Slot("(O)5", 2, 0),
        };
        var a = BonusSlotSampler.SampleSlots(42, 5, Theme.Farming, pool, CommonRarity, 3);
        var b = BonusSlotSampler.SampleSlots(42, 5, Theme.Farming, pool, CommonRarity, 3);
        Assert.Equal(
            a.Select(s => (s.BundleIndex, s.IngredientIndex)),
            b.Select(s => (s.BundleIndex, s.IngredientIndex)));
        Assert.Equal(3, a.Count);
    }

    [Fact]
    public void One_goal_per_item_id_even_when_multiple_open_slots_share_it()
    {
        var pool = new List<BonusSlot>
        {
            Slot("(O)24", 0, 0, stack: 1),          // Spring Crops parsnip
            Slot("(O)24", 3, 1, stack: 5, quality: 2), // Quality Crops parsnip
        };
        var sample = BonusSlotSampler.SampleSlots(42, 5, Theme.Farming, pool, CommonRarity, 4);
        Assert.Single(sample);
        Assert.Equal("(O)24", sample[0].ItemId);
    }

    [Fact]
    public void Slot_choice_among_duplicates_is_seeded_deterministic()
    {
        var pool = new List<BonusSlot>
        {
            Slot("(O)24", 0, 0, stack: 1),
            Slot("(O)24", 3, 1, stack: 5, quality: 2),
        };
        var a = BonusSlotSampler.SampleSlots(42, 5, Theme.Farming, pool, CommonRarity, 4).Single();
        var b = BonusSlotSampler.SampleSlots(42, 5, Theme.Farming, pool, CommonRarity, 4).Single();
        Assert.Equal((a.BundleIndex, a.IngredientIndex), (b.BundleIndex, b.IngredientIndex));

        // Across many (seed, week) combos BOTH slots must occur — i.e. the pick is random,
        // not a fixed min/max rule.
        var seen = new HashSet<(int, int)>();
        for (int seed = 0; seed < 40; seed++)
            foreach (var s in BonusSlotSampler.SampleSlots(seed, 5, Theme.Farming, pool, CommonRarity, 4))
                seen.Add((s.BundleIndex, s.IngredientIndex));
        Assert.Contains((0, 0), seen);
        Assert.Contains((3, 1), seen);
    }

    [Fact]
    public void Pool_smaller_than_max_returns_whole_pool()
    {
        var pool = new List<BonusSlot> { Slot("(O)1"), Slot("(O)2", 1) };
        var sample = BonusSlotSampler.SampleSlots(42, 5, Theme.Farming, pool, CommonRarity, 7);
        Assert.Equal(2, sample.Count);
    }

    [Fact]
    public void Empty_pool_returns_empty()
    {
        var sample = BonusSlotSampler.SampleSlots(
            42, 5, Theme.Farming, new List<BonusSlot>(), CommonRarity, 4);
        Assert.Empty(sample);
    }

    [Fact]
    public void Weeks_1_and_2_avoid_early_game_infrastructure_items()
    {
        // AVOID_ID must be in CcItemCatalog.EarlyGameAvoid; (O)24 Parsnip must not be.
        var pool = new List<BonusSlot> { Slot(AVOID_ID, 0, 0), Slot("(O)24", 1, 0) };
        for (int seed = 0; seed < 20; seed++)
        {
            var sample = BonusSlotSampler.SampleSlots(seed, 2, Theme.Farming, pool, CommonRarity, 1);
            Assert.Equal("(O)24", sample.Single().ItemId);
        }
        // Week 3+ unlocks the full pool: with maxCount 2, both appear.
        var late = BonusSlotSampler.SampleSlots(42, 3, Theme.Farming, pool, CommonRarity, 2);
        Assert.Equal(2, late.Count);
    }

    /// <summary>Jeff, 2026-08-29: "farming week 2 this loop was 3 fruits! way too many for a
    /// second week of spring goal. limit fruit tree stuff to 1 per theme" (per theme list, not
    /// per gate). The fruit-tree group is passed in from Data/FruitTrees so modded trees count.</summary>
    [Fact]
    public void At_most_one_goal_from_the_limited_group_per_sample()
    {
        var fruit = new HashSet<string>(StringComparer.Ordinal) { "(O)613", "(O)634", "(O)635" };
        var pool = new List<BonusSlot>
        {
            Slot("(O)613", 5, 6), Slot("(O)634", 5, 7), Slot("(O)635", 5, 8),   // Artisan's fruit lines
            Slot("(O)24", 0, 0), Slot("(O)190", 0, 1),                          // Spring Crops
        };
        for (int seed = 0; seed < 30; seed++)
        {
            var sample = BonusSlotSampler.SampleSlots(seed, 6, Theme.Farming, pool, CommonRarity, 5,
                caps: new[] { new GoalGroupCap(fruit, 1) });
            Assert.True(sample.Count(s => fruit.Contains(s.ItemId)) <= 1, $"seed {seed} drew more than one fruit");
            Assert.Equal(3, sample.Count);   // two crops plus exactly one fruit
        }
        // No group: the old behaviour, all five.
        Assert.Equal(5, BonusSlotSampler.SampleSlots(1, 6, Theme.Farming, pool, CommonRarity, 5).Count);
    }

    /// <summary>Jeff, 2026-08-28: "why is the fishing goal all crab pot stuff and NO FISH!
    /// maybe limit it to 1 or 2 crab pot picks max per week". Crab-pot catches (Data/Fish trap
    /// rows) are a second capped group next to the tree fruit; both caps hold in one sample.</summary>
    [Fact]
    public void Each_capped_group_holds_independently_in_one_sample()
    {
        var fruit = new HashSet<string>(StringComparer.Ordinal) { "(O)613", "(O)634" };
        var trap = new HashSet<string>(StringComparer.Ordinal) { "(O)715", "(O)716", "(O)717", "(O)720" };
        var pool = new List<BonusSlot>
        {
            Slot("(O)715", 11, 0), Slot("(O)716", 11, 1), Slot("(O)717", 11, 2), Slot("(O)720", 11, 5),
            Slot("(O)613", 5, 6), Slot("(O)634", 5, 7),
            Slot("(O)145", 7, 0), Slot("(O)143", 7, 1),
        };
        var caps = new[] { new GoalGroupCap(fruit, 1), new GoalGroupCap(trap, 1) };
        for (int seed = 0; seed < 30; seed++)
        {
            var sample = BonusSlotSampler.SampleSlots(seed, 6, Theme.Fishing, pool, CommonRarity, 6, caps: caps);   // week 6: past the early-game filter
            Assert.True(sample.Count(s => trap.Contains(s.ItemId)) <= 1, $"seed {seed}: more than one crab-pot goal");
            Assert.True(sample.Count(s => fruit.Contains(s.ItemId)) <= 1, $"seed {seed}: more than one fruit goal");
            Assert.Equal(4, sample.Count);   // two fish, one fruit, one crab-pot catch
        }
    }

    [Fact]
    public void Sample_is_invariant_to_pool_input_order()
    {
        var pool = new List<BonusSlot>
        {
            Slot("(O)24", 0, 0, stack: 1),
            Slot("(O)24", 3, 1, stack: 5, quality: 2),
            Slot("(O)188", 1, 0), Slot("(O)190", 2, 0),
        };
        var reversed = Enumerable.Reverse(pool).ToList();
        var a = BonusSlotSampler.SampleSlots(42, 5, Theme.Farming, pool, CommonRarity, 4);
        var b = BonusSlotSampler.SampleSlots(42, 5, Theme.Farming, reversed, CommonRarity, 4);
        Assert.Equal(
            a.Select(s => (s.ItemId, s.BundleIndex, s.IngredientIndex)),
            b.Select(s => (s.ItemId, s.BundleIndex, s.IngredientIndex)));
    }
}
