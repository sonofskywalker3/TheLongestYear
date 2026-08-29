using System;
using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class PoolDomainClassifierTests
{
    private static PoolItem Item(string id) => new(id, 50, 3, Array.Empty<Season>(), Array.Empty<string>());

    private static ItemPools Pools() => new()
    {
        Fish = new[] { Item("(O)128"), Item("(O)129"), Item("(O)130") },
        MonsterDrops = new[] { Item("(O)766"), Item("(O)768"), Item("(O)769") },
        Crops = new[] { Item("(O)24"), Item("(O)188"), Item("(O)190") },
    };

    private static BundleSpec Spec(string name, params string[] ids) => new(
        "Pantry", 0, name, name, "O 495 30", 0, Math.Max(1, ids.Length - 1),
        ids.Select(id => new BundleSlotSpec(id, 1, 0)).ToList());

    [Fact]
    public void NameFastPaths_SeasonalCropsForagingQuality()
    {
        Assert.Equal(new DomainMatch(PoolDomain.SeasonalCrops, Season.Spring),
            PoolDomainClassifier.Classify(Spec("Spring Crops", "24"), Pools()));
        Assert.Equal(new DomainMatch(PoolDomain.SeasonalForage, Season.Winter),
            PoolDomainClassifier.Classify(Spec("Winter Foraging", "412"), Pools()));
        Assert.Equal(new DomainMatch(PoolDomain.QualityCrops, null),
            PoolDomainClassifier.Classify(Spec("Quality Crops", "24"), Pools()));
    }

    [Fact]
    public void MoneyBundlesAreNeverRolled()
        => Assert.Equal(PoolDomain.None,
            PoolDomainClassifier.Classify(Spec("2,500g", "-1"), Pools()).Domain);

    [Fact]
    public void CategorySlotBundlesRollTheirRecipe()
        => Assert.Equal(PoolDomain.Recipe,
            PoolDomainClassifier.Classify(Spec("Animal", "-5", "186"), Pools()).Domain);

    [Fact]
    public void AVanillaListBundleIsClassifiedAsARecipe()
    {
        BundleSpec dye = Spec("Dye", "(O)420", "(O)397", "(O)421", "(O)444", "(O)62", "(O)266");
        Assert.Equal(PoolDomain.Recipe, PoolDomainClassifier.Classify(dye, Pools()).Domain);
    }

    [Fact]
    public void MembershipMajority_TwoThirds_ClaimsDomain()
    {
        // 2 of 3 fish (66.7% >= 2/3) -> Fish.
        var match = PoolDomainClassifier.Classify(Spec("River Fish", "128", "129", "999"), Pools());
        Assert.Equal(new DomainMatch(PoolDomain.Fish, null), match);
    }

    [Fact]
    public void BelowMajority_FallsThroughToTheRecipeDomain()
    {
        Assert.Equal(PoolDomain.Recipe,
            PoolDomainClassifier.Classify(Spec("Odd", "128", "998", "999"), Pools()).Domain);
    }

    [Fact]
    public void CropsMajority_MapsToSeasonalCropsWithNullSeason()
    {
        var match = PoolDomainClassifier.Classify(Spec("Rare Crops", "24", "188"), Pools());
        Assert.Equal(new DomainMatch(PoolDomain.SeasonalCrops, null), match);
    }

    [Fact]
    public void QualifiedAndBareIdsBothMatch()
    {
        var match = PoolDomainClassifier.Classify(Spec("Monster Hunter", "(O)766", "768"), Pools());
        Assert.Equal(PoolDomain.MonsterDrops, match.Domain);
    }
}
