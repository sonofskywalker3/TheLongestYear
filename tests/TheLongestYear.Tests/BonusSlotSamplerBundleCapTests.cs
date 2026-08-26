using System;
using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>
/// Jeff, 2026-08-26, from emmalution's stream: "the weekly theme was asking for 3 items from a
/// bundle with 2 slots."
///
/// A bundle that only requires SOME of its listed items still puts every open line into the goal
/// pool, so the sampler could hand out more goals in one bundle than the bundle can ever accept.
/// Once the required count is met the bundle completes and the extra line stops taking donations.
/// Before 0.14.0 that was merely odd-looking: vanilla's blanket flip on completion ticked the
/// leftover goal anyway. Since 0.14.0 a goal needs a REAL deposit, so the extra goal is impossible
/// and the week can never be completed. The sampler must never ask for more than a bundle needs.
/// </summary>
public class BonusSlotSamplerBundleCapTests
{
    private static BonusSlot Slot(int bundle, int ingredient, string itemId) =>
        new() { BundleIndex = bundle, IngredientIndex = ingredient, ItemId = itemId, Stack = 1 };

    private static Rarity Common(string _) => Rarity.Common;

    /// <summary>One bundle, five open lines, but it only needs two more items.</summary>
    private static IReadOnlyList<BonusSlot> FiveLineBundle() => new List<BonusSlot>
    {
        Slot(1, 0, "(O)16"), Slot(1, 1, "(O)18"), Slot(1, 2, "(O)20"),
        Slot(1, 3, "(O)22"), Slot(1, 4, "(O)24"),
    };

    [Fact]
    public void Never_asks_for_more_lines_than_the_bundle_still_needs()
    {
        var sampled = BonusSlotSampler.SampleSlots(
            runSeed: 12345, weekOfYear: 5, Theme.Foraging, FiveLineBundle(), Common, maxCount: 4,
            remainingNeedForBundle: _ => 2);

        Assert.Equal(2, sampled.Count);
        Assert.All(sampled, s => Assert.Equal(1, s.BundleIndex));
    }

    [Fact]
    public void Spends_the_remaining_goals_on_other_bundles()
    {
        var pool = FiveLineBundle().Concat(new[]
        {
            Slot(2, 0, "(O)78"), Slot(2, 1, "(O)80"), Slot(2, 2, "(O)82"),
        }).ToList();

        var sampled = BonusSlotSampler.SampleSlots(
            runSeed: 999, weekOfYear: 5, Theme.Foraging, pool, Common, maxCount: 4,
            remainingNeedForBundle: b => b == 1 ? 2 : 3);

        Assert.Equal(4, sampled.Count);
        Assert.Equal(2, sampled.Count(s => s.BundleIndex == 1));
        Assert.Equal(2, sampled.Count(s => s.BundleIndex == 2));
    }

    [Fact]
    public void A_bundle_that_needs_nothing_contributes_no_goals()
    {
        var sampled = BonusSlotSampler.SampleSlots(
            runSeed: 4, weekOfYear: 5, Theme.Foraging, FiveLineBundle(), Common, maxCount: 4,
            remainingNeedForBundle: _ => 0);

        Assert.Empty(sampled);
    }

    [Fact]
    public void No_cap_supplied_keeps_the_old_behaviour()
    {
        // Back-compat: callers that cannot work out the need (previews on partial data) still get
        // a full sample rather than nothing.
        var sampled = BonusSlotSampler.SampleSlots(
            runSeed: 12345, weekOfYear: 5, Theme.Foraging, FiveLineBundle(), Common, maxCount: 4);

        Assert.Equal(4, sampled.Count);
    }

    [Fact]
    public void Still_deterministic_for_the_same_inputs()
    {
        var a = BonusSlotSampler.SampleSlots(7, 3, Theme.Fishing, FiveLineBundle(), Common, 2, _ => 2);
        var b = BonusSlotSampler.SampleSlots(7, 3, Theme.Fishing, FiveLineBundle(), Common, 2, _ => 2);

        Assert.Equal(
            a.Select(s => (s.BundleIndex, s.IngredientIndex)),
            b.Select(s => (s.BundleIndex, s.IngredientIndex)));
    }

    [Fact]
    public void Never_returns_the_same_slot_twice()
    {
        var sampled = BonusSlotSampler.SampleSlots(
            runSeed: 55, weekOfYear: 6, Theme.Foraging, FiveLineBundle(), Common, maxCount: 5,
            remainingNeedForBundle: _ => 5);

        Assert.Equal(sampled.Count, sampled.Select(s => (s.BundleIndex, s.IngredientIndex)).Distinct().Count());
    }
}
