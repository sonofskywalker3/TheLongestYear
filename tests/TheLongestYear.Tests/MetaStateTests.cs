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
}
