using System;
using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class BonusItemSamplerTests
{
    // Default rarity lookup for tests that don't care about rarity: everything Common (max weight).
    // With every weight equal the weighted-draw degenerates to uniform sampling, so existing
    // assertions about pool membership / determinism still hold.
    private static readonly Func<string, Rarity> AllCommon = _ => Rarity.Common;

    // Two Fishing bundles so the theme has a real pool to sample from.
    private static IReadOnlyList<BundleRequirement> Bundles() => new[]
    {
        BundleRequirement.CreateSeasonal("Spring Forage", Theme.Fishing,
            new[] { "fish-A", "fish-B", "fish-C" }, Season.Spring),
        BundleRequirement.CreatePerItem("River", Theme.Fishing,
            new Dictionary<string, Season>
            {
                ["fish-D"] = Season.Spring,
                ["fish-E"] = Season.Summer,
                ["fish-F"] = Season.Spring
            }),
        // Same season, different theme — should NOT contribute to a Fishing sample.
        BundleRequirement.CreateSeasonal("Spring Forage Foraging", Theme.Foraging,
            new[] { "forage-1", "forage-2" }, Season.Spring)
    };

    [Fact]
    public void Sample_pulls_only_from_the_given_theme()
    {
        var sample = BonusItemSampler.SampleForTheme(
            runSeed: 42, weekOfYear: 0,
            theme: Theme.Fishing,
            currentSeason: Season.Spring,
            bundles: Bundles(),
            isObtainableInSeason: _ => true,
            rarityOf: AllCommon,
            maxCount: 10);

        Assert.DoesNotContain("forage-1", sample);
        Assert.DoesNotContain("forage-2", sample);
        // All Spring-in-play Fishing items: A, B, C (Seasonal) + D, F (PerItem pinned Spring).
        Assert.Equal(5, sample.Count);
        Assert.Contains("fish-A", sample); Assert.Contains("fish-B", sample); Assert.Contains("fish-C", sample);
        Assert.Contains("fish-D", sample); Assert.Contains("fish-F", sample);
    }

    [Fact]
    public void Sample_is_deterministic_for_same_inputs()
    {
        var first = BonusItemSampler.SampleForTheme(
            42, 3, Theme.Fishing, Season.Spring, Bundles(), _ => true, AllCommon, maxCount: 3);
        var second = BonusItemSampler.SampleForTheme(
            42, 3, Theme.Fishing, Season.Spring, Bundles(), _ => true, AllCommon, maxCount: 3);
        Assert.Equal(first, second);
    }

    [Fact]
    public void Sample_changes_with_seed()
    {
        var first  = BonusItemSampler.SampleForTheme(
            42, 3, Theme.Fishing, Season.Spring, Bundles(), _ => true, AllCommon, maxCount: 3);
        var second = BonusItemSampler.SampleForTheme(
            99, 3, Theme.Fishing, Season.Spring, Bundles(), _ => true, AllCommon, maxCount: 3);

        // Each pulls 3 of 5 — high probability the picks (or order) differ. Use the set-different
        // OR order-different signal so the test doesn't get flaky on a freak collision.
        bool differentPicks = !first.OrderBy(s => s).SequenceEqual(second.OrderBy(s => s));
        bool sameSetDifferentOrder = !first.SequenceEqual(second);
        Assert.True(differentPicks || sameSetDifferentOrder,
            $"Sample should differ between seeds. seed 42: [{string.Join(",", first)}], seed 99: [{string.Join(",", second)}]");
    }

    [Fact]
    public void Sample_respects_max_count()
    {
        var sample = BonusItemSampler.SampleForTheme(
            42, 0, Theme.Fishing, Season.Spring, Bundles(), _ => true, AllCommon, maxCount: 2);
        Assert.Equal(2, sample.Count);
    }

    [Fact]
    public void Empty_pool_returns_empty()
    {
        // No Mining bundles in our fixture -> nothing to sample.
        var sample = BonusItemSampler.SampleForTheme(
            42, 0, Theme.Mining, Season.Spring, Bundles(), _ => true, AllCommon, maxCount: 5);
        Assert.Empty(sample);
    }

    [Fact]
    public void Obtainability_predicate_filters_Percentage_pool()
    {
        var bundles = new[]
        {
            BundleRequirement.CreatePercentage("Crab Pot", Theme.Fishing,
                new[] { "crab-spring", "crab-summer", "crab-yearround" },
                numberOfSlots: 2,
                // Spring quota set to 1 so the bundle is in-play for Spring; the obtainability
                // predicate is what we're testing here.
                cumulativeRequiredBySeason: new[] { 1, 1, 2, 2 })
        };
        // Only crab-spring and crab-yearround are obtainable in Spring (per the predicate).
        var sample = BonusItemSampler.SampleForTheme(
            42, 0, Theme.Fishing, Season.Spring, bundles,
            isObtainableInSeason: id => id != "crab-summer",
            rarityOf: AllCommon,
            maxCount: 10);

        Assert.Equal(2, sample.Count);
        Assert.Contains("crab-spring", sample);
        Assert.Contains("crab-yearround", sample);
    }

    [Fact]
    public void Wrong_season_collapses_seasonal_to_empty()
    {
        // Spring Seasonal bundle queried in Summer -> no items.
        var bundles = new[]
        {
            BundleRequirement.CreateSeasonal("Spring", Theme.Foraging,
                new[] { "horseradish" }, Season.Spring)
        };
        var sample = BonusItemSampler.SampleForTheme(
            42, 0, Theme.Foraging, Season.Summer, bundles, _ => true, AllCommon, maxCount: 5);
        Assert.Empty(sample);
    }

    // ----- UX4: rarity weighting + zero-quota Percentage exclusion -----

    [Fact]
    public void EarlyGameAvoid_filters_pool_in_weeks_1_and_2()
    {
        // Spring Mining bundle containing (O)80 Quartz (early-game OK) + (O)768 Solar Essence
        // (deep-mine, in EarlyGameAvoid). In weeks 1-2 the avoid set kicks in and Solar Essence
        // is filtered out; in week 3+ both items are eligible.
        var bundles = new[]
        {
            BundleRequirement.CreatePercentage("Geologist", Theme.Mining,
                new[] { "(O)80", "(O)768" }, // Quartz, Solar Essence
                numberOfSlots: 1,
                cumulativeRequiredBySeason: new[] { 1, 1, 1, 1 })
        };

        // Week 1: Solar Essence filtered out, only Quartz remains.
        var w1 = BonusItemSampler.SampleForTheme(
            42, 1, Theme.Mining, Season.Spring, bundles, _ => true, AllCommon, maxCount: 10);
        Assert.Equal(new[] { "(O)80" }, w1.ToArray());

        // Week 3: full pool, both items present.
        var w3 = BonusItemSampler.SampleForTheme(
            42, 3, Theme.Mining, Season.Spring, bundles, _ => true, AllCommon, maxCount: 10);
        Assert.Equal(2, w3.Count);
        Assert.Contains("(O)80", w3);
        Assert.Contains("(O)768", w3);
    }

    [Fact]
    public void EarlyGameAvoid_falls_back_to_full_pool_when_filter_empties_it()
    {
        // Pool consists entirely of EarlyGameAvoid items — would normally produce an empty
        // bonus card in early weeks. Sampler falls back to the unfiltered pool so the player
        // still sees a card (even if it lists hard items).
        var bundles = new[]
        {
            BundleRequirement.CreatePercentage("All Hard", Theme.Mining,
                new[] { "(O)768", "(O)769" }, // Solar + Void Essence both in EarlyGameAvoid
                numberOfSlots: 1,
                cumulativeRequiredBySeason: new[] { 1, 1, 1, 1 })
        };
        var sample = BonusItemSampler.SampleForTheme(
            42, 1, Theme.Mining, Season.Spring, bundles, _ => true, AllCommon, maxCount: 10);
        Assert.Equal(2, sample.Count);
    }

    [Fact]
    public void Zero_quota_Percentage_bundle_is_excluded_from_the_pool()
    {
        // 2026-05-28 reversal: zero-quota Percentage bundles return EMPTY for the bonus pool.
        // Adventurer's Spring quota = 0, so Solar/Void Essence (priced as Common/Uncommon)
        // should not appear in Spring Mining bonus picks. Rarity-only weighting can't keep
        // them out — both essences price as Common-tier, matching Quartz's weight 1:1.
        var bundles = new[]
        {
            BundleRequirement.CreatePercentage("Adventurer", Theme.Mining,
                new[] { "solar-essence", "void-essence", "bat-wing" },
                numberOfSlots: 2,
                cumulativeRequiredBySeason: new[] { 0, 1, 2, 2 })
        };
        // Sampled in Spring (quota = 0): empty.
        var springSample = BonusItemSampler.SampleForTheme(
            42, 0, Theme.Mining, Season.Spring, bundles, _ => true, AllCommon, maxCount: 10);
        Assert.Empty(springSample);

        // Sampled in Summer (quota = 1): full pool.
        var summerSample = BonusItemSampler.SampleForTheme(
            42, 0, Theme.Mining, Season.Summer, bundles, _ => true, AllCommon, maxCount: 10);
        Assert.Equal(3, summerSample.Count);
    }

    [Fact]
    public void Sampling_favors_lower_rarity_items_over_many_draws()
    {
        // 100 common items + 1 very-rare item. Over many seeds, the rare item should be drawn
        // far less often than its 1/101 uniform share — the inverse-rarity weighting puts it at
        // 1/(100*8 + 1) ≈ 0.12% per draw vs 0.99% under uniform.
        var ingredients = Enumerable.Range(0, 100).Select(i => $"common-{i:D3}").Append("rare-1").ToList();
        var bundles = new[]
        {
            BundleRequirement.CreatePercentage("Mixed", Theme.Mining, ingredients,
                numberOfSlots: 50,
                cumulativeRequiredBySeason: new[] { 50, 50, 50, 50 })
        };
        Func<string, Rarity> rarityOf = id => id == "rare-1" ? Rarity.VeryRare : Rarity.Common;

        int rareHits = 0;
        const int Trials = 200;
        for (int seed = 0; seed < Trials; seed++)
        {
            var sample = BonusItemSampler.SampleForTheme(
                seed, 0, Theme.Mining, Season.Spring, bundles, _ => true, rarityOf, maxCount: 1);
            if (sample.Contains("rare-1")) rareHits++;
        }
        // Expected ~0.25 hits under weighted; uniform would yield ~2.0. 5 is a generous ceiling
        // that catches the regression (uniform sampling) without flaking on rng variance.
        Assert.True(rareHits <= 5,
            $"Expected very-rare item to be picked sparingly under inverse-rarity weighting; saw {rareHits}/{Trials}.");
    }

    [Fact]
    public void WeightFor_inverse_rarity_table()
    {
        Assert.Equal(8, BonusItemSampler.WeightFor(Rarity.Common));
        Assert.Equal(4, BonusItemSampler.WeightFor(Rarity.Uncommon));
        Assert.Equal(2, BonusItemSampler.WeightFor(Rarity.Rare));
        Assert.Equal(1, BonusItemSampler.WeightFor(Rarity.VeryRare));
    }
}
