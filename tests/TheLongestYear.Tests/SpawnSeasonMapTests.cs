using System;
using System.Collections.Generic;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>Nexus 1122423 (2026-08-24): a Spring weekly theme asked for Pike, which only
/// spawns in Summer/Winter. The CcItem catalog's SeasonResolver treated every fish as
/// year-round; this map feeds it the engine pools' real spawn seasons instead.</summary>
public class SpawnSeasonMapTests
{
    private static PoolItem Item(string id, params Season[] seasons)
        => new(id, 50, 3, seasons, Array.Empty<string>());

    [Fact]
    public void FromPools_SeasonalFish_GetTheirSpawnSeasons()
    {
        var pools = new ItemPools
        {
            Fish = new[] { Item("(O)144", Season.Summer, Season.Winter) }, // Pike
        };
        IReadOnlyDictionary<string, IReadOnlySet<Season>> map = SpawnSeasonMap.FromPools(pools);
        Assert.Equal(new HashSet<Season> { Season.Summer, Season.Winter }, map["(O)144"]);
    }

    [Fact]
    public void FromPools_AnySeasonFish_MapToAllFourSeasons()
    {
        var pools = new ItemPools { Fish = new[] { Item("(O)145") } }; // Carp, no season limits
        var map = SpawnSeasonMap.FromPools(pools);
        Assert.Equal(4, map["(O)145"].Count);
    }

    [Fact]
    public void FromPools_CrabPotAndFishPools_BothIncluded_DuplicateIdsUnionSeasons()
    {
        var pools = new ItemPools
        {
            Fish = new[] { Item("(O)144", Season.Summer) },
            CrabPot = new[] { Item("(O)715", Season.Spring), Item("(O)144", Season.Winter) },
        };
        var map = SpawnSeasonMap.FromPools(pools);
        Assert.Equal(new HashSet<Season> { Season.Spring }, map["(O)715"]);
        Assert.Equal(new HashSet<Season> { Season.Summer, Season.Winter }, map["(O)144"]);
    }

    [Fact]
    public void FromPools_EmptyPools_EmptyMap()
    {
        Assert.Empty(SpawnSeasonMap.FromPools(new ItemPools()));
    }
}
