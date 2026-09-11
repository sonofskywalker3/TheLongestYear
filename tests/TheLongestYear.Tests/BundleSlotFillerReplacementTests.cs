using System;
using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>The one-slot replacement draw used to repair a board that already carries an
/// unreachable ask (spec 2026-09-10-source-reachability, task 9). It has to come from the same
/// pool the slot came from and, for a composite Recipe bundle, from the same PART: Field Research
/// asks for a forage, an artifact, a fish and a mineral, so a fish slot must be replaced by a fish
/// and not merely by something else the bundle could hold.</summary>
public class BundleSlotFillerReplacementTests
{
    private static readonly BundleGenerationTuning Tuning = new();

    private static PoolItem Item(string id, int price = 50, int weight = 3)
        => new(id, price, weight, Array.Empty<Season>(), Array.Empty<string>());

    private static BundleSpec Spec(string name, params string[] ids)
        => new("Pantry", 7, name, name, "O 495 30", 0, ids.Length,
            ids.Select(id => new BundleSlotSpec(id, 1, 0)).ToList());

    private static readonly IReadOnlySet<string> Nothing = new HashSet<string>(StringComparer.Ordinal);

    [Fact]
    public void A_slot_whose_pool_has_candidates_gets_one_the_board_does_not_already_ask_for()
    {
        var pools = new ItemPools
        {
            Metals = new[] { Item("(O)m1"), Item("(O)m2"), Item("(O)m3"), Item("(O)m4") },
        };
        var spec = Spec("Blacksmith's", "(O)m1", "(O)m2");
        var avoid = new HashSet<string>(StringComparer.Ordinal) { "(O)m1", "(O)m2", "(O)m3" };

        PoolItem? pick = BundleSlotFiller.ReplacementFor(
            spec, 0, new DomainMatch(PoolDomain.Metals, null), pools, Tuning, new Random(1),
            avoid, availability: null, knownRecipe: null);

        Assert.NotNull(pick);
        Assert.Equal("(O)m4", pick!.ItemId);
    }

    [Fact]
    public void The_bundles_own_other_slots_are_never_handed_back_as_a_replacement()
    {
        var pools = new ItemPools
        {
            Metals = new[] { Item("(O)m1"), Item("(O)m2"), Item("(O)m3") },
        };
        // Nothing is vetoed by the caller, so only the bundle's own slots stand in the way.
        var spec = Spec("Blacksmith's", "(O)m1", "(O)m2");

        PoolItem? pick = BundleSlotFiller.ReplacementFor(
            spec, 0, new DomainMatch(PoolDomain.Metals, null), pools, Tuning, new Random(4),
            Nothing, availability: null, knownRecipe: null);

        Assert.NotNull(pick);
        Assert.Equal("(O)m3", pick!.ItemId);
    }

    [Fact]
    public void An_exhausted_pool_returns_null_rather_than_repeating_an_ask()
    {
        var pools = new ItemPools
        {
            Metals = new[] { Item("(O)m1"), Item("(O)m2"), Item("(O)m3") },
        };
        var spec = Spec("Blacksmith's", "(O)m1", "(O)m2", "(O)m3");

        PoolItem? pick = BundleSlotFiller.ReplacementFor(
            spec, 0, new DomainMatch(PoolDomain.Metals, null), pools, Tuning, new Random(2),
            Nothing, availability: null, knownRecipe: null);

        Assert.Null(pick);
    }

    [Fact]
    public void A_slot_index_off_the_end_of_the_bundle_returns_null()
    {
        var pools = new ItemPools { Metals = new[] { Item("(O)m1"), Item("(O)m2") } };
        var spec = Spec("Blacksmith's", "(O)m1");

        Assert.Null(BundleSlotFiller.ReplacementFor(
            spec, 5, new DomainMatch(PoolDomain.Metals, null), pools, Tuning, new Random(3),
            Nothing, availability: null, knownRecipe: null));
    }

    /// <summary>Field Research's four parts are Forage, Artifact-or-shell, Fish, Mineral-or-geode,
    /// in that fixed order, so slot 2 is the FISH slot. Replacing it from the recipe's union (the
    /// same domain) would be wrong: only the fish pool may fill it.</summary>
    [Fact]
    public void A_recipe_bundles_slot_is_replaced_from_its_own_part_not_the_whole_recipe()
    {
        var pools = new ItemPools
        {
            Forage = new[] { Item("(O)forage1"), Item("(O)forage2") },
            Artifacts = new[] { Item("(O)artifact1"), Item("(O)artifact2") },
            Fish = new[] { Item("(O)fish1"), Item("(O)fish2") },
            GeodeMinerals = new[] { Item("(O)mineral1"), Item("(O)mineral2") },
        };
        var spec = Spec("Field Research", "(O)forage1", "(O)artifact1", "(O)fish1", "(O)mineral1");

        // Every seed must land in the fish part: the draw is over that part alone.
        for (int seed = 0; seed < 25; seed++)
        {
            PoolItem? pick = BundleSlotFiller.ReplacementFor(
                spec, 2, new DomainMatch(PoolDomain.Recipe, null), pools, Tuning, new Random(seed),
                Nothing, availability: null, knownRecipe: null);

            Assert.NotNull(pick);
            Assert.Equal("(O)fish2", pick!.ItemId);
        }
    }

    /// <summary>The same bundle, a different slot: slot 0 is the FORAGE part, so it must come back
    /// forage. Together with the test above this pins the part mapping, not just "some part".</summary>
    [Fact]
    public void The_same_recipe_bundle_replaces_its_forage_slot_from_the_forage_part()
    {
        var pools = new ItemPools
        {
            Forage = new[] { Item("(O)forage1"), Item("(O)forage2") },
            Artifacts = new[] { Item("(O)artifact1"), Item("(O)artifact2") },
            Fish = new[] { Item("(O)fish1"), Item("(O)fish2") },
            GeodeMinerals = new[] { Item("(O)mineral1"), Item("(O)mineral2") },
        };
        var spec = Spec("Field Research", "(O)forage1", "(O)artifact1", "(O)fish1", "(O)mineral1");

        for (int seed = 0; seed < 25; seed++)
        {
            PoolItem? pick = BundleSlotFiller.ReplacementFor(
                spec, 0, new DomainMatch(PoolDomain.Recipe, null), pools, Tuning, new Random(seed),
                Nothing, availability: null, knownRecipe: null);

            Assert.NotNull(pick);
            Assert.Equal("(O)forage2", pick!.ItemId);
        }
    }

    /// <summary>Field Research's four parts ask for one slot each, so on a SIX-slot bundle they
    /// cannot account for every slot: the reconstructed boundaries are not the ones the bundle was
    /// filled with, whatever they say. Every slot is affected, INCLUDING the ones an early part
    /// appears to cover.</summary>
    private static ItemPools ShortRecipePools() => new()
    {
        Forage = new[] { Item("(O)forage1"), Item("(O)forage2"), Item("(O)forage3") },
        Artifacts = new[] { Item("(O)artifact1"), Item("(O)artifact2") },
        Fish = new[] { Item("(O)fish1"), Item("(O)fish2"), Item("(O)fish3") },
        GeodeMinerals = new[] { Item("(O)mineral1"), Item("(O)mineral2") },
    };

    private static BundleSpec ShortRecipeSpec() => Spec("Field Research",
        "(O)forage1", "(O)artifact1", "(O)fish1", "(O)mineral1", "(O)forage2", "(O)fish2");

    /// <summary>The candidates the fixture leaves free: one per part, so a draw confined to a
    /// single part can only ever return one id, and a union draw returns several.</summary>
    private static readonly IReadOnlySet<string> ShortRecipeFree = new HashSet<string>(StringComparer.Ordinal)
    {
        "(O)forage3", "(O)artifact2", "(O)fish3", "(O)mineral2",
    };

    private static void AssertUnionDraw(int slotIndex)
    {
        ItemPools pools = ShortRecipePools();
        BundleSpec spec = ShortRecipeSpec();
        var notes = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        for (int seed = 0; seed < 40; seed++)
        {
            PoolItem? pick = BundleSlotFiller.ReplacementFor(
                spec, slotIndex, new DomainMatch(PoolDomain.Recipe, null), pools, Tuning,
                new Random(seed), Nothing, availability: null, knownRecipe: null, log: notes.Add);

            Assert.NotNull(pick);
            Assert.Contains(pick!.ItemId, ShortRecipeFree);
            seen.Add(pick.ItemId);
        }

        Assert.True(seen.Count > 1,
            $"slot {slotIndex} drew from a single part ({string.Join(",", seen)}), not the union.");
        Assert.Contains(notes, n => n.Contains("could not identify which recipe part"));
    }

    /// <summary>The regression this fix exists for. Slot 1 sits INSIDE what part 1 appears to
    /// cover, so a walk that returns the moment `slotIndex &lt; filled` answers "part 1" and never
    /// notices that the parts account for only four of the bundle's six slots. That answer is
    /// wrong: the boundaries it came from are not the ones the bundle was filled with. The draw
    /// must union instead.
    ///
    /// Deliberately NOT slot 4: that index falls past every part's covered range, so even the
    /// early-returning walk fell through to -1 and unioned already. A test on slot 4 passes
    /// against the unfixed code and protects nothing.</summary>
    [Fact]
    public void A_slot_inside_an_early_part_still_unions_when_the_parts_are_structurally_short()
        => AssertUnionDraw(1);

    /// <summary>The other half: a slot past every part's covered range. Already handled before the
    /// coverage check existed, kept so the fall-through path stays covered too.</summary>
    [Fact]
    public void A_slot_past_every_part_unions_when_the_parts_are_structurally_short()
        => AssertUnionDraw(4);

    [Fact]
    public void A_domain_of_none_is_never_repaired()
    {
        var pools = new ItemPools { Metals = new[] { Item("(O)m1"), Item("(O)m2") } };
        var spec = Spec("Vault", "-1");

        Assert.Null(BundleSlotFiller.ReplacementFor(
            spec, 0, new DomainMatch(PoolDomain.None, null), pools, Tuning, new Random(5),
            Nothing, availability: null, knownRecipe: null));
    }

    /// <summary>A legendary fish is never a repair pick: the board's legendary allowance was spent
    /// when the board was generated, and this pass cannot know what is left of it.</summary>
    [Fact]
    public void A_legendary_fish_is_never_drawn_as_a_replacement()
    {
        var pools = new ItemPools
        {
            Fish = new[] { Item("(O)163"), Item("(O)159"), Item("(O)160"), Item("(O)145") },
        };
        var spec = Spec("Lake Fish", "(O)145");

        for (int seed = 0; seed < 25; seed++)
        {
            PoolItem? pick = BundleSlotFiller.ReplacementFor(
                spec, 0, new DomainMatch(PoolDomain.Fish, null), pools, Tuning, new Random(seed),
                Nothing, availability: null, knownRecipe: null);

            Assert.Null(pick);
        }
    }
}
