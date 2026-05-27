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
    [InlineData("forage_yield_up", "+25% Foraging Yield")]
    [InlineData("forage_drops_off", "Foraging Disabled")]
    [InlineData("crop_growth_up", "+25% Crop Growth")]
    [InlineData("crop_growth_down", "-25% Crop Growth")]
    [InlineData("fish_bite_up", "+30% Fish Bite Rate")]
    [InlineData("mine_drops_up", "+30% Mine Drops")]
    [InlineData("mine_drops_off", "Mine Drops Disabled")]
    [InlineData("all_drops_up", "+10% All Drops")]
    [InlineData("all_sell_prices_down", "-50% All Sell Prices")]
    [InlineData("shop_discount", "-15% Shop Prices")]
    [InlineData("stamina_drain_up", "+30% Stamina Drain")]
    public void DisplayNameFor_maps_known_ids(string id, string expected)
        => Assert.Equal(expected, ThemeModifiers.DisplayNameFor(id));

    [Fact]
    public void DisplayNameFor_falls_through_to_raw_id_when_unknown()
        => Assert.Equal("not-a-real-id", ThemeModifiers.DisplayNameFor("not-a-real-id"));
}
