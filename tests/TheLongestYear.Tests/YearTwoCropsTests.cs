using System;
using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class YearTwoCropsTests
{
    // Artichoke is deliberately absent from every exclusion below. Vanilla's Fall Mixed Seeds
    // roll (Crop.getRandomLowGradeCropForThisSeason, Next(487, 491)) already includes Artichoke
    // Seeds 489 at 25% per seed, in any year, so a year-1 player can grow it without buying
    // anything. Only the shop listing is YEAR 2 gated.
    [Fact]
    public void No_upgrades_excludes_garlic_and_red_cabbage_but_not_artichoke_on_easy()
    {
        var ex = YearTwoCrops.ExcludedFor(_ => false, DifficultyStep.Easy);
        Assert.Equal(new[] { "(O)248", "(O)266" }, ex.OrderBy(x => x, System.StringComparer.Ordinal));
        Assert.DoesNotContain(YearTwoCrops.Artichoke, ex);
    }

    [Fact]
    public void Garlic_cultivation_frees_only_garlic_on_easy()
    {
        var ex = YearTwoCrops.ExcludedFor(id => id == YearTwoCrops.GarlicUpgrade, DifficultyStep.Easy);
        Assert.DoesNotContain("(O)248", ex);
        Assert.Contains("(O)266", ex);
    }

    [Fact]
    public void Red_cabbage_cultivation_frees_only_red_cabbage_on_easy()
    {
        var ex = YearTwoCrops.ExcludedFor(id => id == YearTwoCrops.RedCabbageUpgrade, DifficultyStep.Easy);
        Assert.Contains("(O)248", ex);
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
    public void Both_cultivation_upgrades_exist_cost_the_same_and_undercut_pierre()
    {
        UpgradeDefinition garlic = UpgradeCatalog.TryGet(YearTwoCrops.GarlicUpgrade)!;
        UpgradeDefinition redCabbage = UpgradeCatalog.TryGet(YearTwoCrops.RedCabbageUpgrade)!;
        UpgradeDefinition pierre = UpgradeCatalog.TryGet(YearTwoCrops.PierreUpgrade)!;

        Assert.Equal(garlic.Cost, redCabbage.Cost);
        // Both together must stay cheaper than the guaranteed shop route, or the random one is
        // pointless for anyone who wants more than a single crop.
        Assert.True(garlic.Cost + redCabbage.Cost < pierre.Cost);
        Assert.Equal(UpgradeCategory.Obtainability, garlic.Category);
    }

    [Fact]
    public void There_is_no_artichoke_cultivation_upgrade()
        => Assert.Null(UpgradeCatalog.TryGet("cult_artichoke"));

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

    // Jeff, 2026-10-21: without the Pierre buy the three crops only come from Mixed Seeds, so their
    // ask basis is 5, not the shop-seed 99 (a Fall board asked 49 Artichokes, JazzDaxx on Nexus).
    [Theory]
    [InlineData(YearTwoCrops.Garlic)]
    [InlineData(YearTwoCrops.RedCabbage)]
    [InlineData(YearTwoCrops.Artichoke)]
    public void Year_two_crop_basis_is_mixed_seeds_without_pierre_and_a_shop_seed_with_it(string id)
    {
        Assert.Equal(99, QuantityBasisTables.Crops[id]);
        Assert.Equal(YearTwoCrops.MixedSeedsOnlyAskBasis, QuantityAskPass.BasisByDeadline(id, Season.Fall, hasUpgrade: _ => false));
        Assert.Equal(99, QuantityAskPass.BasisByDeadline(id, Season.Fall, hasUpgrade: u => u == YearTwoCrops.PierreUpgrade));
        // A cultivation upgrade adds a Mixed Seeds route, not a shop, so it keeps the low basis.
        Assert.Equal(YearTwoCrops.MixedSeedsOnlyAskBasis,
            QuantityAskPass.BasisByDeadline(id, Season.Fall, hasUpgrade: u => u != YearTwoCrops.PierreUpgrade));
        // No save in hand: the generated table stands.
        Assert.Equal(99, QuantityAskPass.BasisByDeadline(id, Season.Fall));
    }

    [Theory]
    [InlineData(YearTwoCrops.Garlic)]
    [InlineData(YearTwoCrops.RedCabbage)]
    [InlineData(YearTwoCrops.Artichoke)]
    public void Year_two_crop_ask_keeps_the_difficulty_band_and_gold_factor_on_the_lower_basis(string id)
    {
        DifficultyProfile hard = DifficultyResolver.Resolve(new DifficultySettings { StackSize = DifficultyStep.Hard }, new GameplayConfig());
        for (int seed = 0; seed < 50; seed++)
        {
            int without = Roll(id, hard, seed, 0, _ => false);
            Assert.Equal(AskBands.Roll(YearTwoCrops.MixedSeedsOnlyAskBasis, hard, new Random(seed)), without);
            Assert.Equal(AskBands.ForGold(without, AskBands.CropGoldFactor), Roll(id, hard, seed, 2, _ => false));
            Assert.Equal(AskBands.Roll(99, hard, new Random(seed)), Roll(id, hard, seed, 0, u => u == YearTwoCrops.PierreUpgrade));
        }
    }

    private static int Roll(string id, DifficultyProfile profile, int seed, int quality, Func<string, bool> hasUpgrade)
        => QuantityAskPass.Apply(
            new BundleSpec("Pantry", 5, "Test", "Test", "O 495 30", 0, 1, new List<BundleSlotSpec> { new(id, 1, quality) }),
            profile, _ => null, new Random(seed), hasUpgrade: hasUpgrade).Slots[0].Stack;
}
