using System;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class ItemHardnessTests
{
    private static readonly RarityThresholds T = new();   // 50 / 200 / 600

    private static PoolItem Item(string id, int price, Season[]? seasons = null)
        => new(id, price, 3, seasons ?? Array.Empty<Season>(), Array.Empty<string>());

    [Fact]
    public void Score_rarity_tier_is_the_base()
    {
        Assert.Equal(1, ItemHardness.Score(Item("(O)1", 10), PoolDomain.Fish, T));
        Assert.Equal(2, ItemHardness.Score(Item("(O)1", 60), PoolDomain.Fish, T));
        Assert.Equal(3, ItemHardness.Score(Item("(O)1", 250), PoolDomain.Fish, T));
        Assert.Equal(4, ItemHardness.Score(Item("(O)1", 700), PoolDomain.Fish, T));
    }

    [Fact]
    public void Score_adds_two_for_station_domains_and_one_for_late_spawn()
    {
        Assert.Equal(3, ItemHardness.Score(Item("(O)1", 10), PoolDomain.ArtisanGoods, T));
        Assert.Equal(2, ItemHardness.Score(Item("(O)1", 10, new[] { Season.Fall }), PoolDomain.Fish, T));
        Assert.Equal(2, ItemHardness.Score(Item("(O)1", 10, new[] { Season.Winter, Season.Fall }), PoolDomain.Fish, T));
        Assert.Equal(1, ItemHardness.Score(Item("(O)1", 10, new[] { Season.Summer, Season.Fall }), PoolDomain.Fish, T));
    }
}
