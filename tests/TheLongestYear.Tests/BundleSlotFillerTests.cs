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
    public void A_bundle_with_nothing_for_spring_swaps_in_a_stretch_item_not_a_spring_item()
    {
        // Three Winter items (heavy weight) crowd the raw roll; a Spring-stretch item ("(O)s",
        // pacing week 6 / hard week 1) and a true Spring item ("(O)p", week 1) sit in the pool at
        // low weight so the raw roll is expected to miss both.
        var pools = new ItemPools
        {
            Metals = new[]
            {
                Item("(O)w1", weight: 100), Item("(O)w2", weight: 100), Item("(O)w3", weight: 100),
                Item("(O)s", weight: 1), Item("(O)p", weight: 1),
            },
        };
        var model = Model(new Dictionary<string, ItemAvailability>
        {
            ["(O)w1"] = Avail(13, 13),
            ["(O)w2"] = Avail(13, 13),
            ["(O)w3"] = Avail(13, 13),
            ["(O)s"] = Avail(6, 1),
            ["(O)p"] = Avail(1, 1),
        });
        var spec = Spec("Blacksmith's", 3);
        var match = new DomainMatch(PoolDomain.Metals, null);

        BundleSpec raw = BundleSlotFiller.Fill(spec, match, pools, Tuning, new Random(1));
        Assert.DoesNotContain(raw.Slots, s => s.ItemId == "(O)s"); // RED precondition: no swap logic yet

        BundleSpec filled = BundleSlotFiller.Fill(spec, match, pools, Tuning, new Random(1), availability: model);
        Assert.Contains(filled.Slots, s => s.ItemId == "(O)s");
        Assert.DoesNotContain(filled.Slots, s => s.ItemId == "(O)p");
    }

    [Fact]
    public void Easy_gets_no_swap_at_all()
    {
        var pools = new ItemPools
        {
            Metals = new[]
            {
                Item("(O)w1", weight: 100), Item("(O)w2", weight: 100), Item("(O)w3", weight: 100),
                Item("(O)s", weight: 1), Item("(O)p", weight: 1),
            },
        };
        var model = Model(new Dictionary<string, ItemAvailability>
        {
            ["(O)w1"] = Avail(13, 13),
            ["(O)w2"] = Avail(13, 13),
            ["(O)w3"] = Avail(13, 13),
            ["(O)s"] = Avail(6, 1),
            ["(O)p"] = Avail(1, 1),
        }, DifficultyStep.Easy);
        var spec = Spec("Blacksmith's", 3);
        var match = new DomainMatch(PoolDomain.Metals, null);

        BundleSpec raw = BundleSlotFiller.Fill(spec, match, pools, Tuning, new Random(1));
        BundleSpec filled = BundleSlotFiller.Fill(spec, match, pools, Tuning, new Random(1), availability: model);
        Assert.Equal(raw.Slots.Select(s => s.ItemId), filled.Slots.Select(s => s.ItemId));
    }

    [Fact]
    public void A_four_slot_bundle_without_a_hard_item_swaps_one_in()
    {
        var pools = new ItemPools
        {
            Metals = new[]
            {
                Item("(O)e1", weight: 100), Item("(O)e2", weight: 100), Item("(O)e3", weight: 100),
                Item("(O)e4", weight: 100), Item("(O)e5", weight: 100), Item("(O)h", weight: 1),
            },
        };
        var model = Model(new Dictionary<string, ItemAvailability>
        {
            ["(O)e1"] = Avail(1, 1, effort: 2),
            ["(O)e2"] = Avail(1, 1, effort: 2),
            ["(O)e3"] = Avail(1, 1, effort: 2),
            ["(O)e4"] = Avail(1, 1, effort: 2),
            ["(O)e5"] = Avail(1, 1, effort: 2),
            ["(O)h"] = Avail(1, 1, effort: 7),
        });
        var spec = Spec("Blacksmith's", 4);
        var match = new DomainMatch(PoolDomain.Metals, null);

        BundleSpec raw = BundleSlotFiller.Fill(spec, match, pools, Tuning, new Random(1));
        Assert.DoesNotContain(raw.Slots, s => s.ItemId == "(O)h"); // RED precondition: no swap logic yet

        BundleSpec filled = BundleSlotFiller.Fill(spec, match, pools, Tuning, new Random(1), availability: model);
        Assert.Contains(filled.Slots, s => model.For(s.ItemId).Effort >= 6);
    }

    /// <summary>The slot count is the only thing separating the two fills here: same pool, same
    /// model, same seed. The 4-slot bundle must gain the hard item; the 3-slot one must not, so
    /// exactly one of the two boards ends up holding it.</summary>
    [Fact]
    public void A_three_slot_bundle_is_exempt_from_the_hard_item_rule()
    {
        var pools = new ItemPools
        {
            Metals = new[]
            {
                Item("(O)e1", weight: 100), Item("(O)e2", weight: 100), Item("(O)e3", weight: 100),
                Item("(O)e4", weight: 100), Item("(O)e5", weight: 100), Item("(O)h", weight: 1),
            },
        };
        var model = Model(new Dictionary<string, ItemAvailability>
        {
            ["(O)e1"] = Avail(1, 1, effort: 2),
            ["(O)e2"] = Avail(1, 1, effort: 2),
            ["(O)e3"] = Avail(1, 1, effort: 2),
            ["(O)e4"] = Avail(1, 1, effort: 2),
            ["(O)e5"] = Avail(1, 1, effort: 2),
            ["(O)h"] = Avail(1, 1, effort: 7),
        });
        var match = new DomainMatch(PoolDomain.Metals, null);

        BundleSpec four = BundleSlotFiller.Fill(
            Spec("Blacksmith's", BundleSlotFiller.MinSlotsForHardItem), match, pools, Tuning, new Random(1), availability: model);
        BundleSpec three = BundleSlotFiller.Fill(
            Spec("Blacksmith's", BundleSlotFiller.MinSlotsForHardItem - 1), match, pools, Tuning, new Random(1), availability: model);

        Assert.Contains(four.Slots, s => s.ItemId == "(O)h");
        Assert.DoesNotContain(three.Slots, s => s.ItemId == "(O)h");
    }

    /// <summary>The hard-item rule is not a stretch mechanism, so making the stretch rule
    /// pacing-mode-only must not switch it off on Hard and Extreme (whose models read hard weeks):
    /// that would leave those boards easier than Normal. Off on Easy, on everywhere else.</summary>
    [Theory]
    [InlineData(DifficultyStep.Easy, false)]
    [InlineData(DifficultyStep.Normal, true)]
    [InlineData(DifficultyStep.Hard, true)]
    [InlineData(DifficultyStep.Extreme, true)]
    public void The_hard_item_rule_survives_every_step_above_easy(DifficultyStep step, bool expectHard)
    {
        var pools = new ItemPools
        {
            Metals = new[]
            {
                Item("(O)e1", weight: 100), Item("(O)e2", weight: 100), Item("(O)e3", weight: 100),
                Item("(O)e4", weight: 100), Item("(O)e5", weight: 100), Item("(O)h", weight: 1),
            },
        };
        var model = Model(new Dictionary<string, ItemAvailability>
        {
            ["(O)e1"] = Avail(1, 1, effort: 2),
            ["(O)e2"] = Avail(1, 1, effort: 2),
            ["(O)e3"] = Avail(1, 1, effort: 2),
            ["(O)e4"] = Avail(1, 1, effort: 2),
            ["(O)e5"] = Avail(1, 1, effort: 2),
            ["(O)h"] = Avail(1, 1, effort: 7),
        }, step);

        BundleSpec filled = BundleSlotFiller.Fill(Spec("Blacksmith's", 4),
            new DomainMatch(PoolDomain.Metals, null), pools, Tuning, new Random(1), availability: model);

        Assert.Equal(expectHard, filled.Slots.Any(s => s.ItemId == "(O)h"));
        Assert.Equal(expectHard, BundleSlotFiller.HardItemRuleApplies(model));
    }

    /// <summary>The hard swap takes out the EASIEST slot, which is usually the only thing the
    /// bundle could reach early — so the swap itself can be what empties a season. Here the roll
    /// holds exactly one Spring-reachable item ("(O)p", the lowest effort and therefore the hard
    /// swap's victim) and no hard item; a Spring stretch ("(O)s") waits in the pool. After the
    /// hard swap the filler must re-run the stretch pass, so every fill still ends up holding
    /// something reachable in Spring or a Spring stretch line. Before the re-run this failed on
    /// the seeds whose raw roll drew (O)p plus three Winter items.</summary>
    [Fact]
    public void The_hard_swap_never_leaves_a_season_with_no_reachable_item_and_no_stretch()
    {
        var pools = new ItemPools
        {
            Metals = new[]
            {
                Item("(O)p", weight: 100),  // Spring-reachable, lowest effort: the hard swap's victim
                Item("(O)a1", weight: 100), Item("(O)a2", weight: 100), Item("(O)a3", weight: 100),
                Item("(O)s", weight: 1),    // Spring stretch: pacing week 6, hard week 1
                Item("(O)h", weight: 1),    // the hard item
            },
        };
        var model = Model(new Dictionary<string, ItemAvailability>
        {
            ["(O)p"] = Avail(1, 1, effort: 1),
            ["(O)a1"] = Avail(13, 13, effort: 2),
            ["(O)a2"] = Avail(13, 13, effort: 2),
            ["(O)a3"] = Avail(13, 13, effort: 2),
            ["(O)s"] = Avail(6, 1, effort: 2),
            ["(O)h"] = Avail(13, 13, effort: 7),
        });
        var match = new DomainMatch(PoolDomain.Metals, null);

        for (int seed = 0; seed < 40; seed++)
        {
            BundleSpec filled = BundleSlotFiller.Fill(
                Spec("Blacksmith's", 4), match, pools, Tuning, new Random(seed), availability: model);
            Assert.Contains(filled.Slots, s =>
                StretchRule.IsReachable(model.For(s.ItemId), Season.Spring)
                || StretchRule.IsStretchFor(model.For(s.ItemId), Season.Spring));
        }
    }

    /// <summary>A season-named bundle gates its own season by nature, so neither swap touches it:
    /// with a model and without it, the same seed composes the same slots.</summary>
    [Fact]
    public void A_season_named_bundle_is_untouched_by_the_swaps()
    {
        var fall = new[] { Season.Fall };
        var pools = new ItemPools
        {
            Forage = new[]
            {
                Item("(O)404", seasons: fall), Item("(O)420", seasons: fall),
                Item("(O)422", seasons: fall), Item("(O)281", seasons: fall),
                Item("(O)h", seasons: fall),
            },
        };
        var model = Model(new Dictionary<string, ItemAvailability>
        {
            ["(O)404"] = Avail(9, 9, effort: 2),
            ["(O)420"] = Avail(9, 9, effort: 2),
            ["(O)422"] = Avail(9, 9, effort: 2),
            ["(O)281"] = Avail(9, 9, effort: 2),
            ["(O)h"] = Avail(9, 9, effort: 7),
        });
        var spec = Spec("Fall Foraging", 4);
        var match = new DomainMatch(PoolDomain.SeasonalForage, Season.Fall);

        BundleSpec plain = BundleSlotFiller.Fill(spec, match, pools, Tuning, new Random(7));
        BundleSpec withModel = BundleSlotFiller.Fill(spec, match, pools, Tuning, new Random(7), availability: model);
        Assert.Equal(plain.Slots, withModel.Slots);
    }

    /// <summary>A pool with nothing to stretch and nothing hard is not an error: the bundle fills
    /// anyway, and the filler says why it could not swap so the gate audit's later complaint about
    /// this bundle has an explanation in the same log.</summary>
    [Fact]
    public void A_pool_with_no_stretch_candidate_still_fills_and_logs_it()
    {
        var pools = new ItemPools
        {
            Metals = new[]
            {
                Item("(O)a1"), Item("(O)a2"), Item("(O)a3"), Item("(O)a4"),
            },
        };
        var model = Model(new Dictionary<string, ItemAvailability>
        {
            ["(O)a1"] = Avail(13, 13, effort: 2),
            ["(O)a2"] = Avail(13, 13, effort: 2),
            ["(O)a3"] = Avail(13, 13, effort: 2),
            ["(O)a4"] = Avail(13, 13, effort: 2),
        });
        var spec = Spec("Blacksmith's", 4);
        var messages = new List<string>();

        BundleSpec filled = BundleSlotFiller.Fill(spec, new DomainMatch(PoolDomain.Metals, null),
            pools, Tuning, new Random(1), log: messages.Add, availability: model);

        Assert.NotSame(spec, filled);
        Assert.Equal(4, filled.Slots.Count);
        foreach (Season season in StretchRule.StretchSeasons)
            Assert.Contains(messages, m => m.Contains($"no stretch item for {season}"));
        Assert.Contains(messages, m => m.Contains("no hard item"));
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

    // ---- Recipe domain (Plan 3 Task 5) ----

    /// <summary>Pool items outweigh the bundle's own vanilla items (which every part also keeps,
    /// at the synthesized weight 1) by 1000 to 1, so a seeded roll draws the pool, not the
    /// fallback: these tests are about which pool each part reads.</summary>
    private static ItemPools RecipePools() => new()
    {
        ByKind = new Dictionary<ItemKind, IReadOnlyList<PoolItem>>
        {
            [ItemKind.Gem] = new[]
            {
                Item("(O)60", weight: 1000), Item("(O)62", weight: 1000), Item("(O)64", weight: 1000),
                Item("(O)72", weight: 1000), Item("(O)80", weight: 1000),
            },
        },
        ColourTags = new Dictionary<string, IReadOnlyList<PoolItem>>(StringComparer.Ordinal)
        {
            ["color_red"] = new[] { Item("(O)r1", weight: 1000), Item("(O)r2", weight: 1000) },
            ["color_purple"] = new[] { Item("(O)p1", weight: 1000) },
            ["color_yellow"] = new[] { Item("(O)y1", weight: 1000) },
            ["color_white"] = new[] { Item("(O)w1", weight: 1000) },
            ["color_blue"] = new[] { Item("(O)b1", weight: 1000) },
            ["color_green"] = new[] { Item("(O)g1", weight: 1000) },
        },
    };

    private static readonly DomainMatch RecipeMatch = new(PoolDomain.Recipe, null);

    [Fact]
    public void Dye_takes_one_item_from_each_colour_part()
    {
        var filled = BundleSlotFiller.Fill(Spec("Dye", 6, 6), RecipeMatch, RecipePools(), Tuning, new Random(7));
        var ids = filled.Slots.Select(s => s.ItemId).ToList();
        Assert.Equal(6, ids.Count);
        Assert.Contains(ids, id => id is "(O)r1" or "(O)r2");
        foreach (string only in new[] { "(O)p1", "(O)y1", "(O)w1", "(O)b1", "(O)g1" })
            Assert.Contains(only, ids);
    }

    [Fact]
    public void A_named_recipe_rolls_its_own_pool_not_the_vanilla_items()
    {
        var filled = BundleSlotFiller.Fill(Spec("Treasure Hunter's", 3, 3, "(O)9001", "(O)9002", "(O)9003"),
            RecipeMatch, RecipePools(), Tuning, new Random(11));
        Assert.Contains(filled.Slots, s => s.ItemId is "(O)60" or "(O)62" or "(O)64" or "(O)72" or "(O)80");
    }

    [Fact]
    public void A_part_whose_pool_is_too_small_fills_from_the_bundles_own_items()
    {
        var pools = new ItemPools
        {
            ByKind = new Dictionary<ItemKind, IReadOnlyList<PoolItem>>
            {
                [ItemKind.Gem] = new[] { Item("(O)60", weight: 1000) },
            },
        };
        var filled = BundleSlotFiller.Fill(Spec("Treasure Hunter's", 3, 3, "(O)9001", "(O)9002", "(O)9003"),
            RecipeMatch, pools, Tuning, new Random(5));
        var ids = filled.Slots.Select(s => s.ItemId).ToList();
        Assert.Equal(3, ids.Count);
        Assert.Equal(3, ids.Distinct().Count());
        Assert.Contains("(O)60", ids);
        // Two of the three slots can only have come from the bundle's own items.
        Assert.Equal(2, ids.Count(id => id.StartsWith("(O)900", StringComparison.Ordinal)));
    }

    [Fact]
    public void A_recipe_bundle_with_nothing_to_roll_keeps_its_vanilla_slots()
    {
        var filled = BundleSlotFiller.Fill(Spec("Some Unknown Bundle", 3, 3, "(O)9001"),
            RecipeMatch, new ItemPools(), Tuning, new Random(5));
        Assert.Single(filled.Slots);
        Assert.Equal("(O)9001", filled.Slots[0].ItemId);
    }

    [Fact]
    public void The_same_seed_composes_the_same_recipe_bundle_twice()
    {
        BundleSpec spec = Spec("Dye", 6, 6);
        var a = BundleSlotFiller.Fill(spec, RecipeMatch, RecipePools(), Tuning, new Random(21));
        var b = BundleSlotFiller.Fill(spec, RecipeMatch, RecipePools(), Tuning, new Random(21));
        Assert.Equal(a.Slots.Select(s => s.ItemId), b.Slots.Select(s => s.ItemId));
    }
}
