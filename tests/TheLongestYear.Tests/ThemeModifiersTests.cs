using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class ThemeModifiersTests
{
    [Theory]
    [InlineData(Theme.Foraging)]
    [InlineData(Theme.Farming)]
    [InlineData(Theme.Fishing)]
    [InlineData(Theme.Mining)]
    [InlineData(Theme.Mixed)]
    public void Every_theme_has_a_distinct_nonempty_bonus_and_liability(Theme theme)
    {
        var (bonus, liability) = ThemeModifiers.For(theme);
        Assert.False(string.IsNullOrWhiteSpace(bonus));
        Assert.False(string.IsNullOrWhiteSpace(liability));
        Assert.NotEqual(bonus, liability);
    }

    [Theory]
    [InlineData("forage_yield_up", "Foraging Yield +")]
    [InlineData("forage_drops_off", "No Forage Drops")]
    [InlineData("crop_growth_up", "Crop Growth +")]
    [InlineData("crop_growth_down", "Crop Growth −")]
    [InlineData("fish_bite_up", "Fish Bite +")]
    [InlineData("mine_drops_up", "Mine Drops +")]
    [InlineData("mine_drops_off", "No Mine Drops")]
    [InlineData("shop_discount", "Shop Discount")]
    [InlineData("stamina_drain_up", "Stamina Drains Faster")]
    public void DisplayNameFor_maps_known_ids(string id, string expected)
        => Assert.Equal(expected, ThemeModifiers.DisplayNameFor(id));

    [Fact]
    public void DisplayNameFor_falls_through_to_raw_id_when_unknown()
        => Assert.Equal("not-a-real-id", ThemeModifiers.DisplayNameFor("not-a-real-id"));
}
