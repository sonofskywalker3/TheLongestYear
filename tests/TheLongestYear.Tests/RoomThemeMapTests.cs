using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class RoomThemeMapTests
{
    [Theory]
    [InlineData("Pantry", Theme.Farming)]
    [InlineData("Crafts Room", Theme.Foraging)]
    [InlineData("CraftsRoom", Theme.Foraging)]
    [InlineData("Fish Tank", Theme.Fishing)]
    [InlineData("Boiler Room", Theme.Mining)]
    [InlineData("Bulletin Board", Theme.Mixed)]
    public void TryGetTheme_maps_known_rooms(string room, Theme expected)
    {
        Assert.True(RoomThemeMap.TryGetTheme(room, out Theme theme));
        Assert.Equal(expected, theme);
    }

    [Theory]
    [InlineData("Vault")]
    [InlineData("Abandoned Joja Mart")]
    [InlineData("Nonsense")]
    public void TryGetTheme_rejects_non_item_rooms(string room)
        => Assert.False(RoomThemeMap.TryGetTheme(room, out _));
}
