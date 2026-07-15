using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class RemixSelectorTests
{
    private static BundleSpec Named(string name) => new(
        "Pantry", 0, name, name, "O 495 30", 0, 1,
        new[] { new BundleSlotSpec("24", 1, 0) });

    private static IReadOnlyList<IReadOnlyList<BundleSpec>> Pools() => new[]
    {
        (IReadOnlyList<BundleSpec>)new[] { Named("A1"), Named("A2"), Named("A3") },
        new[] { Named("B1"), Named("B2") },
        new[] { Named("C1") },
    };

    [Fact]
    public void SameSeed_SamePicks()
    {
        var a = RemixSelector.PickForRoom(Pools(), seed: 12345, room: "Pantry");
        var b = RemixSelector.PickForRoom(Pools(), seed: 12345, room: "Pantry");
        Assert.Equal(a.Select(x => x.Name), b.Select(x => x.Name));
    }

    [Fact]
    public void DifferentSeeds_CanDiffer()
    {
        // Across 20 seeds at least two distinct selections must appear (3*2*1=6 combos).
        var seen = new HashSet<string>();
        for (int s = 0; s < 20; s++)
            seen.Add(string.Join(",", RemixSelector.PickForRoom(Pools(), s, "Pantry").Select(x => x.Name)));
        Assert.True(seen.Count >= 2, $"only saw: {string.Join(" | ", seen)}");
    }

    [Fact]
    public void OnePickPerPosition_ReindexedSequentially()
    {
        var picks = RemixSelector.PickForRoom(Pools(), 7, "Pantry");
        Assert.Equal(3, picks.Count);
        Assert.Equal(new[] { 0, 1, 2 }, picks.Select(p => p.Index));
        Assert.StartsWith("A", picks[0].Name);
        Assert.StartsWith("B", picks[1].Name);
        Assert.Equal("C1", picks[2].Name);
    }

    [Fact]
    public void DifferentRooms_SameSeed_IndependentStreams()
    {
        var pantry = RemixSelector.PickForRoom(Pools(), 99, "Pantry");
        var crafts = RemixSelector.PickForRoom(Pools(), 99, "CraftsRoom");
        // Not required to differ every time, but the streams must be independently seeded:
        // assert the salt actually feeds the RNG by checking 20 seeds produce a difference somewhere.
        bool anyDifference = false;
        for (int s = 0; s < 20 && !anyDifference; s++)
            anyDifference = !RemixSelector.PickForRoom(Pools(), s, "Pantry").Select(x => x.Name)
                .SequenceEqual(RemixSelector.PickForRoom(Pools(), s, "CraftsRoom").Select(x => x.Name));
        Assert.True(anyDifference);
    }

    [Fact]
    public void EngineSeed_StablePerLoop_ChangesAcrossLoops()
    {
        Assert.Equal(BundleEngineSeed.For(123456789UL, 3), BundleEngineSeed.For(123456789UL, 3));
        Assert.NotEqual(BundleEngineSeed.For(123456789UL, 3), BundleEngineSeed.For(123456789UL, 4));
    }

    // Reproduces the live tly_genbundles finding: one room fielded "Home Cook's" at three
    // positions (name-uniquified downstream to II/III but content-identical) because each
    // position's candidate pool independently offered the same-named bundle. Every position
    // below offers the SAME three names ("A","B","C"), so an alternative unpicked-name
    // candidate is always available at every position (worst case: 2 of 3 already picked,
    // 1 still unpicked) -- this must never produce a duplicate name for ANY seed.
    private static IReadOnlyList<IReadOnlyList<BundleSpec>> OverlappingPools() => new[]
    {
        (IReadOnlyList<BundleSpec>)new[] { Named("A"), Named("B"), Named("C") },
        new[] { Named("A"), Named("B"), Named("C") },
        new[] { Named("A"), Named("B"), Named("C") },
    };

    [Fact]
    public void NoDuplicateNamesWithinRoom_WhenAlternativesExist()
    {
        for (int seed = 0; seed < 200; seed++)
        {
            var picks = RemixSelector.PickForRoom(OverlappingPools(), seed, "Pantry");
            var names = picks.Select(p => p.Name).ToList();
            Assert.True(
                names.Distinct(System.StringComparer.Ordinal).Count() == names.Count,
                $"seed {seed} produced duplicate names: {string.Join(",", names)}");
        }
    }

    [Fact]
    public void DuplicateNamesStillAllowed_WhenNoAlternativeRemains()
    {
        // Every position offers ONLY "Only" -- no unpicked-name alternative can ever exist, so
        // the dedup guard must fall back to the full pool rather than throw or drop a position
        // (the global name-uniquifier in BundleEngine is the last-resort backstop for this case).
        var pools = new[]
        {
            (IReadOnlyList<BundleSpec>)new[] { Named("Only") },
            new[] { Named("Only") },
        };
        var picks = RemixSelector.PickForRoom(pools, seed: 42, room: "Pantry");
        Assert.Equal(2, picks.Count);
        Assert.All(picks, p => Assert.Equal("Only", p.Name));
        Assert.Equal(new[] { 0, 1 }, picks.Select(p => p.Index));
    }

    [Fact]
    public void Dedup_SameSeed_StillDeterministic()
    {
        var a = RemixSelector.PickForRoom(OverlappingPools(), seed: 555, room: "Pantry");
        var b = RemixSelector.PickForRoom(OverlappingPools(), seed: 555, room: "Pantry");
        Assert.Equal(a.Select(x => x.Name), b.Select(x => x.Name));
    }
}
