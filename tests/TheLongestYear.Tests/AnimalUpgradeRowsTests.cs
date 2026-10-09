using System.Linq;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>The Animals tab (spec 2026-10-09, Ruling 4): the animal keeps moved there with their ids and
/// prices unchanged, so rows a save already owns stay owned.</summary>
[Collection("i18n")]
public class AnimalUpgradeRowsTests
{
    [Theory]
    [InlineData("keep_pet", 50)]
    [InlineData("early_horse", 450)]
    [InlineData("keep_coop", 600)]
    [InlineData("keep_big_coop", 1200)]
    [InlineData("keep_deluxe_coop", 2000)]
    [InlineData("keep_barn", 600)]
    [InlineData("keep_big_barn", 1200)]
    [InlineData("keep_deluxe_barn", 2000)]
    [InlineData("keep_silo", 150)]
    [InlineData("start_chicken", 400)]
    [InlineData("start_rabbit", 700)]
    [InlineData("start_ostrich", 1500)]
    [InlineData("herdbook_1", 750)]
    [InlineData("herdbook_17", 2250)]
    public void Moved_keeps_sit_in_Animals_with_unchanged_price(string id, long cost)
    {
        UpgradeDefinition def = UpgradeCatalog.TryGet(id)!;
        Assert.NotNull(def);
        Assert.Equal(UpgradeCategory.Animals, def.Category);
        Assert.Equal(cost, def.Cost);
    }

    [Fact]
    public void Moved_keeps_keep_their_gates()
    {
        Assert.Equal("building:Stable", UpgradeCatalog.TryGet("early_horse")!.RunReachRequirement);
        Assert.Equal("pet:1", UpgradeCatalog.TryGet("keep_pet")!.RunReachRequirement);
        Assert.Equal("building:Silo", UpgradeCatalog.TryGet("keep_silo")!.RunReachRequirement);
        Assert.Equal("keep_big_coop", UpgradeCatalog.TryGet("keep_deluxe_coop")!.PrerequisiteId);
        Assert.Equal("herdbook_16", UpgradeCatalog.TryGet("herdbook_17")!.PrerequisiteId);
        Assert.Null(UpgradeCatalog.TryGet("herdbook_1")!.PrerequisiteId);
    }

    [Fact]
    public void Fish_pond_and_house_keeps_stay_in_Buildings()
    {
        Assert.Equal(UpgradeCategory.Buildings, UpgradeCatalog.TryGet(FishPondKeep.UpgradeId)!.Category);
        Assert.Equal(UpgradeCategory.Buildings, UpgradeCatalog.TryGet("keep_kitchen")!.Category);
    }

    [Fact]
    public void Every_animals_row_prerequisite_is_in_the_same_tab()
    {
        // KeepShopFilter finds a chain's next tier within one category only.
        foreach (UpgradeDefinition def in UpgradeCatalog.ByCategory(UpgradeCategory.Animals))
            if (def.PrerequisiteId != null)
                Assert.Equal(UpgradeCategory.Animals, UpgradeCatalog.TryGet(def.PrerequisiteId)!.Category);
    }

    [Fact]
    public void Animals_tab_has_a_name()
        => Assert.Equal("Animals", ThemeDisplay.CategoryName(UpgradeCategory.Animals));
}
