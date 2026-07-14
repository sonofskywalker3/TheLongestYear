using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class EngineModeDeciderTests
{
    [Fact]
    public void Decide_ReturnsEngineManifest_WhenMarkerEqualsCompletedResets()
    {
        Assert.Equal(
            RequirementsSource.EngineManifest,
            EngineModeDecider.Decide(marker: 3, completedResets: 3, ccTouched: false));
    }

    [Fact]
    public void Decide_ReturnsEngineManifest_WhenMarkerEqualsCompletedResets_FreshGenerationStampedZero()
    {
        Assert.Equal(
            RequirementsSource.EngineManifest,
            EngineModeDecider.Decide(marker: 0, completedResets: 0, ccTouched: false));
    }

    [Fact]
    public void Decide_ReturnsGenerateFreshRun_OnlyForPristineFirstRun()
    {
        Assert.Equal(
            RequirementsSource.GenerateFreshRun,
            EngineModeDecider.Decide(marker: -1, completedResets: 0, ccTouched: false));
    }

    [Fact]
    public void Decide_ReturnsLegacyReadAndClassify_WhenPreEngineSaveHasDonations()
    {
        Assert.Equal(
            RequirementsSource.LegacyReadAndClassify,
            EngineModeDecider.Decide(marker: -1, completedResets: 0, ccTouched: true));
    }

    [Fact]
    public void Decide_ReturnsLegacyReadAndClassify_WhenPreEngineSaveIsMidLoopAfterResets()
    {
        Assert.Equal(
            RequirementsSource.LegacyReadAndClassify,
            EngineModeDecider.Decide(marker: -1, completedResets: 5, ccTouched: false));
    }

    [Fact]
    public void Decide_ReturnsLegacyReadAndClassify_WhenMarkerIsBehindCompletedResets_Stale()
    {
        Assert.Equal(
            RequirementsSource.LegacyReadAndClassify,
            EngineModeDecider.Decide(marker: 2, completedResets: 3, ccTouched: false));
    }
}
