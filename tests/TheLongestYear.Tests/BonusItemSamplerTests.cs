using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class BonusItemSamplerTests
{
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
            42, 3, Theme.Fishing, Season.Spring, Bundles(), _ => true, maxCount: 3);
        var second = BonusItemSampler.SampleForTheme(
            42, 3, Theme.Fishing, Season.Spring, Bundles(), _ => true, maxCount: 3);
        Assert.Equal(first, second);
    }

    [Fact]
    public void Sample_changes_with_seed()
    {
        var first  = BonusItemSampler.SampleForTheme(
            42, 3, Theme.Fishing, Season.Spring, Bundles(), _ => true, maxCount: 3);
        var second = BonusItemSampler.SampleForTheme(
            99, 3, Theme.Fishing, Season.Spring, Bundles(), _ => true, maxCount: 3);

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
            42, 0, Theme.Fishing, Season.Spring, Bundles(), _ => true, maxCount: 2);
        Assert.Equal(2, sample.Count);
    }

    [Fact]
    public void Empty_pool_returns_empty()
    {
        // No Mining bundles in our fixture -> nothing to sample.
        var sample = BonusItemSampler.SampleForTheme(
            42, 0, Theme.Mining, Season.Spring, Bundles(), _ => true, maxCount: 5);
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
                cumulativeRequiredBySeason: new[] { 0, 1, 2, 2 })
        };
        // Only crab-spring and crab-yearround are obtainable in Spring.
        var sample = BonusItemSampler.SampleForTheme(
            42, 0, Theme.Fishing, Season.Spring, bundles,
            isObtainableInSeason: id => id != "crab-summer",
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
            42, 0, Theme.Foraging, Season.Summer, bundles, _ => true, maxCount: 5);
        Assert.Empty(sample);
    }
}
