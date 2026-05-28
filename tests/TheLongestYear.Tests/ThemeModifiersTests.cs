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
    [InlineData("forage_yield_up",      "25% chance to find an extra foraged item")]
    [InlineData("forage_off",           "All foraging items removed")]
    [InlineData("crop_growth_up",       "Crops grow 25% faster")]
    [InlineData("crop_growth_down",     "Crops grow 25% slower")]
    [InlineData("fish_bite_up",         "Fish bite 30% sooner")]
    [InlineData("fish_bite_down",       "Fish bite 30% slower")]
    [InlineData("mine_drops_up",        "30% chance for mined resources to drop +1")]
    [InlineData("mines_closed",         "Mine descent blocked all week")]
    [InlineData("all_drops_up",         "10% chance for any drop to be +1")]
    [InlineData("all_sell_prices_down", "All sell prices cut in half")]
    [InlineData("forage_drops_off",     "Foraging disabled (legacy)")]
    [InlineData("mine_drops_off",       "Mine drops disabled (legacy)")]
    [InlineData("shop_discount",        "Shop prices 15% lower")]
    [InlineData("stamina_drain_up",     "Tools drain 30% more stamina")]
    public void DisplayNameFor_maps_known_ids(string id, string expected)
        => Assert.Equal(expected, ThemeModifiers.DisplayNameFor(id));

    [Theory]
    [InlineData(Theme.Foraging, "forage_yield_up", "mines_closed")]
    [InlineData(Theme.Farming,  "crop_growth_up",  "fish_bite_down")]
    [InlineData(Theme.Fishing,  "fish_bite_up",    "crop_growth_down")]
    [InlineData(Theme.Mining,   "mine_drops_up",   "forage_off")]
    [InlineData(Theme.Mixed,    "all_drops_up",    "all_sell_prices_down")]
    public void For_returns_correct_signed_off_ids(Theme theme, string expectedBonus, string expectedLiability)
    {
        var (bonus, liability) = ThemeModifiers.For(theme);
        Assert.Equal(expectedBonus, bonus);
        Assert.Equal(expectedLiability, liability);
    }

    [Fact]
    public void DisplayNameFor_falls_through_to_raw_id_when_unknown()
        => Assert.Equal("not-a-real-id", ThemeModifiers.DisplayNameFor("not-a-real-id"));
}
