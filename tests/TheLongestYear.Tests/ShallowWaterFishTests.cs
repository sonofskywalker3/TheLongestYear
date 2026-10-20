using System;
using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>Player report 2026-10-20: a Quality Fish bundle asked for a gold Goby, and every Goby
/// she caught at the Forest waterfall pool came out base quality. Fish size, and so quality, scales
/// with the clear water between the bobber and the nearest land; a cast from the only ledge above
/// that pool lands on its edge, one tile from land, where a level 10 farmer can never reach silver.
/// A fish whose every reachable catch spot is that shallow must never be asked for with a star.</summary>
public class ShallowWaterFishTests
{
    // ---- the game's size formula (FishingRod.startMinigameEndFunction, BobberBar ctor) ----

    [Fact]
    public void OneTileFromLand_AtLevelTen_TopsOutBelowSilver()
    {
        double best = FishStarDepth.MaxSizeFraction(1, FishStarDepth.YearOneFishingLevel);
        Assert.InRange(best, 0.316, 0.317);
        Assert.True(best < FishStarDepth.SilverSizeFraction);
    }

    [Fact]
    public void TwoTilesFromLand_AtLevelTen_CanRollSilver()
        => Assert.True(FishStarDepth.MaxSizeFraction(2, FishStarDepth.YearOneFishingLevel) >= FishStarDepth.SilverSizeFraction);

    [Fact]
    public void NextToLand_IsAlwaysTheSmallestSize()
        => Assert.Equal(0.0, FishStarDepth.MaxSizeFraction(0, FishStarDepth.YearOneFishingLevel));

    [Fact]
    public void LowLevels_UseTheFiveRollCeiling_AndOpenWaterClamps()
    {
        // Below level 10 the size roll is Next(1 + level/2, 6), so its best is 5.
        Assert.Equal(FishStarDepth.MaxSizeFraction(5, 0), FishStarDepth.MaxSizeFraction(5, 9));
        Assert.Equal(1.0, FishStarDepth.MaxSizeFraction(5, 14));
    }

    [Theory]
    [InlineData(null, true)]   // the map could not be read: keep the old behaviour
    [InlineData(-1, true)]     // no cast the walk can see lands where the fish bites: not proof of shallow water
    [InlineData(0, false)]
    [InlineData(1, false)]
    [InlineData(2, true)]
    [InlineData(5, true)]
    public void CanRollStar_FollowsTheDeepestReachableSpot(int? maxClearWater, bool expected)
        => Assert.Equal(expected, FishStarDepth.CanRollStar(maxClearWater));

    // ---- the pool rule ----

    private static RawObjectEntry Fish() => new("Fish", -4, 100, false, new[] { "fish_river" });

    private static ItemPools Pools(params RawSpawnEntry[] spawns)
    {
        var objects = new Dictionary<string, RawObjectEntry>(StringComparer.Ordinal);
        foreach (RawSpawnEntry s in spawns)
            objects[s.ItemId.Replace("(O)", "")] = Fish();
        return ItemPoolBuilder.Build(
            new List<RawCropEntry>(), objects, new List<RawSpawnEntry>(), spawns.ToList(),
            new HashSet<string>(), new List<RawMonsterDropEntry>(),
            new List<RawFruitTreeEntry>(), new List<RawGeodeDropEntry>(),
            new BundleGenerationTuning());
    }

    [Fact]
    public void AFishOnlyInShallowWater_LosesItsStar_AndIsListedAsShallow()
    {
        ItemPools pools = Pools(
            new RawSpawnEntry("(O)Goby", null, null, "Forest", MaxClearWater: 1),
            new RawSpawnEntry("(O)145", null, null, "Forest", MaxClearWater: 4),
            new RawSpawnEntry("(O)132", null, null, "Forest"));
        Assert.DoesNotContain("(O)Goby", pools.QualityEligibleIds!);
        Assert.Contains("(O)Goby", pools.ShallowWaterFishIds);
        Assert.Contains("(O)145", pools.QualityEligibleIds!);
        Assert.Contains("(O)132", pools.QualityEligibleIds!);
        Assert.DoesNotContain("(O)145", pools.ShallowWaterFishIds);
        Assert.DoesNotContain("(O)132", pools.ShallowWaterFishIds);
    }

    [Fact]
    public void ADeepSpotAnywhere_KeepsTheStar()
    {
        ItemPools pools = Pools(
            new RawSpawnEntry("(O)139", null, null, "Forest", MaxClearWater: 0),
            new RawSpawnEntry("(O)139", null, null, "Town", MaxClearWater: 3));
        Assert.Contains("(O)139", pools.QualityEligibleIds!);
        Assert.Empty(pools.ShallowWaterFishIds);
    }

    [Fact]
    public void AShallowFish_IsNeverRolledWithAStar()
    {
        ItemPools pools = Pools(
            new RawSpawnEntry("(O)Goby", null, null, "Forest", MaxClearWater: 1),
            new RawSpawnEntry("(O)145", null, null, "Forest", MaxClearWater: 4));
        var tuning = new BundleGenerationTuning { GoldQualityChance = 1.0 };
        var spec = new BundleSpec("Pantry", 0, "River Fish", "River Fish", "O 1 1", 4, 2,
            new List<BundleSlotSpec> { new("(O)136", 1, 2), new("(O)137", 1, 2) });
        for (int seed = 0; seed < 50; seed++)
        {
            BundleSpec filled = BundleSlotFiller.Fill(spec, new DomainMatch(PoolDomain.Fish, null), pools, tuning, new Random(seed));
            Assert.Contains(filled.Slots, s => s.ItemId == "(O)Goby");
            Assert.Contains(filled.Slots, s => s.ItemId == "(O)145" && s.Quality == 2);
            foreach (BundleSlotSpec slot in filled.Slots.Where(s => s.ItemId == "(O)Goby"))
                Assert.Equal(0, slot.Quality);
        }
    }

    /// <summary>Boards from before 0.19.121 re-derive from their seed (pre-0.16.158 saves keep no
    /// stored board). A shallow-water fish must take the same two quality rolls it took then, so
    /// every later slot and bundle draws the same numbers; only its own star is dropped.</summary>
    [Fact]
    public void AShallowFish_TakesTheSameRolls_SoTheSeedStillGivesTheSameBoard()
    {
        RawSpawnEntry[] Rows(int? gobyDepth) => new[]
        {
            new RawSpawnEntry("(O)Goby", null, null, "Forest", MaxClearWater: gobyDepth),
            new RawSpawnEntry("(O)145", null, null, "Forest", MaxClearWater: 4),
            new RawSpawnEntry("(O)132", null, null, "Forest", MaxClearWater: 4),
            new RawSpawnEntry("(O)139", null, null, "Forest", MaxClearWater: 4),
            new RawSpawnEntry("(O)140", null, null, "Forest", MaxClearWater: 4),
        };
        ItemPools shallow = Pools(Rows(1));
        ItemPools deep = Pools(Rows(4));
        Assert.Contains("(O)Goby", shallow.ShallowWaterFishIds);
        Assert.Empty(deep.ShallowWaterFishIds);
        var tuning = new BundleGenerationTuning { GoldQualityChance = 0.3, SilverQualityChance = 0.4 };
        var spec = new BundleSpec("Pantry", 0, "River Fish", "River Fish", "O 1 1", 4, 3,
            new List<BundleSlotSpec> { new("(O)136", 1, 0), new("(O)137", 1, 0), new("(O)138", 1, 0) });
        int gobyStarsDropped = 0;
        for (int seed = 0; seed < 200; seed++)
        {
            var rngShallow = new Random(seed);
            var rngDeep = new Random(seed);
            BundleSpec a = BundleSlotFiller.Fill(spec, new DomainMatch(PoolDomain.Fish, null), shallow, tuning, rngShallow);
            BundleSpec b = BundleSlotFiller.Fill(spec, new DomainMatch(PoolDomain.Fish, null), deep, tuning, rngDeep);
            Assert.Equal(b.Slots.Count, a.Slots.Count);
            for (int i = 0; i < a.Slots.Count; i++)
            {
                Assert.Equal(b.Slots[i].ItemId, a.Slots[i].ItemId);
                Assert.Equal(b.Slots[i].Stack, a.Slots[i].Stack);
                if (a.Slots[i].ItemId == "(O)Goby")
                {
                    Assert.Equal(0, a.Slots[i].Quality);
                    if (b.Slots[i].Quality > 0) gobyStarsDropped++;
                }
                else
                    Assert.Equal(b.Slots[i].Quality, a.Slots[i].Quality);
            }
            // The stream after the bundle is the same too, so the next bundle draws the same.
            Assert.Equal(rngDeep.Next(), rngShallow.Next());
        }
        Assert.True(gobyStarsDropped > 0, "the test should cover a Goby that would have rolled a star");
    }

    // ---- the load-time repair ----

    private static readonly IReadOnlySet<string> Shallow = new HashSet<string>(StringComparer.Ordinal) { "(O)Goby" };

    [Fact]
    public void Repair_DropsTheStarOnAShallowFish_Only()
    {
        const string value = "Quality Fish/O 690 1/Goby 1 2 136 1 2 (O)Goby 1 1/4/3//Quality Fish";
        string? repaired = ShallowFishStars.RepairBundleValue(value, Shallow);
        Assert.Equal("Quality Fish/O 690 1/Goby 1 0 136 1 2 (O)Goby 1 0/4/3//Quality Fish", repaired);
    }

    [Fact]
    public void Repair_LeavesACleanBundleAlone()
    {
        Assert.Null(ShallowFishStars.RepairBundleValue("River Fish/O 685 30/Goby 1 0 145 1 2/6/2", Shallow));
        Assert.Null(ShallowFishStars.RepairBundleValue("River Fish/O 685 30/145 1 2/6/1", new HashSet<string>()));
        Assert.Null(ShallowFishStars.RepairBundleValue("broken", Shallow));
    }

    [Fact]
    public void Repair_NamesTheFishItLowered()
    {
        const string value = "Quality Fish/O 690 1/Goby 1 2 136 1 2/4/2";
        Assert.Equal(new[] { "(O)Goby" }, ShallowFishStars.LoweredIds(value, Shallow));
    }
}
