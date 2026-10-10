using System;
using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public partial class BundleSlotFillerTests
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

    /// <summary>Builds a synthetic ItemAvailability with only week/hard-week/effort set, for the
    /// stretch and hard-item swap tests below (StretchRule and EffortTiers.IsHard care about
    /// nothing else).</summary>
    private static ItemAvailability Avail(int week, int hardWeek, int effort = 1) =>
        new(AvailabilityWeeks.SeasonOf(week), effort, "test", EarliestWeek: week, HardWeek: hardWeek);

    /// <summary>Builds the model the way ModEntry.BuildAvailabilityModelFor does: the week mode
    /// always comes from the step. A model whose mode contradicts its step is not one the mod can
    /// build, and the stretch rule reads both.</summary>
    private static ItemAvailabilityModel Model(
        Dictionary<string, ItemAvailability> derived, DifficultyStep step = DifficultyStep.Normal)
        => new(derived, mode: WeekModes.For(step), step: step);

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
    public void QualityCrops_AllGold_StackLeftToTheQuantityPass()
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
            Assert.Equal(1, s.Stack);   // basis x band comes later, in QuantityAskPass (2026-09-04)
        });
        Assert.Equal(3, filled.NumberOfSlots);
    }

    [Fact]
    public void MonsterDrops_RollOne_TheQuantityPassBandsThemLater()
    {
        var pools = new ItemPools
        {
            MonsterDrops = new[] { Item("(O)766", price: 5), Item("(O)767", price: 15), Item("(O)768", price: 40), Item("(O)769", price: 40) },
        };
        var filled = BundleSlotFiller.Fill(Spec("Adventurer's", 3),
            new DomainMatch(PoolDomain.MonsterDrops, null), pools, Tuning, new Random(2));
        Assert.All(filled.Slots, s => Assert.Equal(1, s.Stack));
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

    /// <summary>Player report 2026-08-28 ("4 of my foraging bundles need mussels"): beach
    /// shellfish and desert fruit spawn in every season, so they sat in all four seasonal
    /// forage pools with the same weight as a real seasonal plant. A season-named bundle now
    /// asks only for items that are specific to a season, like vanilla; any-season items keep
    /// feeding the season-less bundles (generic crop re-rolls, Four Seasons Sampler).</summary>
    [Fact]
    public void SeasonalDomains_SkipAnySeasonItems_SeasonlessBundleKeepsThem()
    {
        Season[] spring = { Season.Spring };
        var pools = new ItemPools
        {
            Forage = new[]
            {
                Item("(O)16", seasons: spring), Item("(O)18", seasons: spring),
                Item("(O)20", seasons: spring), Item("(O)22", seasons: spring),
                Item("(O)719"), Item("(O)372"), // Mussel, Clam: every season
            },
            Crops = new[]
            {
                Item("(O)24", seasons: spring), Item("(O)188", seasons: spring),
                Item("(O)190", seasons: spring), Item("(O)192", seasons: spring),
                Item("(O)999"), // a modded any-season crop
            },
        };
        bool anySeasonCropSeenInGenericBundle = false;
        for (int seed = 0; seed < 40; seed++)
        {
            var forage = BundleSlotFiller.Fill(Spec("Spring Foraging", 4, 4),
                new DomainMatch(PoolDomain.SeasonalForage, Season.Spring), pools, Tuning, new Random(seed));
            Assert.DoesNotContain(forage.Slots, s => s.ItemId is "(O)719" or "(O)372");
            Assert.Equal(4, forage.Slots.Count);

            var crops = BundleSlotFiller.Fill(Spec("Spring Crops", 4, 4),
                new DomainMatch(PoolDomain.SeasonalCrops, Season.Spring), pools, Tuning, new Random(seed));
            Assert.DoesNotContain(crops.Slots, s => s.ItemId == "(O)999");

            var generic = BundleSlotFiller.Fill(Spec("Garden", 4, 4),
                new DomainMatch(PoolDomain.SeasonalCrops, null), pools, Tuning, new Random(seed));
            anySeasonCropSeenInGenericBundle |= generic.Slots.Any(s => s.ItemId == "(O)999");
        }
        Assert.True(anySeasonCropSeenInGenericBundle);
    }

    /// <summary>Player report 2026-10 (gmastern1): a Spring Crops bundle asked for Rhubarb. Data/Crops
    /// lists Rhubarb as a Spring crop, but its seeds come from the Oasis and the model dates the
    /// harvest to week 11, so a season-named bundle holding it could never meet its own Spring gate.
    /// A season-named bundle draws only items the model can deliver by the end of its season.</summary>
    [Fact]
    public void SeasonNamedBundle_LeavesOutItemsTheModelDatesAfterItsSeason()
    {
        Season[] spring = { Season.Spring };
        var pools = new ItemPools
        {
            Crops = new[]
            {
                Item("(O)24", seasons: spring), Item("(O)188", seasons: spring),
                Item("(O)190", seasons: spring), Item("(O)192", seasons: spring),
                Item("(O)250", seasons: spring),
                Item("(O)252", weight: 100, seasons: spring), // Rhubarb: Oasis seeds, week 11
            },
        };
        var model = Model(new Dictionary<string, ItemAvailability>
        {
            ["(O)24"] = Avail(1, 1), ["(O)188"] = Avail(1, 1), ["(O)190"] = Avail(2, 2),
            ["(O)192"] = Avail(1, 1), ["(O)250"] = Avail(1, 1), ["(O)252"] = Avail(11, 11),
        });
        for (int seed = 0; seed < 40; seed++)
        {
            BundleSpec filled = BundleSlotFiller.Fill(Spec("Spring Crops", 4, 4),
                new DomainMatch(PoolDomain.SeasonalCrops, Season.Spring), pools, Tuning, new Random(seed),
                availability: model);
            Assert.Equal(4, filled.Slots.Count);
            Assert.DoesNotContain(filled.Slots, s => s.ItemId == "(O)252");
        }
    }

    /// <summary>No item asked twice across the board (Jeff, 2026-08-28: "Flounder on 3 bundles",
    /// "Mussel on 4"). The engine hands each fill the ids every earlier bundle already asks for;
    /// the fill leaves them out while the pool can still fill every slot without them, and only
    /// falls back to the whole pool when it would otherwise run dry.</summary>
    [Fact]
    public void Avoid_LeavesOutItemsOtherBundlesAsk_WhilePoolCanStillFill()
    {
        var pools = new ItemPools
        {
            Crops = Enumerable.Range(0, 12).Select(i => Item($"(O){100 + i}",
                seasons: new[] { Season.Spring })).ToList(),
        };
        var avoid = new HashSet<string> { "(O)100", "(O)101", "(O)102" };
        for (int seed = 0; seed < 40; seed++)
        {
            var filled = BundleSlotFiller.Fill(Spec("Spring Crops", 4, 4),
                new DomainMatch(PoolDomain.SeasonalCrops, Season.Spring), pools, Tuning, new Random(seed),
                avoid: avoid);
            Assert.Equal(4, filled.Slots.Count);
            Assert.DoesNotContain(filled.Slots, s => avoid.Contains(s.ItemId));
        }
    }

    [Fact]
    public void Avoid_FallsBackToWholePool_WhenItWouldRunDry()
    {
        var pools = new ItemPools
        {
            Crops = Enumerable.Range(0, 5).Select(i => Item($"(O){100 + i}",
                seasons: new[] { Season.Spring })).ToList(),
        };
        var avoid = new HashSet<string> { "(O)100", "(O)101" }; // only 3 left, 4 needed
        var filled = BundleSlotFiller.Fill(Spec("Spring Crops", 4, 4),
            new DomainMatch(PoolDomain.SeasonalCrops, Season.Spring), pools, Tuning, new Random(1),
            avoid: avoid);
        Assert.Equal(4, filled.Slots.Count);
        Assert.Equal(4, filled.Slots.Select(s => s.ItemId).Distinct().Count());
    }

    [Fact]
    public void CandidateCount_ReflectsTheDomainFilters()
    {
        var pools = new ItemPools
        {
            Crops = Enumerable.Range(0, 12).Select(i => Item($"(O){100 + i}", seasons: new[] { Season.Spring }))
                .Concat(new[] { Item("(O)900"), Item("(O)901") }) // any-season: not for a season-named bundle
                .ToList(),
        };
        Assert.Equal(12, BundleSlotFiller.CandidateCount(Spec("Spring Crops", 4, 4),
            new DomainMatch(PoolDomain.SeasonalCrops, Season.Spring), pools));
        Assert.Equal(14, BundleSlotFiller.CandidateCount(Spec("Garden", 4, 4),
            new DomainMatch(PoolDomain.SeasonalCrops, null), pools));
        Assert.Equal(0, BundleSlotFiller.CandidateCount(Spec("Vault", 1, 1),
            new DomainMatch(PoolDomain.None, null), pools));
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

    [Fact]
    public void QualityAsk_OnlyForEligibleIds_WhenEligibilityKnown()
    {
        var tuning = new BundleGenerationTuning { GoldQualityChance = 1.0 };
        var pools = new ItemPools
        {
            Forage = new[] { Item("(O)16", seasons: new[] { Season.Spring }), Item("(O)815", seasons: new[] { Season.Spring }) },
            QualityEligibleIds = new HashSet<string> { "(O)16" },
        };
        var filled = BundleSlotFiller.Fill(Spec("Spring Foraging", 2, 2),
            new DomainMatch(PoolDomain.SeasonalForage, Season.Spring), pools, tuning, new Random(3));
        foreach (BundleSlotSpec slot in filled.Slots)
            Assert.Equal(slot.ItemId == "(O)16" ? 2 : 0, slot.Quality);
    }

    [Fact]
    public void QualityAsk_AllowedEverywhere_WhenEligibilityUnknown()
    {
        var tuning = new BundleGenerationTuning { GoldQualityChance = 1.0 };
        var pools = new ItemPools { Forage = new[] { Item("(O)815", seasons: new[] { Season.Spring }) } };   // QualityEligibleIds null
        var filled = BundleSlotFiller.Fill(Spec("Spring Foraging", 1, 1),
            new DomainMatch(PoolDomain.SeasonalForage, Season.Spring), pools, tuning, new Random(3));
        Assert.Equal(2, filled.Slots[0].Quality);
    }

    [Fact]
    public void QualityCrops_IneligibleItemGetsBaseQualityEvenThere()
    {
        var pools = new ItemPools
        {
            Crops = new[] { Item("(O)24", seasons: new[] { Season.Spring }) },
            QualityEligibleIds = new HashSet<string>(),   // known, and nothing is eligible
        };
        var filled = BundleSlotFiller.Fill(Spec("Quality Crops", 1, 1),
            new DomainMatch(PoolDomain.QualityCrops, null), pools, Tuning, new Random(3));
        Assert.Equal(0, filled.Slots[0].Quality);
    }
}
