using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class BoostsTests
{
    [Fact]
    public void Buying_year_two_seeds_marks_this_week_and_spends_75()
    {
        var meta = new MetaState { JunimoPoints = 100 };
        var run = new RunState();
        Assert.Equal(BoostPurchase.Result.Success, BoostPurchase.TryBuy(meta, run, BoostId.YearTwoSeeds, weekOfYear: 6));
        Assert.Equal(25, meta.JunimoPoints);
        Assert.True(BoostState.YearTwoSeedsActive(run, 6));
        Assert.False(BoostState.YearTwoSeedsActive(run, 7));
        Assert.Equal(BoostPurchase.Result.AlreadyActive, BoostPurchase.TryBuy(meta, run, BoostId.YearTwoSeeds, 6));
    }

    [Fact]
    public void Year_two_seeds_cannot_be_bought_in_winter()
    {
        var meta = new MetaState { JunimoPoints = 1000 };
        Assert.Equal(BoostPurchase.Result.NotAvailable, BoostPurchase.TryBuy(meta, new RunState(), BoostId.YearTwoSeeds, weekOfYear: 14));
    }

    [Fact]
    public void Sneak_peek_lasts_the_season_and_costs_100()
    {
        var meta = new MetaState { JunimoPoints = 100 };
        var run = new RunState();
        Assert.Equal(BoostPurchase.Result.Success, BoostPurchase.TryBuy(meta, run, BoostId.SneakPeek, weekOfYear: 10));
        Assert.True(BoostState.SneakPeekActive(run, Season.Fall));
        Assert.False(BoostState.SneakPeekActive(run, Season.Winter));
        Assert.Equal(0, meta.JunimoPoints);
    }

    [Fact]
    public void A_new_run_clears_boosts()
    {
        var run = new RunState { YearTwoSeedsWeek = 3, SneakPeekSeason = 2 };
        run.BeginNewRun(1);
        Assert.Equal(-1, run.YearTwoSeedsWeek);
        Assert.Equal(-1, run.SneakPeekSeason);
    }

    [Theory]
    [InlineData(Season.Spring, "476")]
    [InlineData(Season.Summer, "485")]
    [InlineData(Season.Fall, "489")]
    [InlineData(Season.Winter, null)]
    public void Seed_per_season(Season season, string? id) => Assert.Equal(id, YearTwoSeeds.SeedIdFor(season));
}
