using System.Collections.Generic;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>Curated per-name ramps added 2026-08-21 (user ruling) for the remix-pool + authored
/// pick-X-of-Y bundles whose derived ramp was impossible or plainly harsh/lax.</summary>
public class CuratedQuotaRampTests
{
    // name → (X, expected ramp)
    public static IEnumerable<object[]> Curated => new[]
    {
        new object[] { "Winter Star",          2, new[] { 0, 0, 0, 2 } },
        new object[] { "Forager's",            2, new[] { 0, 0, 2, 2 } },
        new object[] { "Gil's Trophies",       2, new[] { 0, 0, 1, 2 } },
        new object[] { "Brewer's",             4, new[] { 0, 1, 2, 4 } },
        new object[] { "Preserver's",          4, new[] { 0, 1, 2, 4 } },
        new object[] { "Mineral",              4, new[] { 0, 1, 3, 4 } },
        new object[] { "Home Cook's Feast",    4, new[] { 0, 1, 2, 4 } },
        new object[] { "Fish Farmer's",        2, new[] { 0, 0, 1, 2 } },
        new object[] { "Artifact",             4, new[] { 0, 1, 2, 4 } },
        new object[] { "Four Seasons Sampler", 5, new[] { 1, 3, 4, 5 } },
        new object[] { "Rare Crops",           1, new[] { 0, 0, 1, 1 } },
        new object[] { "Garden",               4, new[] { 1, 2, 4, 4 } },
    };

    [Theory]
    [MemberData(nameof(Curated))]
    public void Curated_ramp_is_present_monotone_and_ends_at_X(string name, int x, int[] expected)
    {
        Assert.True(GameplayConfig.DefaultBundleQuotas.TryGetValue(name, out int[] ramp), $"{name} missing");
        Assert.Equal(expected, ramp);
        Assert.Equal(4, ramp.Length);
        for (int i = 1; i < ramp.Length; i++)
            Assert.True(ramp[i] >= ramp[i - 1], $"{name} ramp not monotone");
        Assert.Equal(x, ramp[^1]);
    }

    [Fact]
    public void Winter_Star_no_longer_asks_for_anything_before_winter()
    {
        // 2-of-4 Winter Star: Holly, Plum Pudding, Stuffing, Powdermelon (vanilla remix).
        var parsed = BundleParsing.Parse("Bulletin Board/31",
            "Winter Star/O 74 1/283 5 0 604 1 0 239 1 0 Powdermelon 5 0/2/2/0/Winter Star");
        var req = BundleClassifier.Classify(parsed, Theme.Mixed,
            new Dictionary<string, Season>(), GameplayConfig.DefaultBundleQuotas);

        Assert.NotNull(req);
        Assert.Equal(BundleKind.Percentage, req!.Kind);
        Assert.Equal(new[] { 0, 0, 0, 2 }, req.CumulativeRequiredBySeason);
        Assert.True(req.IsSatisfiedAtSeasonEnd(Season.Summer, new HashSet<string>()));
        Assert.True(req.IsSatisfiedAtSeasonEnd(Season.Fall, new HashSet<string>()));
        Assert.False(req.IsSatisfiedAtSeasonEnd(Season.Winter, new HashSet<string>()));
    }

    [Fact]
    public void Gils_Trophies_curated_ramp_wins_over_the_derived_one()
    {
        var parsed = BundleParsing.Parse("Boiler Room/22",
            "Gil's Trophies/O 879 5/(H)27 1 0 (W)13 1 0 (O)522 1 0 (O)810 1 0/4/2/0/Gil's Trophies");
        var req = BundleClassifier.Classify(parsed, Theme.Mining,
            new Dictionary<string, Season>(), GameplayConfig.DefaultBundleQuotas);

        Assert.Equal(new[] { 0, 0, 1, 2 }, req!.CumulativeRequiredBySeason);
        Assert.NotEqual(BundleClassifier.DerivedDefaultQuota(2), req.CumulativeRequiredBySeason);
    }
}
