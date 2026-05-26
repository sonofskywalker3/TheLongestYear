using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class CcItemCatalogTests
{
    [Fact]
    public void Catalog_is_non_empty_and_ids_are_unique()
    {
        var items = CcItemCatalog.Items;
        Assert.NotEmpty(items);
        Assert.Equal(items.Count, items.Select(i => i.Id).Distinct().Count());
    }

    [Theory]
    [InlineData(Theme.Foraging)]
    [InlineData(Theme.Farming)]
    [InlineData(Theme.Fishing)]
    [InlineData(Theme.Mining)]
    [InlineData(Theme.Mixed)]
    public void Every_theme_is_represented(Theme theme)
        => Assert.Contains(CcItemCatalog.Items, i => i.Theme == theme);

    [Fact]
    public void Every_season_has_at_least_one_obtainable_item()
    {
        foreach (Season s in System.Enum.GetValues(typeof(Season)))
            Assert.Contains(CcItemCatalog.Items, i => i.IsObtainableIn(s));
    }

    [Fact]
    public void RarityOf_returns_the_items_rarity_and_a_default_for_unknown()
    {
        var sample = CcItemCatalog.Items.First();
        Assert.Equal(sample.Rarity, CcItemCatalog.RarityOf(sample.Id));
        Assert.Equal(Rarity.Common, CcItemCatalog.RarityOf("not-a-real-id"));
    }
}
