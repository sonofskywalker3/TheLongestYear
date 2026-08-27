using System.Text.Json;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class MetaStateTests
{
    [Fact]
    public void Round_trips_through_json()
    {
        var original = new MetaState
        {
            JunimoPoints = 123,
            StashCapacityTier = 2,
            OwnedUpgrades = { "backpack_1", "cult_redcabbage" }
        };

        string json = JsonSerializer.Serialize(original);
        MetaState restored = JsonSerializer.Deserialize<MetaState>(json)!;

        Assert.Equal(123, restored.JunimoPoints);
        Assert.Equal(2, restored.StashCapacityTier);
        Assert.Equal(new[] { "backpack_1", "cult_redcabbage" }, restored.OwnedUpgrades);
    }

    [Fact]
    public void New_meta_state_starts_empty()
    {
        var s = new MetaState();
        Assert.Equal(0, s.JunimoPoints);
        Assert.Equal(0, s.StashCapacityTier);
        Assert.Empty(s.OwnedUpgrades);
    }

    [Fact]
    public void Has_upgrade_checks_membership()
    {
        var s = new MetaState { OwnedUpgrades = { "horse_early" } };
        Assert.True(s.HasUpgrade("horse_early"));
        Assert.False(s.HasUpgrade("backpack_1"));
    }

    [Fact]
    public void BackupDone_round_trips_and_defaults_false()
    {
        Assert.False(new MetaState().BackupDone);

        var original = new MetaState { BackupDone = true };
        string json = System.Text.Json.JsonSerializer.Serialize(original);
        MetaState restored = System.Text.Json.JsonSerializer.Deserialize<MetaState>(json)!;
        Assert.True(restored.BackupDone);
    }

    [Fact]
    public void MeetsMetaRequirement_null_or_empty_is_always_true()
    {
        var s = new MetaState();
        Assert.True(s.MeetsMetaRequirement(null));
        Assert.True(s.MeetsMetaRequirement(""));
    }

    [Fact]
    public void MeetsMetaRequirement_species_checks_ever_owned()
    {
        var s = new MetaState { AnimalSpeciesEverOwned = { "Chicken", "Duck" } };
        Assert.True(s.MeetsMetaRequirement("species:Chicken"));
        Assert.True(s.MeetsMetaRequirement("species:Duck"));
        Assert.False(s.MeetsMetaRequirement("species:Pig"));
    }

    [Fact]
    public void MeetsMetaRequirement_unknown_namespace_returns_false()
    {
        var s = new MetaState();
        Assert.False(s.MeetsMetaRequirement("completed:GingerIsland"));
        Assert.False(s.MeetsMetaRequirement("malformed-no-colon"));
    }

    [Fact]
    public void AnimalSpeciesEverOwned_round_trips_through_json()
    {
        var original = new MetaState { AnimalSpeciesEverOwned = { "Chicken", "Cow" } };
        string json = System.Text.Json.JsonSerializer.Serialize(original);
        MetaState restored = System.Text.Json.JsonSerializer.Deserialize<MetaState>(json)!;
        Assert.Equal(new[] { "Chicken", "Cow" }, restored.AnimalSpeciesEverOwned);
    }

    [Fact]
    public void New_meta_state_starts_with_zero_completed_resets_and_empty_quest_mail_lists()
    {
        var s = new MetaState();
        Assert.Equal(0, s.CompletedResets);
        Assert.Empty(s.CompletedQuestsEver);
        Assert.Empty(s.MailFlagsEverReceived);
    }

    [Fact]
    public void New_tracking_fields_round_trip_through_json()
    {
        var original = new MetaState
        {
            CompletedResets = 7,
            CompletedQuestsEver = { "quest_a", "quest_b" },
            MailFlagsEverReceived = { "ccPantry", "JojaMember" }
        };
        string json = System.Text.Json.JsonSerializer.Serialize(original);
        MetaState restored = System.Text.Json.JsonSerializer.Deserialize<MetaState>(json)!;
        Assert.Equal(7, restored.CompletedResets);
        Assert.Equal(new[] { "quest_a", "quest_b" }, restored.CompletedQuestsEver);
        Assert.Equal(new[] { "ccPantry", "JojaMember" }, restored.MailFlagsEverReceived);
    }

    [Fact]
    public void MeetsMetaRequirement_upgrade_returns_true_when_owned()
    {
        var s = new MetaState { OwnedUpgrades = { "backpack_1" } };
        Assert.True(s.MeetsMetaRequirement("upgrade:backpack_1"));
        Assert.False(s.MeetsMetaRequirement("upgrade:backpack_2"));
    }

    [Fact]
    public void MeetsMetaRequirement_quest_checks_CompletedQuestsEver()
    {
        var s = new MetaState { CompletedQuestsEver = { "quest_a" } };
        Assert.True(s.MeetsMetaRequirement("quest:quest_a"));
        Assert.False(s.MeetsMetaRequirement("quest:never_done"));
    }

    [Fact]
    public void MeetsMetaRequirement_mail_checks_MailFlagsEverReceived_case_insensitive()
    {
        var s = new MetaState { MailFlagsEverReceived = { "ccPantry" } };
        Assert.True(s.MeetsMetaRequirement("mail:ccPantry"));
        Assert.True(s.MeetsMetaRequirement("mail:ccpantry"));   // case-insensitive like species:
        Assert.False(s.MeetsMetaRequirement("mail:JojaMember"));
    }

    [Fact]
    public void MeetsMetaRequirement_season_compares_int_threshold_to_CompletedResets()
    {
        var s = new MetaState { CompletedResets = 3 };
        Assert.True(s.MeetsMetaRequirement("season:0"));
        Assert.True(s.MeetsMetaRequirement("season:3"));
        Assert.False(s.MeetsMetaRequirement("season:4"));
    }

    [Fact]
    public void MeetsMetaRequirement_season_returns_false_when_value_is_not_an_int()
    {
        var s = new MetaState { CompletedResets = 5 };
        Assert.False(s.MeetsMetaRequirement("season:abc"));
        Assert.False(s.MeetsMetaRequirement("season:"));
    }

    [Fact]
    public void MeetsMetaRequirement_unknown_namespace_still_returns_false()
    {
        var s = new MetaState();
        Assert.False(s.MeetsMetaRequirement("future:something"));
        Assert.False(s.MeetsMetaRequirement("malformed-no-colon"));
    }

    [Fact]
    public void HighestKeptTier_returns_zero_when_no_keep_upgrades_owned()
    {
        var s = new MetaState();
        Assert.Equal(0, s.HighestKeptTier("keep_hoe_", maxTier: 4));
    }

    [Fact]
    public void HighestKeptTier_returns_the_highest_owned_tier()
    {
        var s = new MetaState
        {
            OwnedUpgrades = { "keep_hoe_1", "keep_hoe_2", "keep_hoe_3", "backpack_1" }
        };
        Assert.Equal(3, s.HighestKeptTier("keep_hoe_", maxTier: 4));
    }

    [Fact]
    public void HighestKeptTier_ignores_non_matching_prefixes()
    {
        var s = new MetaState { OwnedUpgrades = { "keep_axe_4", "keep_hoe_1" } };
        Assert.Equal(1, s.HighestKeptTier("keep_hoe_", maxTier: 4));
    }

    [Fact]
    public void HighestKeptTier_caps_at_maxTier_and_ignores_higher_owned_ids()
    {
        // Defensive: if a hand-edited save somehow has keep_hoe_99, we cap at maxTier.
        var s = new MetaState { OwnedUpgrades = { "keep_hoe_99" } };
        Assert.Equal(0, s.HighestKeptTier("keep_hoe_", maxTier: 4));
    }

    [Fact]
    public void HighestKeptTier_includes_the_exact_maxTier()
    {
        var s = new MetaState { OwnedUpgrades = { "keep_hoe_4" } };
        Assert.Equal(4, s.HighestKeptTier("keep_hoe_", maxTier: 4));
    }

    [Fact]
    public void HighestKeptTier_skips_non_numeric_suffixes()
    {
        var s = new MetaState { OwnedUpgrades = { "keep_hoe_iridium", "keep_hoe_2" } };
        Assert.Equal(2, s.HighestKeptTier("keep_hoe_", maxTier: 4));
    }

    [Fact]
    public void New_meta_state_has_empty_cookbook_craftbook_and_dismissed_indicators()
    {
        var s = new MetaState();
        Assert.Empty(s.CookbookRecipes);
        Assert.Empty(s.CraftbookRecipes);
        Assert.Empty(s.DismissedIndicators);
    }

    [Fact]
    public void Cookbook_craftbook_dismissed_indicators_round_trip_through_json()
    {
        var original = new MetaState
        {
            CookbookRecipes      = { "Fried_Egg", "Bread", "Salad" },
            CraftbookRecipes     = { "Wood_Fence", "Chest" },
            DismissedIndicators  = { "tly.cookbook", "tly.fireplace" }
        };
        string json = System.Text.Json.JsonSerializer.Serialize(original);
        MetaState restored = System.Text.Json.JsonSerializer.Deserialize<MetaState>(json)!;

        Assert.Equal(new[] { "Fried_Egg", "Bread", "Salad" }, restored.CookbookRecipes);
        Assert.Equal(new[] { "Wood_Fence", "Chest" }, restored.CraftbookRecipes);
        Assert.Equal(new HashSet<string> { "tly.cookbook", "tly.fireplace" }, restored.DismissedIndicators);
    }

    [Fact]
    public void DismissedIndicators_is_a_hashset_and_deduplicates()
    {
        var s = new MetaState();
        s.DismissedIndicators.Add("tly.cookbook");
        s.DismissedIndicators.Add("tly.cookbook");   // duplicate
        Assert.Single(s.DismissedIndicators);
    }

    [Fact]
    public void StashItems_defaults_empty_and_round_trips_through_json()
    {
        var s = new MetaState();
        Assert.Empty(s.StashItems);

        var original = new MetaState
        {
            StashItems =
            {
                new StashItemRecord("(O)24", 2, 0),
                new StashItemRecord("(O)698", 1, 4)
            }
        };
        string json = JsonSerializer.Serialize(original);
        MetaState restored = JsonSerializer.Deserialize<MetaState>(json)!;

        Assert.Equal(2, restored.StashItems.Count);
        Assert.Equal("(O)24", restored.StashItems[0].ItemId);
        Assert.Equal(2, restored.StashItems[0].Quantity);
        Assert.Equal(0, restored.StashItems[0].Quality);
        Assert.Equal("(O)698", restored.StashItems[1].ItemId);
        Assert.Equal(1, restored.StashItems[1].Quantity);
        Assert.Equal(4, restored.StashItems[1].Quality);
    }

    [Fact]
    public void StashSlotCount_returns_4_when_no_stash_upgrade_owned()
    {
        var s = new MetaState();
        Assert.Equal(4, s.StashSlotCount);
    }

    [Fact]
    public void StashSlotCount_returns_8_when_stash_1_owned()
    {
        var s = new MetaState { OwnedUpgrades = { "stash_1" } };
        Assert.Equal(8, s.StashSlotCount);
    }

    [Fact]
    public void StashSlotCount_returns_12_when_stash_2_owned()
    {
        var s = new MetaState { OwnedUpgrades = { "stash_1", "stash_2" } };
        Assert.Equal(12, s.StashSlotCount);
    }

    [Fact]
    public void StashSlotCount_returns_16_when_stash_3_owned()
    {
        var s = new MetaState { OwnedUpgrades = { "stash_1", "stash_2", "stash_3" } };
        Assert.Equal(16, s.StashSlotCount);
    }

    [Fact]
    public void GameplayConfig_stash_tile_defaults_to_auto_pick_sentinel()
    {
        // 2026-05-28: (72, 12) was a bad hardcoded default — landed under the
        // farmhouse roof on the user's playtest save. New default is the (0, 0)
        // sentinel, which tells JunimoStashService to auto-pick relative to the
        // FarmHouse entry at runtime. See JunimoStashService.AutoTile.
        var c = new GameplayConfig();
        Assert.Equal(0, c.StashTileX);
        Assert.Equal(0, c.StashTileY);
    }

    [Fact]
    public void GameplayConfig_Enabled_defaults_to_true()
    {
        var c = new GameplayConfig();
        Assert.True(c.Enabled);
    }

    [Fact]
    public void MeetsMetaRequirement_UpgradesNamespace_RequiresAllListedIds()
    {
        var meta = new MetaState();
        meta.OwnedUpgrades.Add("xp_mult_farming_4");
        meta.OwnedUpgrades.Add("xp_mult_fishing_4");

        Assert.True(meta.MeetsMetaRequirement("upgrades:xp_mult_farming_4,xp_mult_fishing_4"));
        Assert.True(meta.MeetsMetaRequirement("upgrades:xp_mult_farming_4, xp_mult_fishing_4")); // tolerant of spaces
        Assert.False(meta.MeetsMetaRequirement("upgrades:xp_mult_farming_4,xp_mult_combat_4"));
        Assert.False(meta.MeetsMetaRequirement("upgrades:"));
    }

    [Fact]
    public void BundlesGeneratedForReset_DefaultsToNever()
    {
        Assert.Equal(-1, new MetaState().BundlesGeneratedForReset);
    }

    [Fact]
    public void BundleSeedLoop_defaults_to_minus_one_and_round_trips()
    {
        Assert.Equal(-1, new MetaState().BundleSeedLoop);
        Assert.Equal(0, new MetaState().ConsecutiveHolds);
        Assert.False(new MetaState().HoldChoiceMadeForReset);

        var original = new MetaState { BundleSeedLoop = 3, ConsecutiveHolds = 2, HoldChoiceMadeForReset = true };
        string json = System.Text.Json.JsonSerializer.Serialize(original);
        MetaState restored = System.Text.Json.JsonSerializer.Deserialize<MetaState>(json)!;
        Assert.Equal(3, restored.BundleSeedLoop);
        Assert.Equal(2, restored.ConsecutiveHolds);
        Assert.True(restored.HoldChoiceMadeForReset);
    }

    [Fact]
    public void EffectiveBundleSeedLoop_falls_back_to_CompletedResets_when_unset()
    {
        var legacy = new MetaState { CompletedResets = 5 };
        Assert.Equal(5, legacy.EffectiveBundleSeedLoop);

        var held = new MetaState { CompletedResets = 5, BundleSeedLoop = 2 };
        Assert.Equal(2, held.EffectiveBundleSeedLoop);
    }

    [Fact]
    public void Pity_fields_round_trip_and_default()
    {
        var fresh = new MetaState();
        Assert.Equal(new[] { 0, 0, 0, 0 }, fresh.SeasonFailCounts);
        Assert.Equal(-1, fresh.LastFailSeason);
        Assert.Equal(-1, fresh.BoardTrimSeason);
        Assert.Equal(0, fresh.BoardTrimSteps);

        var original = new MetaState { SeasonFailCounts = new System.Collections.Generic.List<int> { 1, 6, 0, 0 }, LastFailSeason = 1, BoardTrimSeason = 1, BoardTrimSteps = 2 };
        string json = JsonSerializer.Serialize(original);
        MetaState restored = JsonSerializer.Deserialize<MetaState>(json)!;
        Assert.Equal(new[] { 1, 6, 0, 0 }, restored.SeasonFailCounts);
        Assert.Equal(1, restored.LastFailSeason);
        Assert.Equal(1, restored.BoardTrimSeason);
        Assert.Equal(2, restored.BoardTrimSteps);
    }
}

public class MetaStateDifficultyStampTests
{
    /// <summary>A save from before difficulty modifiers existed has no stamp, and must behave
    /// exactly as it did: all-Normal, resolved live from config.</summary>
    [Fact]
    public void A_Legacy_Save_Resolves_Live_From_Config()
    {
        var meta = new MetaState();
        var cfg = new GameplayConfig();

        Assert.Null(meta.Difficulty);
        Assert.Equal(cfg.StartingMoney, meta.EffectiveDifficulty(cfg).StartingGold);
        Assert.True(meta.EffectiveDifficulty(cfg).Steps.IsAllNormal());
    }

    /// <summary>The whole point of stamping: a GMCM change mid-run must not reach the loop the
    /// player is already inside.</summary>
    [Fact]
    public void The_Stamp_Wins_Over_A_Config_Changed_Mid_Run()
    {
        var cfg = new GameplayConfig();
        var meta = new MetaState
        {
            Difficulty = DifficultyResolver.Resolve(new DifficultySettings(), cfg),
        };

        cfg.Difficulty.StartingGold = DifficultyStep.Extreme;
        cfg.Difficulty.JpEarned = DifficultyStep.Extreme;

        Assert.Equal(500, meta.EffectiveDifficulty(cfg).StartingGold);
        Assert.Equal(1.0, meta.EffectiveDifficulty(cfg).JpEarnedFactor);
    }

    /// <summary>With no stamp yet, a config change DOES apply: that is the first-run case, before
    /// any reset has stamped anything.</summary>
    [Fact]
    public void Without_A_Stamp_A_Config_Change_Applies()
    {
        var cfg = new GameplayConfig();
        cfg.Difficulty.StartingGold = DifficultyStep.Extreme;

        Assert.Equal(0, new MetaState().EffectiveDifficulty(cfg).StartingGold);
    }

    /// <summary>The stamp is nested state on a save-serialized object, so a round trip has to
    /// bring back every resolved value AND the step names the diagnostics command reports.</summary>
    [Fact]
    public void The_Stamp_Survives_A_Save_Round_Trip()
    {
        var cfg = new GameplayConfig();
        var original = new MetaState
        {
            Difficulty = DifficultyResolver.Resolve(
                new DifficultySettings
                {
                    StackSize = DifficultyStep.Hard,
                    JpEarned = DifficultyStep.Extreme,
                    SeasonPity = DifficultyStep.Extreme,
                },
                cfg),
        };

        string json = JsonSerializer.Serialize(original);
        MetaState restored = JsonSerializer.Deserialize<MetaState>(json)!;

        Assert.NotNull(restored.Difficulty);
        Assert.Equal(1.5, restored.Difficulty!.StackFactor, 6);
        Assert.Equal(0.5, restored.Difficulty.JpEarnedFactor, 6);
        Assert.False(restored.Difficulty.Pity.Enabled);
        Assert.Equal(DifficultyStep.Hard, restored.Difficulty.Steps.StackSize);
        Assert.Equal(DifficultyStep.Extreme, restored.Difficulty.Steps.SeasonPity);
    }
}

public class BoardDifficultyTests
{
    /// <summary>Regression, found in game 2026-08-27: re-deriving a stampless save's board against
    /// LIVE config made the manifest check fail and demoted a healthy engine save to the legacy
    /// read path. A save with no stamp predates difficulty modifiers, so its board is all-Normal.</summary>
    [Fact]
    public void A_Stampless_Save_Re_Derives_As_All_Normal_Even_When_Config_Has_Changed()
    {
        var cfg = new GameplayConfig();
        cfg.Difficulty.StackSize = DifficultyStep.Hard;
        cfg.Difficulty.RequiredSlots = DifficultyStep.Hard;
        var meta = new MetaState();

        DifficultyProfile board = meta.BoardDifficulty(cfg);

        Assert.Equal(1.0, board.StackFactor);
        Assert.Equal(0, board.RequiredSlotsDelta);
        Assert.True(board.Steps.AsksAllNormal());
    }

    /// <summary>Economy reads still honour a config change on a stampless save, so a player who has
    /// never reset gets the setting he just chose.</summary>
    [Fact]
    public void Economy_Reads_Still_Follow_Live_Config_On_A_Stampless_Save()
    {
        var cfg = new GameplayConfig();
        cfg.Difficulty.StartingGold = DifficultyStep.Extreme;

        Assert.Equal(0, new MetaState().EffectiveDifficulty(cfg).StartingGold);
    }

    [Fact]
    public void A_Stamped_Save_Re_Derives_From_Its_Stamp()
    {
        var cfg = new GameplayConfig();
        var meta = new MetaState
        {
            Difficulty = DifficultyResolver.Resolve(
                new DifficultySettings { StackSize = DifficultyStep.Hard }, cfg),
        };

        Assert.Equal(1.5, meta.BoardDifficulty(cfg).StackFactor);
    }
}
