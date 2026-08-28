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
    public void Fill_keeps_at_least_one_spring_item_when_the_pool_has_one()
    {
        var pools = new ItemPools { Metals = new[] { Item("(O)386", weight: 100), Item("(O)384", weight: 100), Item("(O)337", weight: 100), Item("(O)378", weight: 1) } };
        var spec = Spec("Blacksmith's", 3);
        var filled = BundleSlotFiller.Fill(spec, new DomainMatch(PoolDomain.Metals, null), pools, Tuning, new Random(7),
            springReady: id => id == "(O)378");
        Assert.Contains(filled.Slots, s => s.ItemId == "(O)378");
        Assert.Equal(3, filled.Slots.Select(s => s.ItemId).Distinct().Count());
    }

    [Fact]
    public void Fill_leaves_a_season_named_bundle_alone()
    {
        var pools = new ItemPools { Forage = new[] { Item("(O)406", seasons: new[] { Season.Fall }), Item("(O)408", seasons: new[] { Season.Fall }), Item("(O)410", seasons: new[] { Season.Fall }), Item("(O)16", seasons: new[] { Season.Fall, Season.Spring }, weight: 1) } };
        var spec = Spec("Fall Foraging", 3);
        var filled = BundleSlotFiller.Fill(spec, new DomainMatch(PoolDomain.SeasonalForage, Season.Fall), pools, Tuning, new Random(3),
            springReady: id => id == "(O)16");
        Assert.Equal(3, filled.Slots.Count);
        // No forced swap toward the one Spring-capable item.
        Assert.True(filled.Slots.Count(s => s.ItemId == "(O)16") <= 1);
    }

    [Fact]
    public void Fill_without_a_spring_candidate_still_fills()
    {
        var pools = new ItemPools { Metals = new[] { Item("(O)386"), Item("(O)384"), Item("(O)337") } };
        var spec = Spec("Blacksmith's", 3);
        var filled = BundleSlotFiller.Fill(spec, new DomainMatch(PoolDomain.Metals, null), pools, Tuning, new Random(7),
            springReady: _ => false);
        Assert.Equal(3, filled.Slots.Count);
    }

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

    private static readonly RarityThresholds Thresholds = new();

    [Fact]
    public void Trim_removes_hardest_items_from_candidates_for_matching_season()
    {
        var pools = new ItemPools
        {
            Crops = Enumerable.Range(0, 8).Select(i => Item($"(O){100 + i}", price: 10 + i * 100,
                seasons: new[] { Season.Spring })).ToList(),
        };
        var spec = Spec("Spring Crops", 4, numberOfSlots: 4);
        // QualityCrops (rather than SeasonalCrops) makes the quality-off assertion load-bearing:
        // RollQuality always asks gold for this domain, so an untrimmed fill would show Quality 2
        // on every slot; only the trim's quality-off unit can bring that back to 0.
        var match = new DomainMatch(PoolDomain.QualityCrops, Season.Spring);
        // 3 units: 1 spent on quality-off, 2 remove the two priciest items.
        var filled = BundleSlotFiller.Fill(spec, match, pools, Tuning, new Random(5),
            new PityTrim(Season.Spring, 3), Thresholds);
        Assert.DoesNotContain(filled.Slots, s => s.ItemId is "(O)107" or "(O)106");
        Assert.All(filled.Slots, s => Assert.Equal(0, s.Quality));
    }

    [Fact]
    public void Trim_ignores_bundles_for_other_seasons_and_applies_to_season_agnostic_pools()
    {
        var pools = new ItemPools
        {
            Crops = Enumerable.Range(0, 8).Select(i => Item($"(O){100 + i}", price: 10 + i * 100,
                seasons: new[] { Season.Summer })).ToList(),
            Metals = Enumerable.Range(0, 6).Select(i => Item($"(O){200 + i}", price: 10 + i * 150)).ToList(),
        };
        var summer = BundleSlotFiller.Fill(Spec("Summer Crops", 4, 4), new DomainMatch(PoolDomain.SeasonalCrops, Season.Summer),
            pools, Tuning, new Random(5), new PityTrim(Season.Spring, 4), Thresholds);
        var plain = BundleSlotFiller.Fill(Spec("Summer Crops", 4, 4), new DomainMatch(PoolDomain.SeasonalCrops, Season.Summer),
            pools, Tuning, new Random(5));
        Assert.Equal(plain.Slots, summer.Slots);

        var metals = BundleSlotFiller.Fill(Spec("Blacksmith's", 3, 3), new DomainMatch(PoolDomain.Metals, null),
            pools, Tuning, new Random(5), new PityTrim(Season.Spring, 2), Thresholds);
        Assert.DoesNotContain(metals.Slots, s => s.ItemId is "(O)205" or "(O)204");
    }

    [Fact]
    public void Trim_never_starves_the_bundle_below_its_slot_count()
    {
        var pools = new ItemPools
        {
            Metals = Enumerable.Range(0, 4).Select(i => Item($"(O){200 + i}", price: 10 + i * 150)).ToList(),
        };
        var spec = Spec("Blacksmith's", 3, 3);
        var filled = BundleSlotFiller.Fill(spec, new DomainMatch(PoolDomain.Metals, null),
            pools, Tuning, new Random(5), new PityTrim(Season.Spring, 10), Thresholds);
        Assert.NotSame(spec, filled);              // still filled (guard stopped at 3 candidates)
        Assert.Equal(3, filled.Slots.Count);
        Assert.DoesNotContain(filled.Slots, s => s.ItemId == "(O)203");
    }

    [Fact]
    public void Fill_logs_the_trim_before_and_after_counts_and_flags_the_guard()
    {
        var pools = new ItemPools
        {
            Metals = Enumerable.Range(0, 4).Select(i => Item($"(O){200 + i}", price: 10 + i * 150)).ToList(),
        };
        var spec = Spec("Blacksmith's", 3, 3);
        var messages = new List<string>();
        var filled = BundleSlotFiller.Fill(spec, new DomainMatch(PoolDomain.Metals, null),
            pools, Tuning, new Random(5), new PityTrim(Season.Spring, 10), Thresholds, messages.Add);
        Assert.NotSame(spec, filled);
        Assert.Single(messages);
        Assert.Contains("4 candidates -> 3", messages[0]);
        Assert.Contains("need 3", messages[0]);
        Assert.Contains("guard stopped early", messages[0]);
    }

    [Fact]
    public void DomainRollsQuality_matches_RollQuality_domains()
    {
        Assert.True(BundleSlotFiller.DomainRollsQuality(PoolDomain.QualityCrops));
        Assert.True(BundleSlotFiller.DomainRollsQuality(PoolDomain.Fish));
        Assert.False(BundleSlotFiller.DomainRollsQuality(PoolDomain.Metals));
        Assert.False(BundleSlotFiller.DomainRollsQuality(PoolDomain.ArtisanGoods));
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
