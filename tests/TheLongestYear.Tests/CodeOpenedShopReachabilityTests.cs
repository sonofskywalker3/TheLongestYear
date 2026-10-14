using System;
using System.Collections.Generic;
using TheLongestYear.Core;
using TheLongestYear.Core.Availability;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>The Island Trader, the resort bar and the Volcano dwarf are opened from game code, so
/// Data/Shops gives them no owner and no tile. They used to count as unplaced shops, an unknown
/// route that kept everything they sell allowed. They now stand where ShopWeeks places them.</summary>
public class CodeOpenedShopReachabilityTests
{
    /// <summary>What ReachabilityGraph returns for the vanilla world: every forbidden (island)
    /// map, and Caldera behind it. The Desert is reached by its warps.</summary>
    private static readonly IReadOnlySet<string> Unreachable =
        new HashSet<string>(StringComparer.Ordinal) { "IslandNorth", "IslandSouth", "Caldera" };

    private static SourceReachability Build(params RawShopListing[] listings) => new(
        Unreachable, listings, Array.Empty<RawShopPlacement>(),
        Array.Empty<RawCropEntry>(), Array.Empty<RawRecipeEntry>(), new HashSet<string>(StringComparer.Ordinal));

    [Theory]
    [InlineData("IslandTrade")]
    [InlineData("ResortBar")]
    [InlineData("VolcanoShop")]
    public void ItemSoldOnlyByAnIslandCodeShop_IsUnreachable(string shop)
        => Assert.True(Build(new RawShopListing("(O)Mod_TropicalThing", shop)).IsUnreachable("(O)Mod_TropicalThing"));

    [Fact]
    public void ItemSoldByTheDesertTrader_StaysReachable()
        => Assert.False(Build(new RawShopListing("(O)Mod_DesertThing", "DesertTrade")).IsUnreachable("(O)Mod_DesertThing"));

    [Fact]
    public void ItemAlsoSoldOnTheMainland_StaysReachable()
    {
        var rule = new SourceReachability(
            Unreachable,
            new[] { new RawShopListing("(O)Mod_TropicalThing", "IslandTrade"), new RawShopListing("(O)Mod_TropicalThing", "Pierre") },
            new[] { new RawShopPlacement("Pierre", "SeedShop") },
            Array.Empty<RawCropEntry>(), Array.Empty<RawRecipeEntry>(), new HashSet<string>(StringComparer.Ordinal));
        Assert.False(rule.IsUnreachable("(O)Mod_TropicalThing"));
    }

    [Fact]
    public void ItemSoldByAnUnplacedShop_StaysAllowed()
        => Assert.False(Build(new RawShopListing("(O)Mod_CartThing", "Traveler")).IsUnreachable("(O)Mod_CartThing"));
}
