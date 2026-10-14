using Newtonsoft.Json;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>Fields removed from GameplayConfig must not break a player's existing config.json:
/// SMAPI reads it with Json.NET, which skips keys the class no longer has.</summary>
public class GameplayConfigLegacyKeysTests
{
    private const string OldConfig =
        "{\"StartingMoney\":750,\"DefaultWeatherPreviewSlots\":2,\"DefaultCartPreviewSlots\":3,\"WeeklyHubHotkey\":\"P\",\"Enabled\":false}";

    [Fact]
    public void Old_config_with_removed_preview_and_hotkey_fields_still_loads()
    {
        var config = JsonConvert.DeserializeObject<GameplayConfig>(OldConfig)!;

        Assert.Equal(750, config.StartingMoney);
        Assert.False(config.Enabled);
    }

    [Fact]
    public void Removed_fields_are_no_longer_written()
    {
        string json = JsonConvert.SerializeObject(new GameplayConfig());

        Assert.DoesNotContain("DefaultWeatherPreviewSlots", json);
        Assert.DoesNotContain("DefaultCartPreviewSlots", json);
        Assert.DoesNotContain("WeeklyHubHotkey", json);
    }
}
