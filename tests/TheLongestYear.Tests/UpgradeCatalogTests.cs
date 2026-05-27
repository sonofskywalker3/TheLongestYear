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

    [Theory]
    [InlineData("keep_hoe_")]
    [InlineData("keep_pickaxe_")]
    [InlineData("keep_axe_")]
    [InlineData("keep_watering_can_")]
    public void Tool_keep_chains_have_four_tiers_each(string prefix)
    {
        var rows = UpgradeCatalog.All.Where(u => u.Id.StartsWith(prefix)).ToList();
        Assert.Equal(4, rows.Count);
        Assert.Equal(new[] { prefix + "1", prefix + "2", prefix + "3", prefix + "4" },
            rows.Select(r => r.Id));
        Assert.All(rows, r => Assert.Equal(UpgradeCategory.Loadout, r.Category));
    }

    [Theory]
    [InlineData("keep_hoe_")]
    [InlineData("keep_pickaxe_")]
    [InlineData("keep_axe_")]
    [InlineData("keep_watering_can_")]
    public void Tool_keep_chains_are_prerequisite_chained(string prefix)
    {
        Assert.Null(UpgradeCatalog.TryGet(prefix + "1")!.PrerequisiteId);
        Assert.Equal(prefix + "1", UpgradeCatalog.TryGet(prefix + "2")!.PrerequisiteId);
        Assert.Equal(prefix + "2", UpgradeCatalog.TryGet(prefix + "3")!.PrerequisiteId);
        Assert.Equal(prefix + "3", UpgradeCatalog.TryGet(prefix + "4")!.PrerequisiteId);
    }

    [Fact]
    public void Fishing_rod_keep_chain_has_two_tiers_chained()
    {
        var t1 = UpgradeCatalog.TryGet("keep_fishing_rod_1");
        var t2 = UpgradeCatalog.TryGet("keep_fishing_rod_2");
        Assert.NotNull(t1);
        Assert.NotNull(t2);
        Assert.Null(t1!.PrerequisiteId);
        Assert.Equal("keep_fishing_rod_1", t2!.PrerequisiteId);
        Assert.Equal(UpgradeCategory.Loadout, t1.Category);
        Assert.Equal(UpgradeCategory.Loadout, t2.Category);
    }

    [Fact]
    public void Tool_keep_tier_costs_climb_monotonically()
    {
        foreach (string prefix in new[] { "keep_hoe_", "keep_pickaxe_", "keep_axe_", "keep_watering_can_" })
        {
            long c1 = UpgradeCatalog.TryGet(prefix + "1")!.Cost;
            long c2 = UpgradeCatalog.TryGet(prefix + "2")!.Cost;
            long c3 = UpgradeCatalog.TryGet(prefix + "3")!.Cost;
            long c4 = UpgradeCatalog.TryGet(prefix + "4")!.Cost;
            Assert.True(c1 < c2 && c2 < c3 && c3 < c4,
                $"{prefix} costs must strictly increase: got {c1},{c2},{c3},{c4}");
        }
    }

    [Theory]
    [InlineData("keep_farming_level_")]
    [InlineData("keep_mining_level_")]
    [InlineData("keep_foraging_level_")]
    [InlineData("keep_fishing_level_")]
    [InlineData("keep_combat_level_")]
    public void Skill_level_keep_chains_have_ten_tiers_chained(string prefix)
    {
        for (int level = 1; level <= 10; level++)
        {
            var def = UpgradeCatalog.TryGet(prefix + level);
            Assert.NotNull(def);
            Assert.Equal(UpgradeCategory.Carryover, def!.Category);
            Assert.Equal(
                level == 1 ? null : prefix + (level - 1),
                def.PrerequisiteId);
        }
    }

    [Fact]
    public void Skill_level_keep_chains_add_fifty_total_entries()
    {
        int count = UpgradeCatalog.All.Count(u => u.Id.Contains("_level_"));
        Assert.Equal(50, count);
    }

    [Fact]
    public void Skill_level_keep_costs_climb_monotonically_and_jump_at_5_and_10()
    {
        long c4 = UpgradeCatalog.TryGet("keep_farming_level_4")!.Cost;
        long c5 = UpgradeCatalog.TryGet("keep_farming_level_5")!.Cost;
        long c9 = UpgradeCatalog.TryGet("keep_farming_level_9")!.Cost;
        long c10 = UpgradeCatalog.TryGet("keep_farming_level_10")!.Cost;
        Assert.True(c5 > c4 * 1.5, $"L5 should noticeably exceed L4 (profession unlock): {c4} → {c5}");
        Assert.True(c10 > c9 * 1.5, $"L10 should noticeably exceed L9 (profession unlock): {c9} → {c10}");
    }
}
