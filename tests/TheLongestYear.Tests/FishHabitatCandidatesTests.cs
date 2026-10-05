using System;
using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>Nexus posts 2026-10-03 (Nerlana): "my lake fish bundle asks for catfish and woodskip
/// which are not actually lake fish and specialty fish asked for herring." The habitat rule took
/// every location ANY original fish spawned in: Carp also bites in the Secret Woods pond and the
/// Sewer, so the Woods joined Lake Fish's water and let Woodskip and Catfish in. Specialty Fish's
/// originals sit at the Beach, the mines, the desert and the Woods, so any Beach fish counted.
/// Locations below are the live Data/Locations spawn keys for each fish.</summary>
public class FishHabitatCandidatesTests
{
    private static PoolItem Fish(string id, params string[] locations)
        => new(id, 50, 3, Array.Empty<Season>(), locations, -4);

    private static readonly PoolItem LargemouthBass = Fish("(O)136", "Backwoods", "Mountain");
    private static readonly PoolItem Carp = Fish("(O)142", "Backwoods", "Mountain", "Sewer", "Woods");
    private static readonly PoolItem Bullhead = Fish("(O)700", "Backwoods", "Mountain");
    private static readonly PoolItem Sturgeon = Fish("(O)698", "Backwoods", "Mountain");
    private static readonly PoolItem Chub = Fish("(O)702", "Backwoods", "Forest", "Mountain");
    private static readonly PoolItem Catfish = Fish("(O)143", "Forest", "Town", "Woods");
    private static readonly PoolItem Woodskip = Fish("(O)734", "Farm_Forest", "Woods");
    private static readonly PoolItem MutantCarp = Fish("(O)682", "Sewer");
    private static readonly PoolItem Sunfish = Fish("(O)145", "Forest", "Town");
    private static readonly PoolItem Shad = Fish("(O)706", "Forest", "Town");
    private static readonly PoolItem TigerTrout = Fish("(O)699", "Forest", "Town");
    private static readonly PoolItem Bream = Fish("(O)132", "Forest", "Town");
    private static readonly PoolItem Sardine = Fish("(O)131", "Beach");
    private static readonly PoolItem Tuna = Fish("(O)130", "Beach");
    private static readonly PoolItem RedSnapper = Fish("(O)150", "Beach");
    private static readonly PoolItem Tilapia = Fish("(O)701", "Beach");
    private static readonly PoolItem Herring = Fish("(O)147", "Beach");
    private static readonly PoolItem Octopus = Fish("(O)149", "Beach", "Submarine");
    private static readonly PoolItem Pufferfish = Fish("(O)128", "Beach");
    private static readonly PoolItem Ghostfish = Fish("(O)156", "UndergroundMine");
    private static readonly PoolItem Stonefish = Fish("(O)158", "UndergroundMine");
    private static readonly PoolItem Sandfish = Fish("(O)164", "Desert");
    private static readonly PoolItem ScorpionCarp = Fish("(O)165", "Desert");
    private static readonly PoolItem LavaEel = Fish("(O)162", "Caldera");
    private static readonly PoolItem SpookFish = Fish("(O)799", "Submarine");
    private static readonly PoolItem Angler = Fish("(O)160", "Town");
    private static readonly PoolItem Walleye = Fish("(O)140", "Backwoods", "Forest", "Mountain", "Town");

    private static readonly IReadOnlyList<PoolItem> Pool = new[]
    {
        LargemouthBass, Carp, Bullhead, Sturgeon, Chub, Catfish, Woodskip, MutantCarp, Sunfish, Shad,
        TigerTrout, Bream, Sardine, Tuna, RedSnapper, Tilapia, Herring, Octopus, Pufferfish, Ghostfish,
        Stonefish, Sandfish, ScorpionCarp, LavaEel, SpookFish, Angler, Walleye,
    };

    private static BundleSpec Bundle(string name, params PoolItem[] originals)
        => new("Fish Tank", 7, name, name, "O 1 1", 0, originals.Length,
            originals.Select(o => new BundleSlotSpec(o.ItemId.Substring(3), 1, 0)).ToList());

    private static HashSet<string> Ids(IEnumerable<PoolItem> items)
        => items.Select(i => i.ItemId).ToHashSet();

    [Fact]
    public void LakeFish_IsTheMountainLake_NotEveryPondCarpSwimsIn()
    {
        var ids = Ids(FishBundleCandidates.ByHabitat(
            Bundle("Lake Fish", LargemouthBass, Carp, Bullhead, Sturgeon), Pool));

        Assert.Contains(Chub.ItemId, ids);
        Assert.Contains(Walleye.ItemId, ids);
        Assert.DoesNotContain(Woodskip.ItemId, ids);
        Assert.DoesNotContain(Catfish.ItemId, ids);
        Assert.DoesNotContain(MutantCarp.ItemId, ids);
        Assert.DoesNotContain(Bream.ItemId, ids);
    }

    [Fact]
    public void RiverFish_IsTownAndForest_WithoutTheWoodsPond()
    {
        var ids = Ids(FishBundleCandidates.ByHabitat(
            Bundle("River Fish", Sunfish, Catfish, Shad, TigerTrout), Pool));

        Assert.Contains(Bream.ItemId, ids);
        Assert.Contains(Chub.ItemId, ids);
        Assert.DoesNotContain(Woodskip.ItemId, ids);
        Assert.DoesNotContain(Carp.ItemId, ids);
        Assert.DoesNotContain(Sardine.ItemId, ids);
    }

    [Fact]
    public void OceanFish_IsTheBeach()
    {
        var ids = Ids(FishBundleCandidates.ByHabitat(
            Bundle("Ocean Fish", Sardine, Tuna, RedSnapper, Tilapia), Pool));

        Assert.Contains(Herring.ItemId, ids);
        Assert.Contains(Octopus.ItemId, ids);
        Assert.DoesNotContain(SpookFish.ItemId, ids);
        Assert.DoesNotContain(Sunfish.ItemId, ids);
    }

    /// <summary>A bundle whose originals share no majority water (Quality Fish: a lake, a river and
    /// an ocean fish) keeps the old any-shared-location pool rather than shrinking to nothing.</summary>
    [Fact]
    public void A_mixed_water_bundle_keeps_every_water_its_originals_touch()
    {
        var ids = Ids(FishBundleCandidates.ByHabitat(
            Bundle("Quality Fish", LargemouthBass, Shad, Tuna, Walleye), Pool));

        Assert.Contains(Herring.ItemId, ids);
        Assert.Contains(Bream.ItemId, ids);
        Assert.Contains(Sturgeon.ItemId, ids);
        Assert.DoesNotContain(Ghostfish.ItemId, ids);
    }

    [Fact]
    public void SpecialtyFish_AsksOnlyForFishOutsideOrdinaryWater()
    {
        var ids = Ids(FishBundleCandidates.ForSpecialty(Pool));

        Assert.Equal(new[]
            {
                Woodskip, MutantCarp, Ghostfish, Stonefish, Sandfish, ScorpionCarp, LavaEel, SpookFish, Angler,
            }.Select(f => f.ItemId).ToHashSet(),
            ids);
    }

    [Fact]
    public void SpecialtyFish_FillsThroughTheFiller_WithoutBeachFish()
    {
        var pools = new ItemPools { Fish = Pool };
        var spec = Bundle("Specialty Fish", Pufferfish, Ghostfish, Sandfish, Woodskip);
        Assert.True(FishBundleCandidates.IsSpecialtyFishBundle(spec));
        for (int seed = 0; seed < 40; seed++)
        {
            var filled = BundleSlotFiller.Fill(spec,
                new DomainMatch(PoolDomain.Fish, null), pools, new BundleGenerationTuning(), new Random(seed));
            Assert.NotSame(spec, filled);
            Assert.DoesNotContain(filled.Slots, s => s.ItemId is "(O)147" or "(O)128" or "(O)149" or "(O)143");
        }
    }

    [Fact]
    public void LakeFish_FillsThroughTheFiller_WithoutWoodskipOrCatfish()
    {
        var pools = new ItemPools { Fish = Pool };
        var spec = Bundle("Lake Fish", LargemouthBass, Carp, Bullhead, Sturgeon);
        for (int seed = 0; seed < 40; seed++)
        {
            var filled = BundleSlotFiller.Fill(spec,
                new DomainMatch(PoolDomain.Fish, null), pools, new BundleGenerationTuning(), new Random(seed));
            Assert.NotSame(spec, filled);
            Assert.DoesNotContain(filled.Slots, s => s.ItemId is "(O)734" or "(O)143" or "(O)682");
        }
    }
}
