using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>
/// Nijah, Nexus, 2026-10-27: on normal time there is not enough of Spirit's Eve to buy from the
/// shop and finish the maze. The clock there runs at a configurable fraction of normal speed;
/// every other festival keeps the normal clock.
/// </summary>
public class FestivalClockTests
{
    private const int VanillaMsPerGameMinute = 700;

    [Fact]
    public void Spirits_eve_is_slowed()
    {
        Assert.True(FestivalClock.IsSlowed("festival_fall27"));
    }

    [Theory]
    [InlineData("festival_summer28")] // Moonlight Jellies: the jellies are a scripted main event, already frozen
    [InlineData("festival_spring13")] // Egg Festival
    [InlineData("festival_spring24")] // Flower Dance
    [InlineData("festival_summer11")] // Luau
    [InlineData("festival_fall16")]   // Stardew Valley Fair
    [InlineData("festival_winter8")]  // Festival of Ice
    [InlineData("festival_winter25")] // Feast of the Winter Star
    public void Other_festivals_keep_normal_time(string eventId)
    {
        Assert.False(FestivalClock.IsSlowed(eventId));
        Assert.Equal(0, FestivalClock.ExtraMillisecondsFor(eventId, 0.5, VanillaMsPerGameMinute));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("fall27")]            // no festival_ prefix: not a festival event id
    [InlineData("festival_fall27x")]
    [InlineData("NightMarket")]       // passive festival, never a festival event
    public void Non_matching_ids_are_not_slowed(string? eventId)
    {
        Assert.False(FestivalClock.IsSlowed(eventId));
    }

    [Theory]
    [InlineData(0.5, 700)]   // half speed: each game minute takes twice as long
    [InlineData(1.0, 0)]     // normal: today's behaviour
    [InlineData(0.25, 2100)] // quarter speed: four times as long
    [InlineData(0.8, 175)]
    public void Extra_milliseconds_stretch_the_game_minute(double speed, int expectedExtra)
    {
        Assert.Equal(expectedExtra, FestivalClock.ExtraMillisecondsPerGameMinute(speed, VanillaMsPerGameMinute));
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-1.0)]
    [InlineData(0.1)]
    [InlineData(double.NaN)]
    public void Too_slow_or_broken_settings_clamp_to_the_minimum(double speed)
    {
        Assert.Equal(FestivalClock.MinSpeed, FestivalClock.ClampSpeed(speed));
        Assert.Equal(2100, FestivalClock.ExtraMillisecondsPerGameMinute(speed, VanillaMsPerGameMinute));
    }

    [Theory]
    [InlineData(1.5)]
    [InlineData(10.0)]
    public void Settings_above_normal_never_speed_the_clock_up(double speed)
    {
        Assert.Equal(FestivalClock.NormalSpeed, FestivalClock.ClampSpeed(speed));
        Assert.Equal(0, FestivalClock.ExtraMillisecondsPerGameMinute(speed, VanillaMsPerGameMinute));
    }

    [Fact]
    public void Half_speed_doubles_the_real_time_of_each_ten_minute_advance()
    {
        int normal = FestivalClock.RealMillisecondsPerTenMinutes(VanillaMsPerGameMinute, 0);
        int extra = FestivalClock.ExtraMillisecondsFor("festival_fall27", 0.5, VanillaMsPerGameMinute);
        int slowed = FestivalClock.RealMillisecondsPerTenMinutes(VanillaMsPerGameMinute, extra);

        Assert.Equal(7000, normal);
        Assert.Equal(14000, slowed);
    }

    [Fact]
    public void Spirits_eve_window_doubles_at_half_speed()
    {
        // 22:00 to 23:50 is eleven 10-minute advances.
        const int advances = 11;
        int extra = FestivalClock.ExtraMillisecondsFor("festival_fall27", 0.5, VanillaMsPerGameMinute);

        int normalMs = advances * FestivalClock.RealMillisecondsPerTenMinutes(VanillaMsPerGameMinute, 0);
        int slowedMs = advances * FestivalClock.RealMillisecondsPerTenMinutes(VanillaMsPerGameMinute, extra);

        Assert.Equal(77_000, normalMs);
        Assert.Equal(154_000, slowedMs);
    }

    [Fact]
    public void A_zero_minute_length_is_left_alone()
    {
        // Game1.realMilliSecondsPerGameMinute before the game initialises it.
        Assert.Equal(0, FestivalClock.ExtraMillisecondsPerGameMinute(0.5, 0));
    }

    [Fact]
    public void Default_config_is_half_speed()
    {
        Assert.Equal(0.5, FestivalClock.DefaultSpeed);
        Assert.Equal(FestivalClock.DefaultSpeed, new GameplayConfig().FestivalClockSlowdown);
    }

    [Fact]
    public void Spirits_eve_temporary_map_gets_the_slowed_value()
    {
        Assert.Equal(700, FestivalClock.TargetExtraMs(
            featureOn: true, runActive: true, "festival_fall27", isTemporary: true, 0.5, VanillaMsPerGameMinute));
    }

    [Fact]
    public void Entry_tick_on_the_real_town_is_never_written()
    {
        // Vanilla attaches festival_fall27 to the persistent Town first and swaps to the "Temp"
        // map a tick later. Writing that tick would slow the real Town for the whole session.
        Assert.Null(FestivalClock.TargetExtraMs(
            featureOn: true, runActive: true, "festival_fall27", isTemporary: false, 0.5, VanillaMsPerGameMinute));
    }

    [Fact]
    public void Town_before_temp_then_temp_writes_only_the_temp_map()
    {
        int? entryTick = FestivalClock.TargetExtraMs(true, true, "festival_fall27", isTemporary: false, 0.5, VanillaMsPerGameMinute);
        int? nextTick = FestivalClock.TargetExtraMs(true, true, "festival_fall27", isTemporary: true, 0.5, VanillaMsPerGameMinute);
        Assert.Null(entryTick);
        Assert.Equal(700, nextTick);
    }

    [Theory]
    [InlineData(false, true, "festival_fall27", true)]   // festivals-cost-hours off
    [InlineData(true, false, "festival_fall27", true)]   // not a TLY run
    [InlineData(true, true, null, true)]                 // no festival up
    [InlineData(true, true, "festival_summer28", true)]  // Moonlight Jellies: normal time
    [InlineData(true, true, "festival_fall16", true)]    // the Fair: normal time
    [InlineData(true, true, null, false)]                // ordinary day in the real Town
    public void Nothing_to_slow_gives_no_write(bool featureOn, bool runActive, string? eventId, bool isTemporary)
    {
        Assert.Null(FestivalClock.TargetExtraMs(featureOn, runActive, eventId, isTemporary, 0.5, VanillaMsPerGameMinute));
    }

    [Fact]
    public void Normal_speed_writes_zero_on_the_festival_map()
    {
        Assert.Equal(0, FestivalClock.TargetExtraMs(true, true, "festival_fall27", true, 1.0, VanillaMsPerGameMinute));
    }
}
