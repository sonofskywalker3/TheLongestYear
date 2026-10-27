using System.Collections.Generic;
using TheLongestYear.Core;
using TheLongestYear.Core.Obtainability;
using Xunit;

namespace TheLongestYear.Tests;

public class YearTwoItemsTests
{
    private static ObtainSource Source(ObtainConditions conditions)
        => new(SourceKind.Shop, DayTable.Always, Reliability.Dependable, conditions, "test");

    private static readonly ObtainabilityModel Model = new(new Dictionary<string, IReadOnlyList<ObtainSource>>
    {
        ["(O)24"] = new[] { Source(ObtainConditions.None) },
        ["(O)476"] = new[] { Source(ObtainConditions.None with { YearTwo = true }) },
        ["(O)830"] = new[] { Source(ObtainConditions.None with { GingerIsland = true }) },
        ["(O)289"] = new[] { Source(ObtainConditions.None with { OwnedOnly = true }) },
        ["(O)91"] = new[] { Source(ObtainConditions.None with { GingerIsland = true }), Source(ObtainConditions.None) },
    });

    [Theory]
    [InlineData("(O)24", YearTwoReason.YearOne)]
    [InlineData("(O)91", YearTwoReason.YearOne)]          // one year-1 route is enough
    [InlineData("(O)476", YearTwoReason.YearTwoOnly)]
    [InlineData("(O)830", YearTwoReason.IslandOnly)]
    [InlineData("(O)289", YearTwoReason.OwnedOnly)]
    [InlineData("(O)Mod.LateGame", YearTwoReason.NoRoute)]
    public void Sorts_by_the_earliest_route(string id, YearTwoReason expected)
        => Assert.Equal(expected, YearTwoItems.Classify(Model, id));
}

public class YearTwoSortInputsTests
{
    [Fact]
    public void Cart_random_stock_is_dropped_but_its_fixed_lines_stay()
    {
        var inputs = new ObtainabilityInputs
        {
            Shops = new[]
            {
                new ShopRow("Traveler", "RANDOM_ITEMS (O) 2 789 @requirePrice @isRandomSale", null, false),
                new ShopRow("Traveler", "(O)347", "SEASON spring summer", false),
                new ShopRow("SeedShop", "(O)476", "YEAR 2", false),
            },
        };
        var shops = YearTwoItems.WithoutCartRandomStock(inputs).Shops;
        Assert.Equal(new[] { "(O)347", "(O)476" }, System.Linq.Enumerable.Select(shops, r => r.ItemId));
    }
}

public class YearTwoCartPoolTests
{
    private static CartCandidate C(string id, YearTwoReason reason, bool cartSellsKind = true)
        => new(id, id.Substring(0, id.IndexOf(')') + 1), cartSellsKind, reason);

    [Fact]
    public void Year_two_items_of_a_kind_the_cart_sells_are_offered()
    {
        Assert.True(YearTwoCartPool.Offers(C("(O)830", YearTwoReason.IslandOnly)));
        Assert.True(YearTwoCartPool.Offers(C("(O)476", YearTwoReason.YearTwoOnly)));
        Assert.True(YearTwoCartPool.Offers(C("(O)Mod.LateGame", YearTwoReason.NoRoute)));
    }

    [Fact]
    public void Kinds_the_cart_never_sells_are_never_offered()
    {
        // A weapon, a ring, a fossil: year 2, but the cart sells none of them in year 1 either.
        Assert.False(YearTwoCartPool.Offers(C("(W)57", YearTwoReason.NoRoute, cartSellsKind: false)));
        Assert.False(YearTwoCartPool.Offers(C("(O)860", YearTwoReason.NoRoute, cartSellsKind: false)));
        Assert.False(YearTwoCartPool.Offers(C("(O)820", YearTwoReason.IslandOnly, cartSellsKind: false)));
    }

    [Fact]
    public void Year_one_and_ruled_items_are_not_offered()
    {
        Assert.False(YearTwoCartPool.Offers(C("(O)24", YearTwoReason.YearOne)));
        Assert.False(YearTwoCartPool.Offers(C("(O)460", YearTwoReason.NoRoute)));   // Mermaid's Pendant, ruled year 1
        Assert.False(YearTwoCartPool.Offers(C("(O)73", YearTwoReason.NoRoute)));    // Golden Walnut, never sold
        Assert.True(YearTwoCartPool.Offers(C("(O)909", YearTwoReason.NoRoute)));    // Radioactive Ore, ruled year 2
    }

    [Fact]
    public void Pool_is_sorted_and_distinct()
        => Assert.Equal(new[] { "(O)830", "(O)831" }, YearTwoCartPool.Select(new[]
        {
            C("(O)831", YearTwoReason.IslandOnly), C("(O)830", YearTwoReason.IslandOnly), C("(O)830", YearTwoReason.IslandOnly),
        }));
}
