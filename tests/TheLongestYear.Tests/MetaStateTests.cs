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
}
