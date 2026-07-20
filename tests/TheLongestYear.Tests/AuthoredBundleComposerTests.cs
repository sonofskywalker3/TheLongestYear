using System;
using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class AuthoredBundleComposerTests
{
    private static PoolItem Item(string id, Season[]? seasons = null)
        => new(id, 50, 3, seasons ?? Array.Empty<Season>(), Array.Empty<string>());

    private static readonly BundleGenerationTuning Tuning = new();

    private static AuthoredBundleDef TrophyDef =>
        AuthoredBundleCatalog.All.Single(d => d.Name == "Gil's Trophies");

    [Fact]
    public void Trophies_Enabled_DrawsFromAllEleven_ShownAndRequiredFromTuning()
    {
        var spec = AuthoredBundleComposer.Compose(TrophyDef, 40, new ItemPools(), Tuning,
            nonObjectDonationsEnabled: true, new Random(3));
        Assert.NotNull(spec);
        Assert.Equal(Tuning.TrophyShownCount, spec!.Slots.Count);
        Assert.Equal(Tuning.TrophyRequiredCount, spec.NumberOfSlots);
        Assert.All(spec.Slots, s => Assert.Contains(s.ItemId, AuthoredBundleCatalog.GilTrophies));
    }

    [Fact]
    public void Trophies_Disabled_RingsOnly()
    {
        var spec = AuthoredBundleComposer.Compose(TrophyDef, 40, new ItemPools(), Tuning,
            nonObjectDonationsEnabled: false, new Random(3));
        Assert.All(spec!.Slots, s => Assert.Contains(s.ItemId, AuthoredBundleCatalog.GilTrophyRingsOnly));
    }

    [Fact]
    public void PoolSource_InsufficientPool_ReturnsNull()
    {
        var def = AuthoredBundleCatalog.All.Single(d => d.Name == "Artifact"); // needs 6 shown
        var pools = new ItemPools { Artifacts = new[] { Item("(O)100"), Item("(O)101") } };
        Assert.Null(AuthoredBundleComposer.Compose(def, 41, pools, Tuning, true, new Random(1)));
    }

    [Fact]
    public void Compose_Deterministic_NoDuplicates_MetadataFromDef()
    {
        var def = AuthoredBundleCatalog.All.Single(d => d.Name == "Book");
        var pools = new ItemPools
        {
            Books = Enumerable.Range(0, 9).Select(i => Item($"(O)B{i}")).ToList(),
        };
        var a = AuthoredBundleComposer.Compose(def, 42, pools, Tuning, true, new Random(9));
        var b = AuthoredBundleComposer.Compose(def, 42, pools, Tuning, true, new Random(9));
        Assert.Equal(a!.Slots, b!.Slots);
        Assert.Equal(def.SlotCount, a.Slots.Select(s => s.ItemId).Distinct().Count());
        Assert.Equal(def.Name, a.Name);
        Assert.Equal(def.RewardField, a.RewardField);
        Assert.Equal(42, a.Index);
        Assert.Equal(-1, a.PickCount);
    }

    [Fact]
    public void SeasonSpread_SamplerSpansThreeSeasons()
    {
        var def = AuthoredBundleCatalog.All.Single(d => d.Name == "Four Seasons Sampler");
        var pools = new ItemPools
        {
            Forage = new[]
            {
                Item("(O)16", new[] { Season.Spring }), Item("(O)396", new[] { Season.Summer }),
                Item("(O)404", new[] { Season.Fall }), Item("(O)412", new[] { Season.Winter }),
                Item("(O)398", new[] { Season.Summer }), Item("(O)406", new[] { Season.Fall }),
                Item("(O)414", new[] { Season.Winter }), Item("(O)18", new[] { Season.Spring }),
            },
        };
        var spec = AuthoredBundleComposer.Compose(def, 43, pools, Tuning, true, new Random(5));
        Assert.NotNull(spec);
        var seasons = spec!.Slots
            .SelectMany(s => pools.Forage.First(p => p.ItemId == s.ItemId).Seasons)
            .Distinct().Count();
        Assert.True(seasons >= 3, $"only {seasons} seasons");
    }
}
