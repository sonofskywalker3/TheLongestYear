using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class BonusDropResolverTests
{
    // Use a seeded random so results are deterministic.
    // With seed 0, the first NextDouble() < threshold tests pass/fail predictably.

    [Fact]
    public void NoBonus_always_returns_false()
    {
        var rng = new System.Random(0);
        Assert.False(BonusDropResolver.ShouldGrantExtraDrop(null!, "(O)281", rng));
    }

    [Fact]
    public void ForageYieldUp_excludes_stone_and_wood()
    {
        // stone = (O)390, wood = (O)388 — must always return false regardless of rng
        for (int seed = 0; seed < 100; seed++)
        {
            var rng = new System.Random(seed);
            Assert.False(BonusDropResolver.ShouldGrantExtraDrop("forage_yield_up", "(O)390", rng));
            rng = new System.Random(seed);
            Assert.False(BonusDropResolver.ShouldGrantExtraDrop("forage_yield_up", "(O)388", rng));
        }
    }

    [Fact]
    public void ForageYieldUp_fires_at_20pct_for_forage_item()
    {
        // Over 10000 samples the hit rate should be 0.20 ± 0.02 (rebalanced 25% → 20%).
        int hits = 0;
        var rng = new System.Random(42);
        for (int i = 0; i < 10000; i++)
            if (BonusDropResolver.ShouldGrantExtraDrop("forage_yield_up", "(O)281", rng))
                hits++;
        double rate = hits / 10000.0;
        Assert.InRange(rate, 0.18, 0.22);
    }

    [Fact]
    public void MineDropsUp_fires_at_20pct_excludes_stone()
    {
        // Rebalanced 30% → 20% per user spec.
        for (int seed = 0; seed < 100; seed++)
        {
            var rng = new System.Random(seed);
            Assert.False(BonusDropResolver.ShouldGrantExtraDrop("mine_drops_up", "(O)390", rng));
        }
        int hits = 0;
        var rng2 = new System.Random(42);
        for (int i = 0; i < 10000; i++)
            if (BonusDropResolver.ShouldGrantExtraDrop("mine_drops_up", "(O)378", rng2))
                hits++;
        double rate = hits / 10000.0;
        Assert.InRange(rate, 0.18, 0.22);
    }

    [Fact]
    public void AllDropsUp_fires_at_10pct_including_stone_and_wood()
    {
        // Mixed all_drops_up = BonusDropResolver.MixedAllDropsChance (10% baseline).
        // Stone/wood are NOT excluded for Mixed.
        int stoneHits = 0, woodHits = 0;
        var rng = new System.Random(42);
        for (int i = 0; i < 10000; i++)
            if (BonusDropResolver.ShouldGrantExtraDrop("all_drops_up", "(O)390", rng))
                stoneHits++;
        rng = new System.Random(42);
        for (int i = 0; i < 10000; i++)
            if (BonusDropResolver.ShouldGrantExtraDrop("all_drops_up", "(O)388", rng))
                woodHits++;
        Assert.InRange(stoneHits / 10000.0, 0.08, 0.12);
        Assert.InRange(woodHits / 10000.0, 0.08, 0.12);
    }

    [Fact]
    public void Unknown_bonus_id_returns_false()
    {
        var rng = new System.Random(0);
        Assert.False(BonusDropResolver.ShouldGrantExtraDrop("not_a_real_bonus", "(O)281", rng));
    }
}
