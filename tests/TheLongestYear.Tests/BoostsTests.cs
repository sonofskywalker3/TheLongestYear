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

    [Fact]
    public void StateOf_agrees_with_TryBuy_and_mutates_nothing()
    {
        // Winter: Year-Two Seeds is never available.
        var winterMeta = new MetaState { JunimoPoints = 1000 };
        var winterRun = new RunState();
        Assert.Equal(BoostPurchase.Result.NotAvailable, BoostPurchase.StateOf(winterMeta, winterRun, BoostId.YearTwoSeeds, 14));
        Assert.Equal(BoostPurchase.Result.NotAvailable, BoostPurchase.TryBuy(winterMeta, winterRun, BoostId.YearTwoSeeds, 14));

        // Too poor.
        var poorMeta = new MetaState { JunimoPoints = 10 };
        var poorRun = new RunState();
        Assert.Equal(BoostPurchase.Result.NotEnoughJp, BoostPurchase.StateOf(poorMeta, poorRun, BoostId.SneakPeek, 10));
        Assert.Equal(BoostPurchase.Result.NotEnoughJp, BoostPurchase.TryBuy(poorMeta, poorRun, BoostId.SneakPeek, 10));

        // Affordable: StateOf says Success and leaves both objects untouched, then TryBuy spends.
        var meta = new MetaState { JunimoPoints = 200 };
        var run = new RunState();
        Assert.Equal(BoostPurchase.Result.Success, BoostPurchase.StateOf(meta, run, BoostId.SneakPeek, 10));
        Assert.Equal(200, meta.JunimoPoints);
        Assert.Equal(-1, run.SneakPeekSeason);
        Assert.Equal(BoostPurchase.Result.Success, BoostPurchase.TryBuy(meta, run, BoostId.SneakPeek, 10));
        Assert.Equal(100, meta.JunimoPoints);

        // Already active, now that it is bought.
        Assert.Equal(BoostPurchase.Result.AlreadyActive, BoostPurchase.StateOf(meta, run, BoostId.SneakPeek, 10));
        Assert.Equal(BoostPurchase.Result.AlreadyActive, BoostPurchase.TryBuy(meta, run, BoostId.SneakPeek, 10));
        Assert.Equal(100, meta.JunimoPoints);
    }

    [Fact]
    public void Sneak_peek_is_inactive_when_nothing_was_bought()
    {
        // -1 is "not bought this run": no season may match it.
        var run = new RunState();
        Assert.Equal(-1, run.SneakPeekSeason);
        foreach (Season season in new[] { Season.Spring, Season.Summer, Season.Fall, Season.Winter })
            Assert.False(BoostState.SneakPeekActive(run, season));
    }

    [Fact]
    public void Boost_run_state_round_trips_through_json()
    {
        var original = new RunState { YearTwoSeedsWeek = 6, SneakPeekSeason = 2 };
        string json = System.Text.Json.JsonSerializer.Serialize(original);
        RunState restored = System.Text.Json.JsonSerializer.Deserialize<RunState>(json)!;
        Assert.Equal(6, restored.YearTwoSeedsWeek);
        Assert.Equal(2, restored.SneakPeekSeason);

        RunState fresh = System.Text.Json.JsonSerializer.Deserialize<RunState>("{}")!;
        Assert.Equal(-1, fresh.YearTwoSeedsWeek);
        Assert.Equal(-1, fresh.SneakPeekSeason);
    }

    [Theory]
    [InlineData(Season.Spring, "476")]
    [InlineData(Season.Summer, "485")]
    [InlineData(Season.Fall, "489")]
    [InlineData(Season.Winter, null)]
    public void Seed_per_season(Season season, string? id) => Assert.Equal(id, YearTwoSeeds.SeedIdFor(season));
}
