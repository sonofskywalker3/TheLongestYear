using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class RunBaselineBuilderTests
{
    private const int Farming = 0;
    private const int Fishing = 1;
    private const int Foraging = 2;
    private const int Mining = 3;
    private const int Combat = 4;

    [Fact]
    public void Empty_meta_state_produces_pure_vanilla_baseline()
    {
        var baseline = RunBaselineBuilder.Build(
            new MetaState(), new RunState(), PlayerSnapshot.Empty, defaultStartingMoney: 500);

        Assert.Equal(500, baseline.StartingGold);
        Assert.Equal(12, baseline.MaxItems);
        Assert.Empty(baseline.ToolTiers);
        Assert.Empty(baseline.SkillLevels);
        Assert.Equal(0, baseline.MineElevatorFloor);
    }

    [Fact]
    public void Backpack_1_grants_24_slot_inventory()
    {
        var meta = new MetaState { OwnedUpgrades = { "backpack_1" } };
        var b = RunBaselineBuilder.Build(meta, new RunState(), PlayerSnapshot.Empty, 500);
        Assert.Equal(24, b.MaxItems);
    }

    [Fact]
    public void Backpack_2_grants_36_slot_inventory_taking_precedence()
    {
        var meta = new MetaState { OwnedUpgrades = { "backpack_1", "backpack_2" } };
        var b = RunBaselineBuilder.Build(meta, new RunState(), PlayerSnapshot.Empty, 500);
        Assert.Equal(36, b.MaxItems);
    }

    [Fact]
    public void Starter_gold_1_adds_1000g_to_default()
    {
        var meta = new MetaState { OwnedUpgrades = { "starter_gold_1" } };
        var b = RunBaselineBuilder.Build(meta, new RunState(), PlayerSnapshot.Empty, 500);
        Assert.Equal(1500, b.StartingGold);
    }

    [Fact]
    public void Starter_gold_2_replaces_starter_gold_1_for_a_total_of_3000g()
    {
        var meta = new MetaState { OwnedUpgrades = { "starter_gold_1", "starter_gold_2" } };
        var b = RunBaselineBuilder.Build(meta, new RunState(), PlayerSnapshot.Empty, 500);
        Assert.Equal(3000, b.StartingGold);
    }

    [Fact]
    public void Starter_gold_5_grants_max_bonus_of_25000g()
    {
        var meta = new MetaState
        {
            OwnedUpgrades = { "starter_gold_1", "starter_gold_2", "starter_gold_3",
                              "starter_gold_4", "starter_gold_5" }
        };
        var b = RunBaselineBuilder.Build(meta, new RunState(), PlayerSnapshot.Empty, 500);
        Assert.Equal(25500, b.StartingGold);
    }

    [Fact]
    public void Tool_keep_tier_appears_in_ToolTiers_when_peak_reached_it()
    {
        var meta = new MetaState
        {
            OwnedUpgrades = { "keep_hoe_1", "keep_hoe_2", "keep_hoe_3", "keep_pickaxe_1" }
        };
        // Peak this run: Gold Hoe (UpgradeLevel 3) + Copper Pickaxe (1). No axe held.
        var snapshot = new PlayerSnapshot
        {
            ToolTiers = new Dictionary<string, int> { ["hoe"] = 3, ["pickaxe"] = 1 }
        };
        var b = RunBaselineBuilder.Build(meta, new RunState(), snapshot, 500);
        Assert.Equal(3, b.ToolTiers["hoe"]);
        Assert.Equal(1, b.ToolTiers["pickaxe"]);
        Assert.False(b.ToolTiers.ContainsKey("axe"));
    }

    [Fact]
    public void Tool_keep_tier_is_granted_in_full_regardless_of_peak()
    {
        // Permanent floor: owning keep_hoe_4 (Iridium) always starts the run at Iridium.
        // The in-run peak no longer caps the grant (the purchase was already reach-gated).
        var meta = new MetaState
        {
            OwnedUpgrades = { "keep_hoe_1", "keep_hoe_2", "keep_hoe_3", "keep_hoe_4" }
        };
        var snapshot = new PlayerSnapshot
        {
            ToolTiers = new Dictionary<string, int> { ["hoe"] = 2 }   // ignored now
        };
        var b = RunBaselineBuilder.Build(meta, new RunState(), snapshot, 500);
        Assert.Equal(4, b.ToolTiers["hoe"]);
    }

    [Fact]
    public void Tool_keep_tier_granted_even_with_zero_peak()
    {
        // Owns keep_hoe_2; the grant no longer depends on the in-run peak, so the Steel hoe
        // is restored regardless of what the player held at run end.
        var meta = new MetaState { OwnedUpgrades = { "keep_hoe_1", "keep_hoe_2" } };
        var b = RunBaselineBuilder.Build(meta, new RunState(), PlayerSnapshot.Empty, 500);
        Assert.Equal(2, b.ToolTiers["hoe"]);
    }

    [Fact]
    public void Fishing_rod_keep_tier_1_writes_UpgradeLevel_2_when_peak_allows()
    {
        // keep_fishing_rod_1 = "Keep Fiberglass Rod" = UpgradeLevel 2 (bamboo at L1 is
        // vanilla Willy day-2 grant). Peak of 2 (Fiberglass) → baseline writes UpgradeLevel 2.
        var meta = new MetaState { OwnedUpgrades = { "keep_fishing_rod_1" } };
        var snapshot = new PlayerSnapshot
        {
            ToolTiers = new Dictionary<string, int> { ["fishing_rod"] = 2 }
        };
        var b = RunBaselineBuilder.Build(meta, new RunState(), snapshot, 500);
        Assert.Equal(2, b.ToolTiers["fishing_rod"]);
    }

    [Fact]
    public void Fishing_rod_keep_tier_2_writes_UpgradeLevel_3_when_peak_allows()
    {
        var meta = new MetaState { OwnedUpgrades = { "keep_fishing_rod_1", "keep_fishing_rod_2" } };
        var snapshot = new PlayerSnapshot
        {
            ToolTiers = new Dictionary<string, int> { ["fishing_rod"] = 3 }
        };
        var b = RunBaselineBuilder.Build(meta, new RunState(), snapshot, 500);
        Assert.Equal(3, b.ToolTiers["fishing_rod"]);
    }

    [Fact]
    public void Skill_keep_level_appears_in_SkillLevels_in_full()
    {
        // Owned keep levels are granted in full (no in-run-peak cap).
        var meta = new MetaState
        {
            OwnedUpgrades = { "keep_farming_level_1", "keep_farming_level_2", "keep_combat_level_5" }
        };
        var b = RunBaselineBuilder.Build(meta, new RunState(), PlayerSnapshot.Empty, 500);
        Assert.Equal(2, b.SkillLevels[Farming]);
        Assert.Equal(5, b.SkillLevels[Combat]);
        Assert.DoesNotContain(Fishing, b.SkillLevels.Keys);
    }

    [Fact]
    public void Profession_picker_requeue_includes_L5_and_L10_only_when_capped_level_hits_them()
    {
        var meta = new MetaState
        {
            OwnedUpgrades =
            {
                "keep_farming_level_1", "keep_farming_level_2", "keep_farming_level_3",
                "keep_farming_level_4", "keep_farming_level_5",
                "keep_mining_level_1", "keep_mining_level_2", "keep_mining_level_3",
                "keep_mining_level_4", "keep_mining_level_5", "keep_mining_level_6",
                "keep_mining_level_7", "keep_mining_level_8", "keep_mining_level_9",
                "keep_mining_level_10",
                "keep_combat_level_1", "keep_combat_level_2"   // L2, not L5/10
            }
        };
        // Reached Farming 5, Mining 10, Combat 2.
        var snapshot = new PlayerSnapshot
        {
            SkillLevels = new Dictionary<int, int> { [Farming] = 5, [Mining] = 10, [Combat] = 2 }
        };
        var b = RunBaselineBuilder.Build(meta, new RunState(), snapshot, 500);
        Assert.Contains(Farming, b.ProfessionPickerSkillsToRequeue);   // L5 kept
        Assert.Contains(Mining, b.ProfessionPickerSkillsToRequeue);    // L10 kept
        Assert.DoesNotContain(Combat, b.ProfessionPickerSkillsToRequeue);
    }

    [Fact]
    public void Profession_picker_requeues_for_owned_L5_regardless_of_peak()
    {
        // Owns keep_farming_level_5 -> Farming starts at L5 and the profession picker is
        // requeued, independent of the in-run peak.
        var meta = new MetaState
        {
            OwnedUpgrades =
            {
                "keep_farming_level_1", "keep_farming_level_2", "keep_farming_level_3",
                "keep_farming_level_4", "keep_farming_level_5"
            }
        };
        var b = RunBaselineBuilder.Build(meta, new RunState(), PlayerSnapshot.Empty, 500);
        Assert.Equal(5, b.SkillLevels[Farming]);
        Assert.Contains(Farming, b.ProfessionPickerSkillsToRequeue);
    }

    [Fact]
    public void Mine_elevator_keep_grants_owned_floor_regardless_of_peak()
    {
        var meta = new MetaState
        {
            OwnedUpgrades =
            {
                "keep_mine_elevator_10","keep_mine_elevator_20","keep_mine_elevator_30",
                "keep_mine_elevator_40","keep_mine_elevator_50","keep_mine_elevator_60",
                "keep_mine_elevator_70","keep_mine_elevator_80"   // owns up to F80
            }
        };
        var run = new RunState { PeakMineFloor = 55 };  // ignored now
        var b = RunBaselineBuilder.Build(meta, run, PlayerSnapshot.Empty, 500);
        Assert.Equal(80, b.MineElevatorFloor);
    }

    [Fact]
    public void Mine_elevator_floor_zero_when_no_keep_owned()
    {
        var b = RunBaselineBuilder.Build(
            new MetaState(),
            new RunState { PeakMineFloor = 90 },   // reached but never bought a keep
            PlayerSnapshot.Empty,
            500);
        Assert.Equal(0, b.MineElevatorFloor);
    }

    [Fact]
    public void Kitchen_bus_horse_flags_track_their_owned_upgrades()
    {
        var meta = new MetaState
        {
            OwnedUpgrades = { "keep_kitchen", "keep_bus_unlocked", "early_horse" }
        };
        var b = RunBaselineBuilder.Build(meta, new RunState(), PlayerSnapshot.Empty, 500);
        Assert.True(b.KitchenOnDay1);
        Assert.True(b.BusUnlocked);
        Assert.True(b.EarlyHorse);
    }

    [Fact]
    public void Basement_and_shortcuts_track_their_owned_upgrades()
    {
        var meta = new MetaState
        {
            OwnedUpgrades = { "keep_kitchen", "keep_basement", "keep_shortcuts" }
        };
        var b = RunBaselineBuilder.Build(meta, new RunState(), PlayerSnapshot.Empty, 500);
        Assert.True(b.KitchenOnDay1);
        Assert.True(b.BasementOnDay1);
        Assert.True(b.ShortcutsUnlocked);
    }

    [Fact]
    public void Basement_and_shortcuts_default_false_without_owned_upgrades()
    {
        var b = RunBaselineBuilder.Build(new MetaState(), new RunState(), PlayerSnapshot.Empty, 500);
        Assert.False(b.BasementOnDay1);
        Assert.False(b.ShortcutsUnlocked);
    }

    [Fact]
    public void KeptBuildings_uses_highest_owned_tier_per_housing_chain()
    {
        var meta = new MetaState
        {
            OwnedUpgrades = { "keep_coop", "keep_big_coop", "keep_barn" }
        };
        var b = RunBaselineBuilder.Build(meta, new RunState(), PlayerSnapshot.Empty, 500);
        Assert.Contains("Big Coop", b.KeptBuildings);
        Assert.DoesNotContain("Coop", b.KeptBuildings);          // superseded
        Assert.DoesNotContain("Deluxe Coop", b.KeptBuildings);   // not owned
        Assert.Contains("Barn", b.KeptBuildings);
    }

    [Fact]
    public void StartingAnimals_include_owned_start_animals_with_their_required_housing()
    {
        var meta = new MetaState
        {
            OwnedUpgrades = { "keep_coop", "start_chicken", "keep_barn", "start_cow" }
        };
        var b = RunBaselineBuilder.Build(meta, new RunState(), PlayerSnapshot.Empty, 500);
        Assert.Contains(b.StartingAnimals, a => a.VanillaType == "White Chicken" && a.HousingType == "Coop");
        Assert.Contains(b.StartingAnimals, a => a.VanillaType == "White Cow" && a.HousingType == "Barn");
    }

    [Fact]
    public void Bamboo_rod_keep_grants_upgrade_level_0()
    {
        // Bamboo Pole = FishingRod.UpgradeLevel 0; permanent floor once owned.
        var meta = new MetaState { OwnedUpgrades = { "keep_fishing_rod_0" } };
        var baseline = RunBaselineBuilder.Build(meta, new RunState(), PlayerSnapshot.Empty, 0);
        Assert.True(baseline.ToolTiers.TryGetValue("fishing_rod", out int lvl));
        Assert.Equal(0, lvl);
    }

    [Fact]
    public void No_rod_keep_means_no_fishing_rod_in_baseline()
    {
        // No rod keep owned -> the builder writes nothing (the -1 sentinel is distinct from
        // a real bamboo at UpgradeLevel 0).
        var baseline = RunBaselineBuilder.Build(new MetaState(), new RunState(), PlayerSnapshot.Empty, 0);
        Assert.False(baseline.ToolTiers.ContainsKey("fishing_rod"));
    }

    [Fact]
    public void Golden_scythe_keep_sets_baseline_flag()
    {
        var meta = new MetaState { OwnedUpgrades = { "keep_golden_scythe" } };
        var baseline = RunBaselineBuilder.Build(meta, new RunState(), PlayerSnapshot.Empty, 0);
        Assert.True(baseline.GrantGoldenScythe);
    }

    [Fact]
    public void Mastery_keep_sets_baseline_level_as_permanent_floor()
    {
        var meta = new MetaState { OwnedUpgrades = { "keep_mastery_1", "keep_mastery_2", "keep_mastery_3" } };
        var baseline = RunBaselineBuilder.Build(meta, new RunState(), PlayerSnapshot.Empty, 0);
        Assert.Equal(3, baseline.MasteryLevel);
    }
}
