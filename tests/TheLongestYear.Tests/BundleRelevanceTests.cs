using System.Collections.Generic;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class BundleRelevanceTests
{
    private static HashSet<string> Build()
    {
        // Bundle wants Parsnip (O)24 and Wood (O)388.
        var bundleItems = new[] { "(O)24", "(O)388" };
        // Parsnip Seeds (O)472 grow into Parsnip (O)24; Cauliflower Seeds (O)473 grow Cauliflower (O)190 (not in a bundle).
        var seedToCrop = new Dictionary<string, string>
        {
            ["(O)472"] = "(O)24",
            ["(O)473"] = "(O)190",
        };
        // Field Snack (O)403) is crafted from Acorn/Maple/Pine — pretend it's a bundle item via a recipe:
        // Recipe producing Parsnip-bundle item (O)24 from Fiber (O)771 (contrived); recipe producing a
        // non-bundle item (O)999 from Stone (O)390.
        var productToIngredients = new Dictionary<string, IReadOnlyCollection<string>>
        {
            ["(O)24"] = new[] { "(O)771" },
            ["(O)999"] = new[] { "(O)390" },
        };
        return BundleRelevance.BuildRelevantItemIds(bundleItems, seedToCrop, productToIngredients);
    }

    [Fact]
    public void Direct_bundle_items_are_relevant()
    {
        var r = Build();
        Assert.Contains("(O)24", r);
        Assert.Contains("(O)388", r);
    }

    [Fact]
    public void Seed_whose_crop_is_a_bundle_item_is_relevant()
    {
        Assert.Contains("(O)472", Build()); // Parsnip Seeds -> Parsnip (bundle)
    }

    [Fact]
    public void Seed_whose_crop_is_not_a_bundle_item_is_not_relevant()
    {
        Assert.DoesNotContain("(O)473", Build()); // Cauliflower Seeds -> Cauliflower (not a bundle item)
    }

    [Fact]
    public void Ingredient_of_a_recipe_producing_a_bundle_item_is_relevant()
    {
        Assert.Contains("(O)771", Build()); // crafts into (O)24 which is a bundle item
    }

    [Fact]
    public void Ingredient_of_a_recipe_producing_a_non_bundle_item_is_not_relevant()
    {
        Assert.DoesNotContain("(O)390", Build()); // crafts into (O)999 which is not a bundle item
    }

    [Fact]
    public void Unrelated_item_is_not_relevant()
    {
        Assert.DoesNotContain("(O)74", Build()); // Prismatic Shard, unrelated
    }
}
