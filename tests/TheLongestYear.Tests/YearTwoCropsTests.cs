using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class YearTwoCropsTests
{
    [Fact]
    public void No_upgrades_excludes_all_three_on_easy()
    {
        var ex = YearTwoCrops.ExcludedFor(_ => false, DifficultyStep.Easy);
        Assert.Equal(new[] { "(O)248", "(O)266", "(O)274" }, ex.OrderBy(x => x, System.StringComparer.Ordinal));
    }

    [Fact]
    public void Red_cabbage_upgrade_frees_only_red_cabbage_on_easy()
    {
        var ex = YearTwoCrops.ExcludedFor(id => id == YearTwoCrops.RedCabbageUpgrade, DifficultyStep.Easy);
        Assert.Contains("(O)248", ex);
        Assert.Contains("(O)274", ex);
        Assert.DoesNotContain("(O)266", ex);
    }

    [Fact]
    public void Pierre_upgrade_frees_everything_on_easy()
        => Assert.Empty(YearTwoCrops.ExcludedFor(id => id == YearTwoCrops.PierreUpgrade, DifficultyStep.Easy));

    [Fact]
    public void Year_two_crops_are_excluded_only_on_easy()
    {
        Assert.Contains("(O)266", YearTwoCrops.ExcludedFor(_ => false, DifficultyStep.Easy));
        Assert.Empty(YearTwoCrops.ExcludedFor(_ => false, DifficultyStep.Normal));
    }

    [Fact]
    public void Extra_excluded_ids_keep_crops_out_of_the_pools_on_easy()
    {
        var pools = ItemPoolBuilder.Build(
            new[] { new RawCropEntry("248", new[] { Season.Spring }), new RawCropEntry("24", new[] { Season.Spring }) },
            new Dictionary<string, RawObjectEntry>
            {
                ["248"] = new("Basic", -75, 60, false, new List<string>()),
                ["24"] = new("Basic", -75, 35, false, new List<string>()),
            },
            new List<RawSpawnEntry>(), new List<RawSpawnEntry>(), new HashSet<string>(),
            new List<RawMonsterDropEntry>(), new List<RawFruitTreeEntry>(), new List<RawGeodeDropEntry>(),
            new BundleGenerationTuning(),
            extraExcludedIds: YearTwoCrops.ExcludedFor(_ => false, DifficultyStep.Easy));
        Assert.Equal(new[] { "(O)24" }, pools.Crops.Select(p => p.ItemId));
        Assert.DoesNotContain("(O)248", pools.QualityEligibleIds!);
    }
}
