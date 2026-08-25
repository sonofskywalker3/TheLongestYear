using System;
using System.Linq;
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

    [Fact]
    public void Trim_removes_hardest_first_ties_by_ordinal_id_and_keeps_order()
    {
        var pool = new[]
        {
            Item("(O)10", 700), Item("(O)20", 10), Item("(O)30", 250), Item("(O)05", 700), Item("(O)40", 60),
        };
        var trimmed = ItemHardness.Trim(pool, count: 2, minKeep: 1, PoolDomain.Fish, T);
        // Two VeryRare (score 4): "(O)05" and "(O)10" removed (highest score, then higher ordinal id first).
        Assert.Equal(new[] { "(O)20", "(O)30", "(O)40" }, trimmed.Select(p => p.ItemId));
    }

    [Fact]
    public void Trim_never_drops_below_minKeep()
    {
        var pool = new[] { Item("(O)1", 700), Item("(O)2", 700), Item("(O)3", 10) };
        var trimmed = ItemHardness.Trim(pool, count: 5, minKeep: 2, PoolDomain.Fish, T);
        Assert.Equal(2, trimmed.Count);
        Assert.Contains(trimmed, p => p.ItemId == "(O)3");
    }

    [Fact]
    public void Trim_zero_returns_same_instance()
    {
        var pool = new[] { Item("(O)1", 700) };
        Assert.Same(pool, ItemHardness.Trim(pool, 0, 1, PoolDomain.Fish, T));
    }
}
