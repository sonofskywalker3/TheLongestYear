using System.Linq;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class AuthoredBundlesTests
{
    [Fact]
    public void Catalog_HasElevenDefs_UniqueSlashFreeNames()
    {
        Assert.Equal(11, AuthoredBundleCatalog.All.Count);
        Assert.Equal(11, AuthoredBundleCatalog.All.Select(d => d.Name).Distinct().Count());
        Assert.All(AuthoredBundleCatalog.All, d => Assert.DoesNotContain('/', d.Name));
    }

    [Fact]
    public void EveryDef_RequiredAtMostShown_RoomIsKnown_RewardNonEmpty()
    {
        var rooms = new[] { "Pantry", "Crafts Room", "Fish Tank", "Boiler Room", "Bulletin Board" };
        Assert.All(AuthoredBundleCatalog.All, d =>
        {
            Assert.True(d.NumberOfSlots <= d.SlotCount, d.Name);
            Assert.Contains(d.Room, rooms);
            Assert.False(string.IsNullOrWhiteSpace(d.RewardField));
            Assert.DoesNotContain('/', d.RewardField);
        });
    }

    [Fact]
    public void GilTrophies_SevenYearOneFeasibleIds_RingsOnlySubsetIsObjects()
    {
        Assert.Equal(7, AuthoredBundleCatalog.GilTrophies.Count);
        Assert.All(AuthoredBundleCatalog.GilTrophies, id => Assert.StartsWith("(", id));
        Assert.Equal(4, AuthoredBundleCatalog.GilTrophyRingsOnly.Count);
        // Late-game trophies are gone (user ruling 2026-08-21).
        Assert.DoesNotContain("(O)520", AuthoredBundleCatalog.GilTrophies);
        Assert.DoesNotContain("(O)811", AuthoredBundleCatalog.GilTrophies);
        Assert.DoesNotContain("(H)50", AuthoredBundleCatalog.GilTrophies);
        Assert.DoesNotContain("(H)60", AuthoredBundleCatalog.GilTrophies);
        Assert.All(AuthoredBundleCatalog.GilTrophyRingsOnly, id => Assert.StartsWith("(O)", id));
        Assert.Contains("(W)13", AuthoredBundleCatalog.GilTrophies);
        Assert.Contains("(H)8", AuthoredBundleCatalog.GilTrophies);
    }

    [Fact]
    public void FixedListDefs_CarryTheirIds_OthersEmpty()
    {
        var trophies = AuthoredBundleCatalog.All.Single(d => d.Name == "Gil's Trophies");
        Assert.Equal(AuthoredBundleCatalog.GilTrophies, trophies.FixedItemIds);
        var artifact = AuthoredBundleCatalog.All.Single(d => d.Name == "Artifact");
        Assert.Empty(artifact.FixedItemIds);
    }
}
