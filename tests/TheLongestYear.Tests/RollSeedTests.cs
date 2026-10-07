using System;
using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>Final review I1: the two cards of one week must roll independently. The legacy seeded
/// System.Random correlated the related seeds (seed ^ week ^ theme ^ salt), so some theme pairs were
/// never within 0.10x of each other and never shared a drawback.</summary>
public class RollSeedTests
{
    private const int Seeds = 4000;
    private static readonly int[] Weeks = { 3, 11 };

    private static IEnumerable<(Theme A, Theme B)> ThemePairs()
    {
        var all = Enum.GetValues<Theme>();
        for (int i = 0; i < all.Length; i++)
            for (int j = i + 1; j < all.Length; j++)
                yield return (all[i], all[j]);
    }

    [Fact]
    public void Mix_is_deterministic_and_order_sensitive()
    {
        Assert.Equal(RollSeed.Mix(1, 2, 3, 4), RollSeed.Mix(1, 2, 3, 4));
        Assert.NotEqual(RollSeed.Mix(1, 2, 3, 4), RollSeed.Mix(2, 1, 3, 4));
        Assert.NotEqual(RollSeed.Mix(1, 2, 3), RollSeed.Mix(1, 2, 3, 0));
    }

    [Fact]
    public void Mix_never_returns_a_negative_seed()
        => Assert.All(Enumerable.Range(-500, 1000), s => Assert.True(RollSeed.Mix(s, 7, 99) >= 0));

    [Fact]
    public void Two_cards_multipliers_are_within_a_tenth_at_the_independent_rate_for_every_theme_pair()
    {
        // 21 steps of 0.05: |step difference| <= 2 has probability (21 + 2*20 + 2*19) / 441.
        const double expected = 99.0 / 441.0;
        foreach (int week in Weeks)
            foreach (var (a, b) in ThemePairs())
            {
                int close = Enumerable.Range(0, Seeds).Count(s =>
                    Math.Abs(CardMultiplier.For(s, week, a, true) - CardMultiplier.For(s, week, b, true)) <= 0.10 + 1e-9);
                double rate = close / (double)Seeds;
                Assert.True(Math.Abs(rate - expected) < 0.05, $"{a}/{b} week {week}: {rate:0.000} vs {expected:0.000}");
            }
    }

    [Fact]
    public void Two_cards_mystery_values_are_close_at_the_independent_rate_for_every_theme_pair()
    {
        // 11 steps of 0.05: |step difference| <= 2 has probability (11 + 2*10 + 2*9) / 121.
        const double expected = 49.0 / 121.0;
        foreach (var (a, b) in ThemePairs())
        {
            int close = Enumerable.Range(0, Seeds).Count(s =>
                Math.Abs(CardMultiplier.Mystery(s, 5, a) - CardMultiplier.Mystery(s, 5, b)) <= 0.10 + 1e-9);
            double rate = close / (double)Seeds;
            Assert.True(Math.Abs(rate - expected) < 0.06, $"{a}/{b}: {rate:0.000} vs {expected:0.000}");
        }
    }

    [Fact]
    public void Two_cards_share_a_drawback_at_the_independent_rate_for_every_theme_pair()
    {
        foreach (int week in Weeks)
            foreach (var (a, b) in ThemePairs())
            {
                var allowedA = RandomPairing.AllLiabilities.Where(id => !RandomPairing.ExcludedFor(a).Contains(id)).ToList();
                var allowedB = RandomPairing.AllLiabilities.Where(id => !RandomPairing.ExcludedFor(b).Contains(id)).ToList();
                double expected = allowedA.Intersect(allowedB).Count() / (double)(allowedA.Count * allowedB.Count);
                int same = Enumerable.Range(0, Seeds).Count(s =>
                    RandomPairing.LiabilityFor(s, week, a, true) == RandomPairing.LiabilityFor(s, week, b, true));
                Assert.True(same > 0, $"{a}/{b} week {week} never share a drawback");
                double rate = same / (double)Seeds;
                Assert.True(Math.Abs(rate - expected) < 0.04, $"{a}/{b} week {week}: {rate:0.000} vs {expected:0.000}");
            }
    }

    [Fact]
    public void A_cards_multiplier_and_drawback_do_not_move_together()
    {
        // Same card, different salts: the drawback index should not track the multiplier step.
        var xs = new List<double>(); var ys = new List<double>();
        for (int s = 0; s < Seeds; s++)
        {
            xs.Add(CardMultiplier.For(s, 6, Theme.Mixed, true));
            ys.Add(RandomPairing.AllLiabilities.ToList().IndexOf(RandomPairing.LiabilityFor(s, 6, Theme.Mixed, true)));
        }
        Assert.InRange(Correlation(xs, ys), -0.06, 0.06);
    }

    private static double Correlation(IReadOnlyList<double> x, IReadOnlyList<double> y)
    {
        double mx = x.Average(), my = y.Average(), sxy = 0, sxx = 0, syy = 0;
        for (int i = 0; i < x.Count; i++)
        {
            sxy += (x[i] - mx) * (y[i] - my);
            sxx += (x[i] - mx) * (x[i] - mx);
            syy += (y[i] - my) * (y[i] - my);
        }
        return sxy / Math.Sqrt(sxx * syy);
    }
}
