using System;
using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class BundleSlotFillerTests
{
    private static PoolItem Item(string id, int price = 50, int weight = 3,
        Season[]? seasons = null, string[]? locations = null)
        => new(id, price, weight, seasons ?? Array.Empty<Season>(), locations ?? Array.Empty<string>());

    private static BundleSpec Spec(string name, int slotCount, int numberOfSlots = -1,
        params string[] ids)
    {
        var slots = (ids.Length > 0 ? ids : Enumerable.Range(0, slotCount).Select(i => (900 + i).ToString()))
            .Select(id => new BundleSlotSpec(id, 1, 0)).ToList();
        return new BundleSpec("Pantry", 0, name, name, "O 495 30", 0,
            numberOfSlots > 0 ? numberOfSlots : slots.Count, slots);
    }

    private static readonly BundleGenerationTuning Tuning = new();

    [Fact]
    public void DomainNone_ReturnsSameInstance()
    {
        var spec = Spec("X", 3);
        Assert.Same(spec, BundleSlotFiller.Fill(
            spec, new DomainMatch(PoolDomain.None, null), new ItemPools(), Tuning, new Random(1)));
    }

    [Fact]
    public void InsufficientPool_ReturnsSameInstance()
    {
        var pools = new ItemPools { Crops = new[] { Item("(O)24"), Item("(O)25") } };
        var spec = Spec("Spring Crops", 4);
        Assert.Same(spec, BundleSlotFiller.Fill(
            spec, new DomainMatch(PoolDomain.SeasonalCrops, Season.Spring), pools, Tuning, new Random(1)));
    }

    [Fact]
    public void Fill_NoDuplicates_TargetCount_MetadataPreserved_Deterministic()
    {
        var pools = new ItemPools
        {
            Crops = Enumerable.Range(0, 12).Select(i => Item($"(O){100 + i}",
                seasons: new[] { Season.Spring })).ToList(),
        };
        var spec = Spec("Spring Crops", 4, numberOfSlots: 4);
        var match = new DomainMatch(PoolDomain.SeasonalCrops, Season.Spring);

        var a = BundleSlotFiller.Fill(spec, match, pools, Tuning, new Random(5));
        var b = BundleSlotFiller.Fill(spec, match, pools, Tuning, new Random(5));

        Assert.Equal(4, a.Slots.Count);
        Assert.Equal(4, a.Slots.Select(s => s.ItemId).Distinct().Count());
        Assert.Equal(a.Slots, b.Slots); // deterministic
        Assert.Equal(spec.Name, a.Name);
        Assert.Equal(spec.NumberOfSlots, a.NumberOfSlots);
        Assert.Equal(spec.Index, a.Index);
    }

    [Fact]
    public void SeasonFilter_ExcludesOutOfSeasonItems()
    {
        var pools = new ItemPools
        {
            Crops = new[]
            {
                Item("(O)1", seasons: new[] { Season.Spring }),
                Item("(O)2", seasons: new[] { Season.Spring }),
                Item("(O)3", seasons: Array.Empty<Season>()),      // any season — eligible
                Item("(O)4", seasons: new[] { Season.Winter }),    // out of season
            },
        };
        var filled = BundleSlotFiller.Fill(Spec("Spring Crops", 3),
            new DomainMatch(PoolDomain.SeasonalCrops, Season.Spring), pools, Tuning, new Random(3));
        Assert.DoesNotContain("(O)4", filled.Slots.Select(s => s.ItemId));
        Assert.Equal(3, filled.Slots.Count);
    }

    [Fact]
    public void QualityCrops_AllGold_AtTunedStack()
    {
        var pools = new ItemPools
        {
            Crops = Enumerable.Range(0, 8).Select(i => Item($"(O){200 + i}")).ToList(),
        };
        var filled = BundleSlotFiller.Fill(Spec("Quality Crops", 4, numberOfSlots: 3),
            new DomainMatch(PoolDomain.QualityCrops, null), pools, Tuning, new Random(11));
        Assert.All(filled.Slots, s =>
        {
            Assert.Equal(2, s.Quality);
            Assert.Equal(Tuning.QualityCropStack, s.Stack);
        });
        Assert.Equal(3, filled.NumberOfSlots);
    }

    [Fact]
    public void MonsterDrops_PriceBandedStacks()
    {
        var pools = new ItemPools
        {
            MonsterDrops = new[]
            {
                Item("(O)766", price: 5),   // cheap
                Item("(O)768", price: 40),  // mid
                Item("(O)769", price: 100), // dear
            },
        };
        var filled = BundleSlotFiller.Fill(Spec("Slime Hunter", 3),
            new DomainMatch(PoolDomain.MonsterDrops, null), pools, Tuning, new Random(2));
        foreach (BundleSlotSpec slot in filled.Slots)
        {
            int price = pools.MonsterDrops.First(p => p.ItemId == slot.ItemId).Price;
            if (price < Tuning.CheapPriceCeiling)
                Assert.InRange(slot.Stack, Tuning.CheapMinStack, Tuning.CheapMaxStack);
            else if (price < Tuning.MidPriceCeiling)
                Assert.InRange(slot.Stack, Tuning.MidMinStack, Tuning.MidMaxStack);
            else
                Assert.InRange(slot.Stack, Tuning.DearMinStack, Tuning.DearMaxStack);
        }
    }

    [Fact]
    public void LargeQuantityForage_ChanceOne_ExactlyOneBigStackSlot()
    {
        var tuning = new BundleGenerationTuning { LargeQuantityForageChance = 1.0 };
        var pools = new ItemPools
        {
            Forage = Enumerable.Range(0, 10).Select(i => Item($"(O){300 + i}",
                seasons: new[] { Season.Spring })).ToList(),
        };
        var filled = BundleSlotFiller.Fill(Spec("Spring Foraging", 4),
            new DomainMatch(PoolDomain.SeasonalForage, Season.Spring), pools, tuning, new Random(9));
        var big = filled.Slots.Where(s => s.Stack >= tuning.LargeQuantityMinStack).ToList();
        Assert.Single(big);
        Assert.InRange(big[0].Stack, tuning.LargeQuantityMinStack, tuning.LargeQuantityMaxStack);
        Assert.Equal(0, big[0].Quality);
    }

    [Fact]
    public void Fish_LocationOverlap_KeepsHabitatIdentity()
    {
        var pools = new ItemPools
        {
            Fish = new[]
            {
                Item("(O)128", locations: new[] { "Beach" }),
                Item("(O)129", locations: new[] { "Beach" }),
                Item("(O)130", locations: new[] { "Beach" }),
                Item("(O)136", locations: new[] { "Forest" }), // river-only — must not appear
            },
        };
        // Original slot 128 spawns at the Beach -> pool restricted to Beach fish.
        var spec = Spec("Ocean Fish", 3, numberOfSlots: 3, "128", "129", "130");
        var filled = BundleSlotFiller.Fill(spec,
            new DomainMatch(PoolDomain.Fish, null), pools, Tuning, new Random(4));
        Assert.DoesNotContain("(O)136", filled.Slots.Select(s => s.ItemId));
        Assert.Equal(3, filled.Slots.Count);
    }

    [Fact]
    public void PickCount_LimitsTargetSlotCount()
    {
        var pools = new ItemPools
        {
            Crops = Enumerable.Range(0, 12).Select(i => Item($"(O){400 + i}")).ToList(),
        };
        var spec = new BundleSpec("Pantry", 0, "Rare Crops", "Rare Crops", "O 495 30", 0, 2,
            Enumerable.Range(0, 8).Select(i => new BundleSlotSpec((500 + i).ToString(), 1, 0)).ToList(),
            PickCount: 4);
        var filled = BundleSlotFiller.Fill(spec,
            new DomainMatch(PoolDomain.SeasonalCrops, null), pools, Tuning, new Random(6));
        Assert.Equal(4, filled.Slots.Count);
        Assert.Equal(2, filled.NumberOfSlots);
    }
}
