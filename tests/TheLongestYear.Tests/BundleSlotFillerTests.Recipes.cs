using System;
using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public partial class BundleSlotFillerTests
{
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

    // ---- Recipe stack and quality (Plan 3 Task 6 ruling) ----

    [Fact]
    public void A_recipe_whose_dominant_part_is_not_fish_crops_or_forage_asks_for_one_plain_item()
    {
        // Treasure Hunter's rolls gems: no stack, no quality star, whatever the quality dice say.
        var tuning = new BundleGenerationTuning { GoldQualityChance = 1.0, SilverQualityChance = 1.0 };
        var filled = BundleSlotFiller.Fill(Spec("Treasure Hunter's", 3, 3, "(O)9001", "(O)9002", "(O)9003"),
            RecipeMatch, RecipePools(), tuning, new Random(11));
        Assert.All(filled.Slots, s => Assert.Equal(1, s.Stack));
        Assert.All(filled.Slots, s => Assert.Equal(0, s.Quality));
    }

    [Fact]
    public void A_forage_recipe_rolls_quality_with_the_forage_domain()
    {
        var pools = new ItemPools
        {
            Forage = new[] { Item("(O)f1", weight: 1000), Item("(O)f2", weight: 1000), Item("(O)f3", weight: 1000) },
        };
        var tuning = new BundleGenerationTuning
        {
            GoldQualityChance = 1.0,
            LargeQuantityForageChance = 0.0, // the large-stack ask is a SeasonalForage-domain rule
        };
        var filled = BundleSlotFiller.Fill(Spec("Forager's", 3, 3, "(O)9001", "(O)9002", "(O)9003"),
            RecipeMatch, pools, tuning, new Random(4));
        Assert.All(filled.Slots, s => Assert.StartsWith("(O)f", s.ItemId, StringComparison.Ordinal));
        Assert.All(filled.Slots, s => Assert.Equal(2, s.Quality));
    }

    [Fact]
    public void A_re_drawn_vanilla_id_keeps_the_stack_and_quality_the_vanilla_slot_had()
    {
        // Nothing in the pools, so every part falls back to the bundle's own items: they must come
        // back with vanilla's own ask (x5 gold), not a fresh roll.
        var spec = new BundleSpec("Pantry", 0, "Forager's", "Forager's", "O 495 30", 0, 2,
            new[] { new BundleSlotSpec("(O)9001", 5, 2), new BundleSlotSpec("(O)9002", 3, 1) });
        var tuning = new BundleGenerationTuning { GoldQualityChance = 0.0, SilverQualityChance = 0.0 };
        var filled = BundleSlotFiller.Fill(spec, RecipeMatch, new ItemPools(), tuning, new Random(9));
        Assert.Equal(2, filled.Slots.Count);
        Assert.Equal((5, 2), (filled.Slots.Single(s => s.ItemId == "(O)9001").Stack,
                              filled.Slots.Single(s => s.ItemId == "(O)9001").Quality));
        Assert.Equal((3, 1), (filled.Slots.Single(s => s.ItemId == "(O)9002").Stack,
                              filled.Slots.Single(s => s.ItemId == "(O)9002").Quality));
    }

    /// <summary>The same rule on a LEGACY domain, not just Recipe: a Metals bundle whose roll can
    /// only land on the two items it already asked for must give them vanilla's own stack and
    /// quality back, exactly as the recipe path does. Before the fix the vanilla-slot map was
    /// built for recipes only, so these two came back x1 plain.</summary>
    [Fact]
    public void A_re_drawn_vanilla_id_keeps_its_vanilla_stack_on_a_legacy_domain_too()
    {
        var spec = new BundleSpec("Pantry", 0, "Blacksmith's", "Blacksmith's", "O 495 30", 0, 2,
            new[] { new BundleSlotSpec("(O)380", 5, 2), new BundleSlotSpec("(O)384", 3, 1) });
        var pools = new ItemPools { Metals = new[] { Item("(O)380"), Item("(O)384") } };
        var tuning = new BundleGenerationTuning { GoldQualityChance = 0.0, SilverQualityChance = 0.0 };

        BundleSpec filled = BundleSlotFiller.Fill(
            spec, new DomainMatch(PoolDomain.Metals, null), pools, tuning, new Random(9));

        Assert.Equal(2, filled.Slots.Count);
        Assert.Equal((5, 2), (filled.Slots.Single(s => s.ItemId == "(O)380").Stack,
                              filled.Slots.Single(s => s.ItemId == "(O)380").Quality));
        Assert.Equal((3, 1), (filled.Slots.Single(s => s.ItemId == "(O)384").Stack,
                              filled.Slots.Single(s => s.ItemId == "(O)384").Quality));
    }

    [Fact]
    public void CandidateCount_counts_the_pool_the_model_selects()
    {
        var pools = new ItemPools
        {
            ByKind = new Dictionary<ItemKind, IReadOnlyList<PoolItem>>
            {
                [ItemKind.Gem] = new[] { Item("(O)60"), Item("(O)62"), Item("(O)64") },
            },
        };
        var model = Model(new Dictionary<string, ItemAvailability>
        {
            ["(O)60"] = Avail(1, 1, effort: 10),
            ["(O)62"] = Avail(1, 1, effort: 10),
            ["(O)64"] = Avail(1, 1, effort: 2),
        });
        BundleSpec spec = Spec("The Missing", 2, 2, "(O)9001", "(O)9002");
        // Without the model The Missing has no extreme band to read and counts its own two items;
        // with it, the two effort-10 gems join them.
        Assert.Equal(2, BundleSlotFiller.CandidateCount(spec, RecipeMatch, pools));
        Assert.Equal(4, BundleSlotFiller.CandidateCount(spec, RecipeMatch, pools, model));
    }

    [Fact]
    public void The_same_seed_composes_the_same_recipe_bundle_twice()
    {
        BundleSpec spec = Spec("Dye", 6, 6);
        var a = BundleSlotFiller.Fill(spec, RecipeMatch, RecipePools(), Tuning, new Random(21));
        var b = BundleSlotFiller.Fill(spec, RecipeMatch, RecipePools(), Tuning, new Random(21));
        Assert.Equal(a.Slots.Select(s => s.ItemId), b.Slots.Select(s => s.ItemId));
    }

    /// <summary>The Prismatic Shard / Mystery Box board allowance (CappedAsks): a fill handed a
    /// budget of none never lets a capped id through, however heavily the pool weights it.</summary>
    [Fact]
    public void Fill_never_passes_the_capped_budget()
    {
        var pools = new ItemPools
        {
            Metals = new[] { Item("(O)74", weight: 1000), Item("(O)m1"), Item("(O)m2"), Item("(O)m3"), Item("(O)m4") },
        };
        var spec = Spec("Blacksmith's", 4);
        var budget = new Dictionary<string, int> { [CappedAsks.PrismaticShard] = 0, [CappedAsks.MysteryBox] = 0 };
        for (int seed = 0; seed < 50; seed++)
        {
            BundleSpec filled = BundleSlotFiller.Fill(spec, new DomainMatch(PoolDomain.Metals, null), pools, Tuning,
                new Random(seed), cappedBudget: budget);
            Assert.NotSame(spec, filled);
            Assert.Equal(4, filled.Slots.Count);
            Assert.DoesNotContain(filled.Slots, s => s.ItemId == CappedAsks.PrismaticShard);
        }
    }

    [Fact]
    public void Fill_keeps_a_capped_item_the_budget_still_allows()
    {
        var pools = new ItemPools
        {
            Metals = new[] { Item("(O)74", weight: 100000), Item("(O)m1"), Item("(O)m2"), Item("(O)m3"), Item("(O)m4") },
        };
        var budget = new Dictionary<string, int> { [CappedAsks.PrismaticShard] = 1 };

        BundleSpec filled = BundleSlotFiller.Fill(Spec("Blacksmith's", 4), new DomainMatch(PoolDomain.Metals, null),
            pools, Tuning, new Random(3), cappedBudget: budget);

        Assert.Single(filled.Slots, s => s.ItemId == CappedAsks.PrismaticShard);
    }
}
