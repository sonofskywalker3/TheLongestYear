using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>Models the reset-then-reload sequence: the seed PerformReset generates with must equal
/// the seed ResolveRequirements re-derives with on the next load, whether or not the board was held.</summary>
public class BundleHoldSeedTests
{
    private const ulong Basis = 0x1234_5678_9ABC_DEF0UL;
    private static readonly long[] Curve = { 0, 50 };

    private static int SeedAtReset(MetaState s)
    {
        s.CompletedResets += 1;                                  // PerformReset step 11
        s.BundlesGeneratedForReset = s.CompletedResets;          // step 11a marker
        return BundleEngineSeed.For(Basis, s.EffectiveBundleSeedLoop);
    }

    private static int SeedAtLoad(MetaState s)
    {
        Assert.Equal(RequirementsSource.EngineManifest,
            EngineModeDecider.Decide(s.BundlesGeneratedForReset, s.CompletedResets, ccTouched: false));
        return BundleEngineSeed.For(Basis, s.EffectiveBundleSeedLoop);
    }

    [Fact]
    public void Held_board_regenerates_from_the_same_seed_as_the_previous_loop()
    {
        var s = new MetaState { CompletedResets = 1, BundlesGeneratedForReset = 1, JunimoPoints = 0 };
        int loop1Seed = BundleEngineSeed.For(Basis, s.EffectiveBundleSeedLoop);

        BundleHold.Apply(s, keep: true, Curve);
        int resetSeed = SeedAtReset(s);
        int loadSeed = SeedAtLoad(s);

        Assert.Equal(loop1Seed, resetSeed);
        Assert.Equal(resetSeed, loadSeed);
        Assert.Equal(2, s.CompletedResets);
    }

    [Fact]
    public void Reshuffled_board_uses_the_new_loop_seed_and_reloads_identically()
    {
        var s = new MetaState { CompletedResets = 1, BundlesGeneratedForReset = 1 };
        int loop1Seed = BundleEngineSeed.For(Basis, s.EffectiveBundleSeedLoop);

        BundleHold.Apply(s, keep: false, Curve);
        int resetSeed = SeedAtReset(s);
        int loadSeed = SeedAtLoad(s);

        Assert.NotEqual(loop1Seed, resetSeed);
        Assert.Equal(BundleEngineSeed.For(Basis, 2), resetSeed);
        Assert.Equal(resetSeed, loadSeed);
    }

    [Fact]
    public void Legacy_save_without_hold_fields_behaves_exactly_as_before()
    {
        var s = new MetaState { CompletedResets = 4, BundlesGeneratedForReset = 4 };
        Assert.Equal(BundleEngineSeed.For(Basis, 4), SeedAtLoad(s));
    }
}
