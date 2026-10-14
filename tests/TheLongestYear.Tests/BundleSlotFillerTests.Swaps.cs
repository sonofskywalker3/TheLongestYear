using System;
using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public partial class BundleSlotFillerTests
{
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
    /// bundle could reach early, so the swap itself can be what empties a season. Here the roll
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
}
