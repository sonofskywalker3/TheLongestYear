using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class BoostsTests
{
    private static MetaState Meta(long jp = 1000) => new() { JunimoPoints = jp };

    [Fact]
    public void Catalog_has_the_roster_in_spec_order_with_spec_prices()
    {
        string[] ids = BoostCatalog.All.Select(b => b.Id.ToString()).ToArray();
        Assert.Equal(new[]
        {
            "RainDance", "StormCall", "FortunesFavor", "SecondWind",
            "Overgrowth", "FeedingFrenzy", "GrowthSpurt", "RichVeins", "Windfall", "QuickFeet", "YearTwoSeeds",
            "Haggler", "FastFriends", "IronLungs", "SneakPeek",
            "CrashCourse", "ElevatorPass",
        }, ids);
        Assert.Equal(25, BoostCatalog.Get(BoostId.RainDance).Cost);
        Assert.Equal(150, BoostCatalog.Get(BoostId.FastFriends).Cost);
        Assert.Equal("forage_yield_up", BoostCatalog.Get(BoostId.Overgrowth).ModifierId);
        Assert.Equal(BoostDuration.Loop, BoostCatalog.Get(BoostId.ElevatorPass).Duration);
    }

    [Theory]
    [InlineData(BoostDuration.Instant, 10, 11)]
    [InlineData(BoostDuration.Week, 10, 14)]
    [InlineData(BoostDuration.Season, 10, 28)]
    [InlineData(BoostDuration.Loop, 10, 112)]
    public void Expiry_by_duration_class(BoostDuration d, int day, int expected)
        => Assert.Equal(expected, BoostExpiry.LastDayFor(d, day));

    [Fact]
    public void Second_wind_expires_the_same_day()
    {
        var run = new RunState();
        Assert.Equal(BoostPurchase.Result.Success, BoostPurchase.TryBuy(Meta(), run, BoostId.SecondWind, BoostContext.Simple(5)));
        Assert.Equal(5, run.ActiveBoosts.Single().ExpiresAfterDay);
    }

    [Fact]
    public void Buying_a_week_row_spends_jp_and_is_active_through_the_week()
    {
        var meta = Meta(100); var run = new RunState();
        Assert.Equal(BoostPurchase.Result.Success, BoostPurchase.TryBuy(meta, run, BoostId.Overgrowth, BoostContext.Simple(9)));
        Assert.Equal(50, meta.JunimoPoints);
        Assert.True(BoostState.IsActive(run, BoostId.Overgrowth, 14));
        Assert.False(BoostState.IsActive(run, BoostId.Overgrowth, 15));
        Assert.Equal(BoostPurchase.Result.AlreadyActive, BoostPurchase.TryBuy(meta, run, BoostId.Overgrowth, BoostContext.Simple(10)));
        Assert.Equal(BoostPurchase.Result.Success, BoostPurchase.TryBuy(meta, run, BoostId.Overgrowth, BoostContext.Simple(15)));
    }

    [Fact]
    public void A_reuse_row_collides_with_another_boost_on_the_same_modifier_only()
    {
        var run = new RunState();
        Assert.Equal(BoostPurchase.Result.Success, BoostPurchase.TryBuy(Meta(), run, BoostId.Windfall, BoostContext.Simple(3)));
        // Windfall binds all_drops_up; Rich Veins binds mine_drops_up: no collision.
        Assert.Equal(BoostPurchase.Result.Success, BoostPurchase.TryBuy(Meta(), run, BoostId.RichVeins, BoostContext.Simple(3)));
        Assert.Equal(new[] { "all_drops_up", "mine_drops_up" }, BoostState.ActiveModifierIds(run, 3).OrderBy(s => s));
    }

    [Fact]
    public void Not_enough_jp_is_reported_and_spends_nothing()
    {
        var meta = Meta(10); var run = new RunState();
        Assert.Equal(BoostPurchase.Result.NotEnoughJp, BoostPurchase.TryBuy(meta, run, BoostId.Windfall, BoostContext.Simple(1)));
        Assert.Equal(10, meta.JunimoPoints);
        Assert.Empty(run.ActiveBoosts);
    }

    [Fact]
    public void Weather_rows_refuse_in_winter_and_before_a_festival()
    {
        var run = new RunState();
        Assert.Equal(BoostPurchase.Result.NotAvailable, BoostPurchase.TryBuy(Meta(), run, BoostId.RainDance, BoostContext.Simple(90)));   // Winter
        var festival = new BoostContext(12, TomorrowIsFestival: true, new[] { 0, 0, 0, 0, 0 }, 0);
        Assert.Equal(BoostPurchase.Result.NotAvailable, BoostPurchase.TryBuy(Meta(), run, BoostId.StormCall, festival));
        Assert.Equal(BoostPurchase.Result.Success, BoostPurchase.TryBuy(Meta(), run, BoostId.RainDance, BoostContext.Simple(12)));
        Assert.Equal(13, run.WeatherOverrideDay);
        Assert.Equal("Rain", run.WeatherOverride);
    }

    [Fact]
    public void Storm_call_after_rain_dance_replaces_the_override_and_expires_the_first_entry()
    {
        var run = new RunState();
        BoostPurchase.TryBuy(Meta(), run, BoostId.RainDance, BoostContext.Simple(12));
        Assert.Equal(BoostPurchase.Result.Success, BoostPurchase.TryBuy(Meta(), run, BoostId.StormCall, BoostContext.Simple(12)));
        Assert.Equal("Storm", run.WeatherOverride);
        Assert.Single(run.ActiveBoosts, b => b.IsActiveOn(13));
        Assert.Equal(BoostPurchase.Result.AlreadyActive, BoostPurchase.TryBuy(Meta(), run, BoostId.StormCall, BoostContext.Simple(12)));
    }

    [Fact]
    public void Year_two_seeds_cannot_be_bought_in_winter()
        => Assert.Equal(BoostPurchase.Result.NotAvailable,
            BoostPurchase.TryBuy(Meta(), new RunState(), BoostId.YearTwoSeeds, BoostContext.Simple(100)));

    [Fact]
    public void Prune_drops_only_entries_that_ended_before_today()
    {
        var run = new RunState();
        BoostPurchase.TryBuy(Meta(), run, BoostId.Overgrowth, BoostContext.Simple(3));   // through day 7
        BoostPurchase.TryBuy(Meta(), run, BoostId.Haggler, BoostContext.Simple(3));      // through day 28
        Assert.Equal(0, BoostState.Prune(run, 7));
        Assert.Equal(1, BoostState.Prune(run, 8));
        Assert.Equal("Haggler", run.ActiveBoosts.Single().Id);
    }

    [Fact]
    public void Legacy_flags_migrate_once_into_the_list()
    {
        var run = new RunState { YearTwoSeedsWeek = 2, SneakPeekSeason = 0 };
        BoostState.MigrateLegacy(run, dayOfYear: 10);
        Assert.True(BoostState.YearTwoSeedsActive(run, 10));
        Assert.False(BoostState.YearTwoSeedsActive(run, 15));
        Assert.True(BoostState.SneakPeekActive(run, 28));
        Assert.False(BoostState.SneakPeekActive(run, 29));
        Assert.Equal(-1, run.YearTwoSeedsWeek);
        Assert.Equal(-1, run.SneakPeekSeason);
    }

    [Fact]
    public void A_new_run_clears_everything()
    {
        var run = new RunState();
        BoostPurchase.TryBuy(Meta(), run, BoostId.Haggler, BoostContext.Simple(3));
        BoostPurchase.TryBuy(Meta(), run, BoostId.RainDance, BoostContext.Simple(3));
        run.BeginNewRun(1);
        Assert.Empty(run.ActiveBoosts);
        Assert.Equal(0, run.SkillLevelsBoughtTotal);
        Assert.Equal(-1, run.WeatherOverrideDay);
    }

    [Fact]
    public void StateOf_agrees_with_TryBuy_and_mutates_nothing()
    {
        var meta = Meta(100); var run = new RunState(); var ctx = BoostContext.Simple(3);
        Assert.Equal(BoostPurchase.Result.Success, BoostPurchase.StateOf(meta, run, BoostId.RichVeins, ctx));
        Assert.Equal(100, meta.JunimoPoints);
        Assert.Empty(run.ActiveBoosts);
    }

    [Fact]
    public void Run_state_with_boosts_round_trips_through_json()
    {
        var run = new RunState();
        BoostPurchase.TryBuy(Meta(), run, BoostId.IronLungs, BoostContext.Simple(40));
        string json = System.Text.Json.JsonSerializer.Serialize(run);
        RunState back = System.Text.Json.JsonSerializer.Deserialize<RunState>(json)!;
        Assert.Equal(56, back.ActiveBoosts.Single().ExpiresAfterDay);
        Assert.Empty(System.Text.Json.JsonSerializer.Deserialize<RunState>("{}")!.ActiveBoosts);
    }

    [Theory]
    [InlineData(Season.Spring, "476")]
    [InlineData(Season.Summer, "485")]
    [InlineData(Season.Fall, "489")]
    [InlineData(Season.Winter, null)]
    public void Seed_per_season(Season season, string? expected) => Assert.Equal(expected, YearTwoSeeds.SeedIdFor(season));
}
