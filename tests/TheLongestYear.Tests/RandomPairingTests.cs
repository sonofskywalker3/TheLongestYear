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
    public void An_old_save_has_no_stored_drawback()
        => Assert.Null(System.Text.Json.JsonSerializer.Deserialize<RunState>("{}")!.CurrentLiabilityId);
}
