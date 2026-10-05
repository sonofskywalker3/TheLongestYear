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
    private static readonly PoolItem SuperCucumber = Fish("(O)155", "Beach", "Submarine");
    private static readonly PoolItem Squid = Fish("(O)151", "Beach");
    private static readonly PoolItem Lingcod = Fish("(O)707", "Backwoods", "Forest", "Mountain", "Town");

    private static readonly IReadOnlyList<PoolItem> Pool = new[]
    {
        LargemouthBass, Carp, Bullhead, Sturgeon, Chub, Catfish, Woodskip, MutantCarp, Sunfish, Shad,
        TigerTrout, Bream, Sardine, Tuna, RedSnapper, Tilapia, Herring, Octopus, Pufferfish, Ghostfish,
        Stonefish, Sandfish, ScorpionCarp, LavaEel, SpookFish, Angler, Walleye, SuperCucumber, Squid, Lingcod,
    };

    private static string Row(string name, int difficulty, string spans)
        => $"{name}/{difficulty}/mixed/12/30/{spans}/spring summer fall winter/both/690 .4 1 .1/5/1/1/0";

    /// <summary>Live Data/Fish difficulty and biting windows for the open-water fish the
    /// hard short-window rule has to judge.</summary>
    private static readonly IReadOnlyDictionary<string, RawFishEntry> FishRows = new Dictionary<string, RawFishEntry>
    {
        ["128"] = RawFishEntry.Parse("128", Row("Pufferfish", 80, "1200 1600")),
        ["149"] = RawFishEntry.Parse("149", Row("Octopus", 95, "600 1300")),
        ["155"] = RawFishEntry.Parse("155", Row("Super Cucumber", 80, "1800 2600")),
        ["151"] = RawFishEntry.Parse("151", Row("Squid", 75, "1800 2600")),
        ["143"] = RawFishEntry.Parse("143", Row("Catfish", 75, "600 2400")),
        ["147"] = RawFishEntry.Parse("147", Row("Herring", 25, "600 2600")),
        ["707"] = RawFishEntry.Parse("707", Row("Lingcod", 85, "600 2600")),
        ["130"] = RawFishEntry.Parse("130", Row("Tuna", 70, "600 1900")),
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
        var ids = Ids(FishBundleCandidates.ForSpecialty(Pool, FishRows));

        Assert.Equal(new[]
            {
                Woodskip, MutantCarp, Ghostfish, Stonefish, Sandfish, ScorpionCarp, LavaEel, SpookFish, Angler,
                Pufferfish, Octopus, SuperCucumber,
            }.Select(f => f.ItemId).ToHashSet(),
            ids);
    }

    /// <summary>Jeff's ruling, 2026-10-05: an open-water fish is still a specialty when it is both
    /// hard (difficulty 80 or more) and bites for 8 hours a day or less in total. Pufferfish (80,
    /// noon to 4pm), Octopus (95, 6am to 1pm) and Super Cucumber (80, 6pm to 2am) qualify; Squid
    /// (75) and Lingcod (85, all day) do not.</summary>
    [Theory]
    [InlineData("128", true)]
    [InlineData("149", true)]
    [InlineData("155", true)]
    [InlineData("151", false)]
    [InlineData("707", false)]
    [InlineData("143", false)]
    [InlineData("147", false)]
    public void HardShortWindowFish_CountAsSpecialty(string id, bool expected)
        => Assert.Equal(expected, FishBundleCandidates.IsHardShortWindowFish(FishRows[id]));

    [Theory]
    [InlineData("1200 1600", 4.0)]
    [InlineData("600 1100 1800 2600", 13.0)]
    [InlineData("1830 2000", 1.5)]
    public void RawFishEntry_DailyWindowHours_SumsEveryWindow(string spans, double hours)
        => Assert.Equal(hours, RawFishEntry.Parse("1", Row("X", 50, spans)).DailyWindowHours(), 3);

    [Fact]
    public void SpecialtyFish_FillsThroughTheFiller_WithoutBeachFish()
    {
        var pools = new ItemPools { Fish = Pool, FishRows = FishRows };
        var spec = Bundle("Specialty Fish", Pufferfish, Ghostfish, Sandfish, Woodskip);
        Assert.True(FishBundleCandidates.IsSpecialtyFishBundle(spec));
        for (int seed = 0; seed < 40; seed++)
        {
            var filled = BundleSlotFiller.Fill(spec,
                new DomainMatch(PoolDomain.Fish, null), pools, new BundleGenerationTuning(), new Random(seed));
            Assert.NotSame(spec, filled);
            Assert.DoesNotContain(filled.Slots, s => s.ItemId is "(O)147" or "(O)151" or "(O)130" or "(O)143");
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
