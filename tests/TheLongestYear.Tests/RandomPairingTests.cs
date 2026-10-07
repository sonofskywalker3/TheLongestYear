using System;
using System.Linq;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class RandomPairingTests
{
    [Fact]
    public void There_are_eight_drawbacks()
        => Assert.Equal(8, RandomPairing.AllLiabilities.Distinct().Count());

    [Theory]
    [InlineData(Theme.Foraging, "forage_off")]
    [InlineData(Theme.Farming, "crop_growth_down")]
    [InlineData(Theme.Fishing, "fish_bite_down")]
    [InlineData(Theme.Mining, "mines_closed")]
    [InlineData(Theme.Spelunking, "mines_closed")]
    [InlineData(Theme.Artisan, "machines_slow")]
    public void A_theme_never_draws_the_drawback_that_blocks_its_own_goals(Theme t, string blocked)
    {
        for (int seed = 0; seed < 500; seed++)
            Assert.NotEqual(blocked, RandomPairing.LiabilityFor(seed, 4, t, random: true));
    }

    [Fact]
    public void Random_pairings_reach_more_than_one_drawback()
        => Assert.True(Enumerable.Range(0, 200).Select(s => RandomPairing.LiabilityFor(s, 4, Theme.Kitchen, true)).Distinct().Count() > 3);

    [Fact]
    public void Off_returns_the_themes_own_drawback()
    {
        foreach (Theme t in Enum.GetValues<Theme>())
            Assert.Equal(ThemeModifiers.For(t).LiabilityId, RandomPairing.LiabilityFor(99, 4, t, random: false));
    }

    [Fact]
    public void Same_seed_week_and_theme_give_the_same_drawback()
        => Assert.Equal(RandomPairing.LiabilityFor(5, 7, Theme.Mixed, true), RandomPairing.LiabilityFor(5, 7, Theme.Mixed, true));

    [Fact]
    public void Effects_use_the_stored_drawback_and_fall_back_to_the_theme()
    {
        var run = new RunState();
        Assert.Equal(ThemeModifiers.For(Theme.Fishing), RandomPairing.EffectsFor(run, Theme.Fishing));
        run.CurrentLiabilityId = "machines_slow";
        Assert.Equal((ThemeModifiers.For(Theme.Fishing).BonusId, "machines_slow"), RandomPairing.EffectsFor(run, Theme.Fishing));
    }

    [Fact]
    public void Second_effects_use_the_second_stored_drawback_not_the_first()
    {
        var run = new RunState { CurrentLiabilityId = "mines_closed" };
        Assert.Equal(ThemeModifiers.For(Theme.Farming), RandomPairing.SecondEffectsFor(run, Theme.Farming));
        run.SecondLiabilityId = "monster_damage_up";
        Assert.Equal((ThemeModifiers.For(Theme.Farming).BonusId, "monster_damage_up"), RandomPairing.SecondEffectsFor(run, Theme.Farming));
    }

    [Theory]
    [InlineData(Theme.Mixed, Theme.Fishing, "fish_bite_down")]
    [InlineData(Theme.Kitchen, Theme.Farming, "crop_growth_down")]
    [InlineData(Theme.Fishing, Theme.Mining, "mines_closed")]
    [InlineData(Theme.Fishing, Theme.Spelunking, "mines_closed")]
    [InlineData(Theme.Mixed, Theme.Artisan, "machines_slow")]
    [InlineData(Theme.Artisan, Theme.Foraging, "forage_off")]
    public void On_a_double_week_a_card_never_draws_the_drawback_that_blocks_the_other_card(Theme card, Theme other, string blocked)
    {
        for (int seed = 0; seed < 500; seed++)
            Assert.NotEqual(blocked, RandomPairing.LiabilityFor(seed, 4, card, random: true, otherCard: other));
    }

    [Fact]
    public void On_a_double_week_a_card_still_never_draws_its_own_blocking_drawback()
    {
        for (int seed = 0; seed < 500; seed++)
        {
            string drawn = RandomPairing.LiabilityFor(seed, 4, Theme.Fishing, random: true, otherCard: Theme.Farming);
            Assert.NotEqual("fish_bite_down", drawn);
            Assert.NotEqual("crop_growth_down", drawn);
        }
    }

    [Fact]
    public void A_double_week_card_still_reaches_several_drawbacks()
        => Assert.True(Enumerable.Range(0, 200)
            .Select(s => RandomPairing.LiabilityFor(s, 4, Theme.Mixed, true, otherCard: Theme.Fishing)).Distinct().Count() > 3);

    /// <summary>Draws recorded before the double-week rule existed: a single week (no other card)
    /// must keep drawing exactly these, so no player's week changes.</summary>
    [Theory]
    [InlineData(1, Theme.Mixed, "all_sell_prices_down")]
    [InlineData(2, Theme.Fishing, "all_sell_prices_down")]
    [InlineData(3, Theme.Farming, "fish_bite_down")]
    [InlineData(4, Theme.Kitchen, "fish_bite_down")]
    [InlineData(5, Theme.Artisan, "all_sell_prices_down")]
    [InlineData(6, Theme.Mining, "all_sell_prices_down")]
    public void A_single_week_draw_is_unchanged(int seed, Theme theme, string expected)
    {
        Assert.Equal(expected, RandomPairing.LiabilityFor(seed, 3, theme, true));
        Assert.Equal(expected, RandomPairing.LiabilityFor(seed, 3, theme, true, otherCard: null));
    }

    [Fact]
    public void A_double_week_with_an_other_card_that_blocks_nothing_draws_like_a_single_week()
    {
        for (int seed = 0; seed < 200; seed++)
            Assert.Equal(RandomPairing.LiabilityFor(seed, 4, Theme.Fishing, true),
                RandomPairing.LiabilityFor(seed, 4, Theme.Fishing, true, otherCard: Theme.Kitchen));
    }

    [Fact]
    public void Off_ignores_the_other_card()
    {
        foreach (Theme t in Enum.GetValues<Theme>())
            Assert.Equal(ThemeModifiers.For(t).LiabilityId, RandomPairing.LiabilityFor(99, 4, t, random: false, otherCard: Theme.Fishing));
    }

    [Fact]
    public void An_old_save_has_no_stored_drawback()
        => Assert.Null(System.Text.Json.JsonSerializer.Deserialize<RunState>("{}")!.CurrentLiabilityId);
}
