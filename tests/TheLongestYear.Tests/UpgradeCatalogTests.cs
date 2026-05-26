using System.Linq;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class UpgradeCatalogTests
{
    [Fact]
    public void Catalog_is_non_empty_and_ids_are_unique()
    {
        var all = UpgradeCatalog.All;
        Assert.NotEmpty(all);
        Assert.Equal(all.Count, all.Select(u => u.Id).Distinct().Count());
    }

    [Theory]
    [InlineData(UpgradeCategory.Loadout)]
    [InlineData(UpgradeCategory.Carryover)]
    [InlineData(UpgradeCategory.Efficiency)]
    [InlineData(UpgradeCategory.Obtainability)]
    [InlineData(UpgradeCategory.Foresight)]
    [InlineData(UpgradeCategory.Stash)]
    [InlineData(UpgradeCategory.Buildings)]
    public void Every_category_has_at_least_one_entry(UpgradeCategory category)
        => Assert.NotEmpty(UpgradeCatalog.ByCategory(category));

    [Fact]
    public void Start_with_animal_upgrades_carry_a_species_meta_requirement()
    {
        var starters = UpgradeCatalog.All
            .Where(u => u.Id.StartsWith("start_"))
            .ToList();
        Assert.NotEmpty(starters);
        foreach (UpgradeDefinition u in starters)
            Assert.StartsWith("species:", u.MetaRequirement ?? "");
    }

    [Fact]
    public void Start_with_animal_upgrades_require_corresponding_housing()
    {
        // Every Start with [animal] entry must list a Keep [Coop|Barn] upgrade as its prerequisite
        // (the player can't keep an animal without first paying for the building).
        foreach (UpgradeDefinition u in UpgradeCatalog.All.Where(u => u.Id.StartsWith("start_")))
        {
            Assert.NotNull(u.PrerequisiteId);
            Assert.StartsWith("keep_", u.PrerequisiteId!);
        }
    }

    [Fact]
    public void Every_prerequisite_points_to_a_real_upgrade_id()
    {
        var allIds = UpgradeCatalog.All.Select(u => u.Id).ToHashSet();
        foreach (UpgradeDefinition u in UpgradeCatalog.All)
            if (u.PrerequisiteId != null)
                Assert.Contains(u.PrerequisiteId, allIds);
    }

    [Fact]
    public void Costs_are_all_positive()
        => Assert.All(UpgradeCatalog.All, u => Assert.True(u.Cost > 0, $"{u.Id} cost must be > 0"));

    [Fact]
    public void TryGet_returns_definition_or_null()
    {
        Assert.NotNull(UpgradeCatalog.TryGet("backpack_1"));
        Assert.Null(UpgradeCatalog.TryGet("not-a-real-id"));
    }

    [Fact]
    public void Weather_sage_chain_has_seven_tiers()
    {
        var weather = UpgradeCatalog.ByCategory(UpgradeCategory.Foresight)
            .Where(u => u.Id.StartsWith("weather_sage_"))
            .ToList();
        Assert.Equal(7, weather.Count);
    }
}
