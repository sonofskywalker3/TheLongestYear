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
    public void An_unknown_kind_falls_back_to_Other()
    {
        PoolRecipe r = BundlePoolRecipes.For("Odds And Ends", new[] { "(O)9001", "(O)9002" }, Pools(), null);
        Assert.Single(r.Parts);
        Assert.Equal("Other", r.Parts[0].Label);
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
