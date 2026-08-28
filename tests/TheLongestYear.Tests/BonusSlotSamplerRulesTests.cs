using System;
using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>Rules A, B and E of the activity-themes spec (2026-08-28): due lines first, filler
/// under the season allowance and one per bundle, weights by effort tier and season.</summary>
public class BonusSlotSamplerRulesTests
{
    private static BonusSlot Slot(string id, int bundle, int line, bool due)
        => new() { ItemId = id, BundleIndex = bundle, IngredientIndex = line, BundleName = $"B{bundle}", Due = due };

    private static Rarity Common(string _) => Rarity.Common;

    private static GoalSamplingRules Rules(Season s, int filler, Func<string, int?>? effort = null)
        => new(s, filler, effort ?? (_ => null));

    private static List<BonusSlot> OneBundle() => new()
    {
        Slot("(O)1", 0, 0, true), Slot("(O)2", 0, 1, true),
        Slot("(O)3", 0, 2, false), Slot("(O)4", 0, 3, false), Slot("(O)5", 0, 4, false),
        Slot("(O)6", 0, 5, false), Slot("(O)7", 0, 6, false), Slot("(O)8", 0, 7, false),
    };

    private static int? EightEfforts(string id) => id switch
    {
        "(O)1" => 1, "(O)2" => 1, "(O)3" => 2, "(O)4" => 2, "(O)5" => 3, "(O)6" => 3, "(O)7" => 5, "(O)8" => 8, _ => null,
    };

    [Fact]
    public void Spring_takes_the_due_lines_only()
    {
        var sample = BonusSlotSampler.SampleSlots(1, 1, Theme.Mining, OneBundle(), Common, 4, rules: Rules(Season.Spring, 0));
        Assert.Equal(2, sample.Count);
        Assert.All(sample, s => Assert.True(s.Due));
    }

    [Fact]
    public void Summer_adds_one_filler()
    {
        var sample = BonusSlotSampler.SampleSlots(1, 5, Theme.Mining, OneBundle(), Common, 5, rules: Rules(Season.Summer, 1));
        Assert.Equal(3, sample.Count);
        Assert.Equal(1, sample.Count(s => !s.Due));
    }

    [Fact]
    public void Winter_still_takes_one_filler_per_bundle()
    {
        var sample = BonusSlotSampler.SampleSlots(1, 13, Theme.Mining, OneBundle(), Common, 7,
            rules: Rules(Season.Winter, GoalSamplingRules.UnlimitedFiller));
        Assert.Equal(3, sample.Count);
    }

    [Fact]
    public void Fall_takes_two_fillers_when_they_are_spread_over_bundles()
    {
        var pool = new List<BonusSlot>
        {
            Slot("(O)1", 0, 0, true), Slot("(O)2", 0, 1, true),
            Slot("(O)3", 0, 2, false), Slot("(O)4", 1, 0, false), Slot("(O)5", 2, 0, false), Slot("(O)6", 2, 1, false),
        };
        var sample = BonusSlotSampler.SampleSlots(1, 9, Theme.Mining, pool, Common, 6, rules: Rules(Season.Fall, 2));
        Assert.Equal(4, sample.Count);
        Assert.Equal(2, sample.Count(s => !s.Due));
        Assert.Equal(2, sample.Where(s => !s.Due).Select(s => s.BundleIndex).Distinct().Count());
    }

    [Fact]
    public void Spring_never_samples_an_extreme_id()
    {
        var pool = OneBundle();
        pool.ForEach(s => s.Due = true);
        for (int seed = 1; seed <= 50; seed++)
        {
            var sample = BonusSlotSampler.SampleSlots(seed, 1, Theme.Mining, pool, Common, 4, rules: Rules(Season.Spring, 0, EightEfforts));
            Assert.DoesNotContain(sample, s => s.ItemId == "(O)7" || s.ItemId == "(O)8");
        }
    }

    [Fact]
    public void Winter_prefers_the_extreme_ids_eight_to_one()
    {
        var pool = OneBundle();
        pool.ForEach(s => s.Due = true);
        int extremeFirst = 0;
        for (int seed = 1; seed <= 100; seed++)
        {
            var sample = BonusSlotSampler.SampleSlots(seed, 13, Theme.Mining, pool, Common, 1, rules: Rules(Season.Winter, 0, EightEfforts));
            if (sample[0].ItemId is "(O)7" or "(O)8") extremeFirst++;
        }
        // Weights: Easy 1 x2, Medium 2 x2, Hard 4 x2, Extreme 8 x2 = 30; the Extreme share is 16/30.
        Assert.InRange(extremeFirst, 40, 70);
    }

    [Fact]
    public void GoalWeighting_uses_the_price_bucket_when_no_effort_is_known()
    {
        var weights = GoalWeighting.For(new[] { "(O)1", "(O)2" }, Rules(Season.Spring, 0),
            id => id == "(O)2" ? Rarity.VeryRare : Rarity.Common);
        Assert.Equal(EffortTier.Easy, weights[0].Tier);
        Assert.Equal(EffortTier.Extreme, weights[1].Tier);
        Assert.Equal(0, weights[1].Weight);
    }

    [Fact]
    public void Legacy_call_without_rules_is_unchanged()
    {
        var a = BonusSlotSampler.SampleSlots(42, 5, Theme.Farming, OneBundle(), Common, 3);
        Assert.Equal(3, a.Count);
    }
}
