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

public class YearTwoCartPoolTests
{
    private static CartCandidate C(string id, YearTwoReason reason, int price = 100, string? objectType = "Basic", int category = 0)
        => new(id, id.Substring(0, id.IndexOf(')') + 1), price, objectType, category, reason);

    [Fact]
    public void Year_two_reasons_are_offered_for_every_type()
    {
        Assert.True(YearTwoCartPool.Offers(C("(O)830", YearTwoReason.IslandOnly)));
        Assert.True(YearTwoCartPool.Offers(C("(BC)254", YearTwoReason.IslandOnly, objectType: null)));
        Assert.True(YearTwoCartPool.Offers(C("(O)476", YearTwoReason.YearTwoOnly)));
    }

    [Fact]
    public void No_route_counts_only_for_objects()
    {
        Assert.True(YearTwoCartPool.Offers(C("(O)Mod.LateGame", YearTwoReason.NoRoute)));
        Assert.False(YearTwoCartPool.Offers(C("(W)Mod.Sword", YearTwoReason.NoRoute, objectType: null)));
        Assert.False(YearTwoCartPool.Offers(C("(H)Mod.Hat", YearTwoReason.NoRoute, objectType: null)));
    }

    [Fact]
    public void Year_one_unpriced_tools_litter_and_quest_items_are_never_offered()
    {
        Assert.False(YearTwoCartPool.Offers(C("(O)24", YearTwoReason.YearOne)));
        Assert.False(YearTwoCartPool.Offers(C("(O)Mod.Free", YearTwoReason.NoRoute, price: 0)));
        Assert.False(YearTwoCartPool.Offers(C("(T)Mod.Tool", YearTwoReason.IslandOnly, objectType: null)));
        Assert.False(YearTwoCartPool.Offers(C("(O)Mod.Rock", YearTwoReason.NoRoute, objectType: "Litter", category: -999)));
        Assert.False(YearTwoCartPool.Offers(C("(O)Mod.Letter", YearTwoReason.NoRoute, objectType: "Quest")));
    }

    [Fact]
    public void Vanilla_rulings_override_the_model()
    {
        Assert.True(YearTwoCartPool.Offers(C("(W)57", YearTwoReason.NoRoute, objectType: null)));      // Dragontooth Cutlass
        Assert.False(YearTwoCartPool.Offers(C("(O)460", YearTwoReason.NoRoute)));                       // Mermaid's Pendant
        Assert.False(YearTwoCartPool.Offers(C("(O)73", YearTwoReason.NoRoute, price: 500)));            // Golden Walnut
    }

    [Fact]
    public void Pool_is_sorted_and_distinct()
        => Assert.Equal(new[] { "(O)830", "(O)831" }, YearTwoCartPool.Select(new[]
        {
            C("(O)831", YearTwoReason.IslandOnly), C("(O)830", YearTwoReason.IslandOnly), C("(O)830", YearTwoReason.IslandOnly),
        }));
}
