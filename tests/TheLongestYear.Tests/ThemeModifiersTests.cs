using System.Linq;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

[Collection("i18n")]
public class ThemeModifiersTests
{
    [Theory]
    [InlineData(Theme.Foraging)]
    [InlineData(Theme.Farming)]
    [InlineData(Theme.Fishing)]
    [InlineData(Theme.Mining)]
    [InlineData(Theme.Mixed)]
    [InlineData(Theme.Spelunking)]
    [InlineData(Theme.Artisan)]
    [InlineData(Theme.Kitchen)]
    public void Every_theme_has_a_distinct_nonempty_bonus_and_liability(Theme theme)
    {
        var (bonus, liability) = ThemeModifiers.For(theme);
        Assert.False(string.IsNullOrWhiteSpace(bonus));
        Assert.False(string.IsNullOrWhiteSpace(liability));
        Assert.NotEqual(bonus, liability);
    }

    [Theory]
    [InlineData("forage_yield_up",      "20% chance to find an extra foraged item")]
    [InlineData("forage_off",           "All foraging items removed")]
    [InlineData("crop_growth_up",       "20% chance per crop per day to grow an extra day")]
    [InlineData("crop_growth_down",     "20% chance per crop per day to grow nothing")]
    [InlineData("fish_bite_up",         "Fish bite 30% sooner")]
    [InlineData("fish_bite_down",       "Fish bite 30% slower")]
    [InlineData("mine_drops_up",        "20% chance for mined resources to drop +1")]
    [InlineData("mines_closed",         "Mine entrance closed all week")]
    [InlineData("all_drops_up",         "10% chance for any drop to be +1")]
    [InlineData("all_sell_prices_down", "All sell prices cut in half")]
    [InlineData("monster_drops_double", "10% chance a slain monster drops everything twice")]
    [InlineData("machines_slow",        "Machines run 25% slower")]
    [InlineData("machines_fast",        "Machines finish 25% sooner")]
    [InlineData("cooked_food_weak",     "Cooked food restores half its energy and health and gives no buffs")]
    [InlineData("animal_double_product", "20% chance an animal gives a second product each day")]
    [InlineData("monster_damage_up",    "Monsters deal 25% more damage")]
    public void DisplayNameFor_maps_known_ids(string id, string expected)
        => Assert.Equal(expected, ThemeModifiers.DisplayNameFor(id));

    [Theory]
    [InlineData(Theme.Foraging, "forage_yield_up", "mines_closed")]
    [InlineData(Theme.Farming,  "crop_growth_up",  "fish_bite_down")]
    [InlineData(Theme.Fishing,  "fish_bite_up",    "crop_growth_down")]
    [InlineData(Theme.Mining,   "mine_drops_up",   "forage_off")]
    [InlineData(Theme.Mixed,    "all_drops_up",    "all_sell_prices_down")]
    [InlineData(Theme.Spelunking, "monster_drops_double", "machines_slow")]
    [InlineData(Theme.Artisan,    "machines_fast",        "cooked_food_weak")]
    [InlineData(Theme.Kitchen,    "animal_double_product", "monster_damage_up")]
    public void For_returns_correct_signed_off_ids(Theme theme, string expectedBonus, string expectedLiability)
    {
        var (bonus, liability) = ThemeModifiers.For(theme);
        Assert.Equal(expectedBonus, bonus);
        Assert.Equal(expectedLiability, liability);
    }

    [Fact]
    public void Every_liability_lands_on_a_different_activity_than_its_bonus_and_each_new_activity_is_bitten_once()
    {
        var pairs = new[] { Theme.Spelunking, Theme.Artisan, Theme.Kitchen }.Select(ThemeModifiers.For).ToList();
        Assert.Equal(new[] { "machines_slow", "cooked_food_weak", "monster_damage_up" }, pairs.Select(p => p.LiabilityId));
        Assert.Equal(3, pairs.Select(p => p.LiabilityId).Distinct().Count());
        Assert.Equal(3, pairs.Select(p => p.BonusId).Distinct().Count());
    }

    [Fact]
    public void DisplayNameFor_falls_through_to_raw_id_when_unknown()
        => Assert.Equal("not-a-real-id", ThemeModifiers.DisplayNameFor("not-a-real-id"));
}
