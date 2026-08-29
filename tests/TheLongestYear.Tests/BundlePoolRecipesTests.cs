using System;
using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class BundlePoolRecipesTests
{
    private static PoolItem Item(string id, int price = 50, int weight = 3, int category = 0)
        => new(id, price, weight, Array.Empty<Season>(), Array.Empty<string>(), category);

    private static ItemPools Pools() => new()
    {
        Fish = new[] { Item("(O)128"), Item("(O)129") },
        Forage = new[] { Item("(O)16"), Item("(O)18"), Item("(O)404") },
        Crops = new[] { Item("(O)24"), Item("(O)262") },
        ArtisanGoods = new[] { Item("(O)340"), Item("(O)344") },
        ByKind = new Dictionary<ItemKind, IReadOnlyList<PoolItem>>
        {
            [ItemKind.Gem] = new[] { Item("(O)60"), Item("(O)62"), Item("(O)64"), Item("(O)72"), Item("(O)80") },
            [ItemKind.Resource] = new[] { Item("(O)388"), Item("(O)390"), Item("(O)771") },
            [ItemKind.MonsterLoot] = new[] { Item("(O)766"), Item("(O)768"), Item("(O)769") },
            [ItemKind.Totem] = new[] { Item("(O)681"), Item("(O)688") },
            [ItemKind.Egg] = new[] { Item("(O)176") },
            [ItemKind.Milk] = new[] { Item("(O)184") },
            [ItemKind.AnimalProduct] = new[] { Item("(O)440") },
        },
        ColourTags = new Dictionary<string, IReadOnlyList<PoolItem>>(StringComparer.Ordinal)
        {
            ["color_red"] = new[] { Item("(O)420") },
            ["color_purple"] = new[] { Item("(O)421") },
            ["color_yellow"] = new[] { Item("(O)190") },
            ["color_white"] = new[] { Item("(O)397") },
            ["color_blue"] = new[] { Item("(O)62") },
            ["color_green"] = new[] { Item("(O)266") },
        },
        WinterOnly = new[] { Item("(O)412"), Item("(O)416") },
    };

    [Fact]
    public void An_unnamed_bundle_rolls_from_its_majority_kind()
    {
        PoolRecipe r = BundlePoolRecipes.For("Some Gem Bundle", new[] { "(O)72", "(O)64", "(O)80" }, Pools(), null);
        Assert.Single(r.Parts);
        Assert.Equal("Gem", r.Parts[0].Label);
    }

    [Fact]
    public void Dye_has_one_part_per_colour()
    {
        PoolRecipe r = BundlePoolRecipes.For("Dye", new string[0], Pools(), null);
        Assert.Equal(6, r.Parts.Count);
        Assert.All(r.Parts, p => Assert.Equal(1, p.Count));
    }

    [Fact]
    public void An_unknown_kind_falls_back_to_the_bundles_own_items()
    {
        ItemPools pools = Pools();
        PoolRecipe r = BundlePoolRecipes.For("Odds And Ends", new[] { "(O)9001", "(O)9002" }, pools, null);
        Assert.Single(r.Parts);
        Assert.True(r.IsVanillaOnly);
        Assert.Equal(
            new[] { "(O)9001", "(O)9002" },
            r.Parts[0].Source(pools, null).Select(p => p.ItemId).ToArray());
    }

    [Fact]
    public void The_Other_kind_is_never_a_roll_source()
    {
        // A pool whose only Other-kind items are rings: an unnamed bundle must never see them.
        ItemPools pools = new()
        {
            ByKind = new Dictionary<ItemKind, IReadOnlyList<PoolItem>>
            {
                [ItemKind.Other] = new[] { Item("(O)517"), Item("(O)518"), Item("(O)519") },
            },
        };
        PoolRecipe r = BundlePoolRecipes.For("Odds And Ends", new[] { "(O)9001" }, pools, null);
        List<string> ids = r.Parts.SelectMany(p => p.Source(pools, null)).Select(p => p.ItemId).ToList();
        Assert.True(r.IsVanillaOnly);
        Assert.Equal(new[] { "(O)9001" }, ids);
    }

    [Fact]
    public void Helpers_and_Home_Cooks_roll_their_own_items_only()
    {
        ItemPools pools = Pools();
        foreach (string name in new[] { "Helper's", "Home Cook's" })
        {
            PoolRecipe r = BundlePoolRecipes.For(name, new[] { "(O)9001" }, pools, null);
            Assert.True(r.IsVanillaOnly);
            Assert.Equal(new[] { "(O)9001" }, r.Parts[0].Source(pools, null).Select(p => p.ItemId).ToArray());
        }
    }

    [Fact]
    public void A_named_recipe_is_never_vanilla_only()
        => Assert.False(BundlePoolRecipes.For("Treasure Hunter's", new[] { "(O)9001" }, Pools(), null).IsVanillaOnly);

    /// <summary>Crab Pot takes the crab-pot pool and every trap fish the pools know. A trap id
    /// NEITHER pool carries ("(O)716" here) is not a candidate: no pool knows it, so it failed
    /// vetting or was excluded, and Data/Fish calling it a trap row does not put it back.</summary>
    [Fact]
    public void Crab_Pot_takes_the_known_trap_fish_and_never_synthesizes_an_unknown_one()
    {
        ItemPools pools = new()
        {
            CrabPot = new[] { Item("(O)372") },
            Fish = new[] { Item("(O)715") },
            TrapFishIds = new HashSet<string>(StringComparer.Ordinal) { "(O)715", "(O)716" },
        };
        List<string> ids = BundlePoolRecipes.For("Crab Pot", Array.Empty<string>(), pools, null)
            .Parts[0].Source(pools, null).Select(p => p.ItemId).ToList();
        Assert.Equal(new[] { "(O)372", "(O)715" }, ids);
        Assert.DoesNotContain("(O)716", ids);
    }

    /// <summary>A trap id the CrabPot pool alone carries still counts: the lookup reads both pools,
    /// not just Fish.</summary>
    [Fact]
    public void Crab_Pot_finds_a_trap_id_the_crab_pot_pool_carries()
    {
        ItemPools pools = new()
        {
            CrabPot = new[] { Item("(O)372") },
            TrapFishIds = new HashSet<string>(StringComparer.Ordinal) { "(O)372" },
        };
        List<string> ids = BundlePoolRecipes.For("Crab Pot", Array.Empty<string>(), pools, null)
            .Parts[0].Source(pools, null).Select(p => p.ItemId).ToList();
        Assert.Equal(new[] { "(O)372" }, ids);
    }

    /// <summary>Chef's ingredient half offers the pantry staples (Sugar, Wheat Flour, Oil, Vinegar,
    /// Rice) as well as crops, forage and animal products: a kitchen asks for them, and no other
    /// pool carries them.</summary>
    [Fact]
    public void Chefs_ingredient_part_offers_the_pantry_staples()
    {
        string[] staples = { "(O)245", "(O)246", "(O)247", "(O)419", "(O)423" };
        ItemPools pools = new()
        {
            Cooking = new[] { Item("(O)194"), Item("(O)195") },
            Crops = new[] { Item("(O)24") },
            ByKind = new Dictionary<ItemKind, IReadOnlyList<PoolItem>>
            {
                // The staples are not a crop, forage or animal product, so in the real pools they
                // land in a kind no recipe rolls: only the Fixed list can reach them.
                [ItemKind.Other] = staples.Select(id => Item(id)).ToList(),
            },
        };
        PoolRecipe r = BundlePoolRecipes.For("Chef's", new[] { "(O)9001", "(O)9002" }, pools, null);
        List<string> ids = r.Parts[1].Source(pools, null).Select(p => p.ItemId).ToList();
        Assert.Equal("Ingredient", r.Parts[1].Label);
        foreach (string staple in staples)
            Assert.Contains(staple, ids);
    }

    /// <summary>An id the pools left out ON PURPOSE is never re-admitted through the bundle's own
    /// vanilla ids. Easy keeps Red Cabbage out until the player owns the upgrade, so the Dye
    /// recipe of a bundle that asked for it in vanilla must not offer it back.</summary>
    [Fact]
    public void An_excluded_vanilla_id_is_not_offered_back_by_a_recipe()
    {
        IReadOnlySet<string> easyExclusions =
            YearTwoCrops.ExcludedFor(_ => false, DifficultyStep.Easy);
        Assert.Contains(YearTwoCrops.RedCabbage, easyExclusions);

        ItemPools pools = ItemPoolBuilder.Build(
            crops: new[] { new RawCropEntry("266", new[] { Season.Fall }) },
            objects: new Dictionary<string, RawObjectEntry>
            {
                ["266"] = new("Basic", -75, 260, false, new[] { "color_red" }),
            },
            forageSpawns: Array.Empty<RawSpawnEntry>(),
            fishSpawns: Array.Empty<RawSpawnEntry>(),
            trapFishIds: new HashSet<string>(StringComparer.Ordinal),
            monsterDrops: Array.Empty<RawMonsterDropEntry>(),
            fruitTrees: Array.Empty<RawFruitTreeEntry>(),
            geodeDrops: Array.Empty<RawGeodeDropEntry>(),
            tuning: new BundleGenerationTuning(),
            extraExcludedIds: easyExclusions);

        Assert.Contains(YearTwoCrops.RedCabbage, pools.ExcludedIds);
        List<string> ids = BundlePoolRecipes
            .For("Dye", new[] { YearTwoCrops.RedCabbage }, pools, null)
            .Parts.SelectMany(p => p.Source(pools, null)).Select(p => p.ItemId).ToList();
        Assert.DoesNotContain(YearTwoCrops.RedCabbage, ids);
    }

    [Fact]
    public void Exotic_Foraging_takes_forage_and_tapper_goods()
    {
        ItemPools pools = new()
        {
            Forage = new[] { Item("(O)404") },
            TapperGoods = new[] { Item("(O)724") },
        };
        List<string> ids = BundlePoolRecipes.For("Exotic Foraging", Array.Empty<string>(), pools, null)
            .Parts[0].Source(pools, null).Select(p => p.ItemId).ToList();
        Assert.Equal(new[] { "(O)404", "(O)724" }, ids);
    }

    [Fact]
    public void Rare_Crops_takes_the_harder_crops_and_the_fruit_tree_fruit()
    {
        ItemPools pools = new()
        {
            Crops = new[] { Item("(O)24"), Item("(O)276") },
            ByKind = new Dictionary<ItemKind, IReadOnlyList<PoolItem>>
            {
                [ItemKind.Other] = new[] { Item("(O)613") },
            },
            FruitTreeFruitIds = new HashSet<string>(StringComparer.Ordinal) { "(O)613" },
        };
        var model = new ItemAvailabilityModel(new Dictionary<string, ItemAvailability>
        {
            ["(O)24"] = new(Season.Spring, 1, "test", EarliestWeek: 1, HardWeek: 1),
            ["(O)276"] = new(Season.Spring, 5, "test", EarliestWeek: 1, HardWeek: 1),
        });
        List<string> ids = BundlePoolRecipes.For("Rare Crops", Array.Empty<string>(), pools, model)
            .Parts[0].Source(pools, model).Select(p => p.ItemId).ToList();
        Assert.Contains("(O)276", ids);    // effort 5
        Assert.DoesNotContain("(O)24", ids);   // effort 1
        Assert.Contains("(O)613", ids);    // fruit-tree fruit
    }

    [Fact]
    public void Sticky_takes_resources_and_tapper_goods()
    {
        ItemPools pools = new()
        {
            TapperGoods = new[] { Item("(O)92") },
            ByKind = new Dictionary<ItemKind, IReadOnlyList<PoolItem>>
            {
                [ItemKind.Resource] = new[] { Item("(O)388") },
            },
        };
        List<string> ids = BundlePoolRecipes.For("Sticky", Array.Empty<string>(), pools, null)
            .Parts[0].Source(pools, null).Select(p => p.ItemId).ToList();
        Assert.Equal(new[] { "(O)388", "(O)92" }, ids);
    }

    [Fact]
    public void Book_takes_only_the_BookWeeks_filtered_pool()
    {
        ItemPools pools = new()
        {
            Books = new[] { Item("(O)Book_Trash") },
            ByKind = new Dictionary<ItemKind, IReadOnlyList<PoolItem>>
            {
                [ItemKind.Book] = new[] { Item("(O)Book_Trash"), Item("(O)Book_PurpleBook") },
            },
        };
        List<string> ids = BundlePoolRecipes.For("Book", Array.Empty<string>(), pools, null)
            .Parts[0].Source(pools, null).Select(p => p.ItemId).ToList();
        Assert.Equal(new[] { "(O)Book_Trash" }, ids);
    }

    [Fact]
    public void Every_part_keeps_the_bundles_own_vanilla_ids()
    {
        ItemPools pools = Pools();
        PoolRecipe r = BundlePoolRecipes.For("Treasure Hunter's", new[] { "(O)9001" }, pools, null);
        IReadOnlyList<PoolItem> candidates = r.Parts[0].Source(pools, null);
        Assert.Contains(candidates, p => p.ItemId == "(O)9001");
        Assert.Contains(candidates, p => p.ItemId == "(O)72");
    }

    [Fact]
    public void The_Missing_takes_only_extreme_effort_items()
    {
        ItemPools pools = Pools();
        var model = new ItemAvailabilityModel(new Dictionary<string, ItemAvailability>
        {
            ["(O)72"] = new(Season.Spring, 9, "test", EarliestWeek: 1, HardWeek: 1),
            ["(O)62"] = new(Season.Spring, 3, "test", EarliestWeek: 1, HardWeek: 1),
        });
        PoolRecipe r = BundlePoolRecipes.For("The Missing", Array.Empty<string>(), pools, model);
        List<string> ids = r.Parts[0].Source(pools, model).Select(p => p.ItemId).ToList();
        Assert.Contains("(O)72", ids);
        Assert.DoesNotContain("(O)62", ids);
    }

    [Fact]
    public void Named_recipes_are_deterministic_in_part_order()
    {
        ItemPools pools = Pools();
        PoolRecipe a = BundlePoolRecipes.For("Dye", Array.Empty<string>(), pools, null);
        PoolRecipe b = BundlePoolRecipes.For("Dye", Array.Empty<string>(), pools, null);
        Assert.Equal(a.Parts.Select(p => p.Label), b.Parts.Select(p => p.Label));
    }
}
