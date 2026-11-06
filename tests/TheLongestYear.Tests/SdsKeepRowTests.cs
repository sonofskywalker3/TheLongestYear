using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>The two keep rows for Seven Deadly Sins' paid areas: offered only with the mod, bought
/// only once the bundle is paid this loop, and a kept bundle's record survives the rewind.</summary>
public class SdsKeepRowTests
{
    [Fact]
    public void The_rows_show_only_with_the_mod_installed()
    {
        var before = UpgradeCatalog.IsModLoaded;
        try
        {
            UpgradeCatalog.IsModLoaded = _ => false;
            Assert.DoesNotContain(UpgradeCatalog.ByCategory(UpgradeCategory.Buildings), d => SdsUnlocks.IsSdsRow(d.Id));
            UpgradeCatalog.IsModLoaded = id => id == SdsUnlocks.ModId;
            var rows = UpgradeCatalog.ByCategory(UpgradeCategory.Buildings);
            Assert.Equal(SdsUnlocks.KeepCliffRoadCost, Assert.Single(rows, d => d.Id == SdsUnlocks.KeepCliffRoadId).Cost);
            Assert.Equal(SdsUnlocks.KeepOldMineCost, Assert.Single(rows, d => d.Id == SdsUnlocks.KeepOldMineId).Cost);
        }
        finally
        {
            UpgradeCatalog.IsModLoaded = before;
        }
    }

    [Fact]
    public void Each_row_needs_its_bundle_paid_this_loop()
    {
        RunReachRequirement? mine = RunReachRequirement.Parse(UpgradeCatalog.TryGet(SdsUnlocks.KeepOldMineId)!.RunReachRequirement);
        Assert.Equal("bundle", mine!.Metric);
        Assert.Equal("SDSCavebase.open", mine.Key);
        Assert.True(mine.IsMet(1));
        Assert.False(mine.IsMet(0));
        Assert.Equal("SDScliff.open", RunReachRequirement.Parse(UpgradeCatalog.TryGet(SdsUnlocks.KeepCliffRoadId)!.RunReachRequirement)!.Key);
    }

    [Fact]
    public void A_bought_keep_leaves_its_bundles_paid()
    {
        Assert.Empty(SdsUnlocks.KeptBundles(_ => false));
        Assert.Equal(new[] { "SDSCavebase.open" }, SdsUnlocks.KeptBundles(id => id == SdsUnlocks.KeepOldMineId));
        var cliff = SdsUnlocks.KeptBundles(id => id == SdsUnlocks.KeepCliffRoadId);
        Assert.Contains("SDScliffroad.roadopen", cliff);
        Assert.Contains("SDScliff.open", cliff);
        Assert.Equal(2, cliff.Count);
    }
}
