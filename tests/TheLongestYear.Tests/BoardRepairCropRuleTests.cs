using System;
using System.Collections.Generic;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>The load repair's crop-bundle rule: an open ask in Quality Crops or a season's Crops
/// bundle that is not in the crop pool is swapped out (player report: Fiber in Quality Crops),
/// and a Quality Crops replacement asks for gold when it can.</summary>
public class BoardRepairCropRuleTests
{
    private static readonly IReadOnlySet<string> CropPool =
        new HashSet<string>(StringComparer.Ordinal) { "(O)24", "(O)254", "(O)433" };

    private static readonly DomainMatch QualityCrops = new(PoolDomain.QualityCrops, null);
    private static readonly DomainMatch SpringCrops = new(PoolDomain.SeasonalCrops, Season.Spring);

    [Fact]
    public void QualityCrops_Fiber_MustBeReplaced()
        => Assert.True(BoardRepairRules.MustReplaceInCropBundle(QualityCrops, "(O)771", CropPool));

    [Fact]
    public void QualityCrops_BareId_IsNormalized()
        => Assert.True(BoardRepairRules.MustReplaceInCropBundle(QualityCrops, "771", CropPool));

    [Fact]
    public void SeasonalCrops_WildSeedPlaceholder_MustBeReplaced()
        => Assert.True(BoardRepairRules.MustReplaceInCropBundle(SpringCrops, "(O)16", CropPool));

    [Theory]
    [InlineData("(O)24")]
    [InlineData("(O)433")]   // Coffee Bean stays a crop
    public void CropBundle_CropPoolItem_Kept(string id)
    {
        Assert.False(BoardRepairRules.MustReplaceInCropBundle(QualityCrops, id, CropPool));
        Assert.False(BoardRepairRules.MustReplaceInCropBundle(SpringCrops, id, CropPool));
    }

    [Theory]
    [InlineData(PoolDomain.Recipe)]          // Construction, Chef's, ...: Fiber is a fair ask there
    [InlineData(PoolDomain.SeasonalForage)]
    [InlineData(PoolDomain.Fish)]
    public void OtherDomains_Fiber_Kept(PoolDomain domain)
        => Assert.False(BoardRepairRules.MustReplaceInCropBundle(new DomainMatch(domain, null), "(O)771", CropPool));

    [Fact]
    public void CropMajorityBundle_WithoutSeason_Kept()
        => Assert.False(BoardRepairRules.MustReplaceInCropBundle(
            new DomainMatch(PoolDomain.SeasonalCrops, null), "(O)771", CropPool));

    [Fact]
    public void NoCropPool_NothingReplaced()
        => Assert.False(BoardRepairRules.MustReplaceInCropBundle(QualityCrops, "(O)771", null));

    [Fact]
    public void Fiber_NotOnTheBuiltInBanList()
        => Assert.False(BoardRepairRules.MustReplace("(O)771", null));

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void QualityCrops_ReplacementThatCanCarryAStar_IsGold(int kept)
        => Assert.Equal(BoardRepairRules.QualityGold, BoardRepairRules.ReplacementQuality(QualityCrops, true, kept));

    [Fact]
    public void QualityCrops_ReplacementWithoutStars_KeepsKeptQuality()
        => Assert.Equal(0, BoardRepairRules.ReplacementQuality(QualityCrops, false, 0));

    [Fact]
    public void OtherBundles_KeepKeptQuality()
    {
        Assert.Equal(0, BoardRepairRules.ReplacementQuality(SpringCrops, true, 0));
        Assert.Equal(1, BoardRepairRules.ReplacementQuality(new DomainMatch(PoolDomain.Recipe, null), true, 1));
    }
}
