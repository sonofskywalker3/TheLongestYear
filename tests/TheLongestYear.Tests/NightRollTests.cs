using System;
using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;
using TheLongestYear.Core.Sabotage;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>Spec 2026-09-15 Part B, sections 2.1, 2.2, 2.6 and 1.5: one roll a night with a
/// decaying weekly chance, one event per strike split evenly with fall-through, the guaranteed
/// Winter tamper, and the once-per-loop unmoderated roll.</summary>
public class NightRollTests
{
    private sealed class FixedRng : Random
    {
        private readonly double _next;
        public FixedRng(double next) => _next = next;
        protected override double Sample() => _next;
        public override double NextDouble() => _next;
        public override int Next(int maxValue) => (int)(_next * maxValue);
    }

    [Theory]
    [InlineData(Season.Spring, 0.0)]
    [InlineData(Season.Summer, 0.25)]
    [InlineData(Season.Fall, 0.35)]
    [InlineData(Season.Winter, 0.35)]
    public void The_chance_starts_each_week_at_the_seasons_value(Season season, double chance)
        => Assert.Equal(chance, NightRoll.ChanceTonight(new RunState(), 7, season));

    [Fact]
    public void Each_strike_drops_the_chance_five_points_until_the_week_resets()
    {
        var run = new RunState();
        Assert.Equal(0.35, NightRoll.ChanceTonight(run, 10, Season.Fall), 3);
        NightRoll.RecordStrike(run, 10, Season.Fall);
        Assert.Equal(0.30, NightRoll.ChanceTonight(run, 10, Season.Fall), 3);
        NightRoll.RecordStrike(run, 10, Season.Fall);
        Assert.Equal(0.25, NightRoll.ChanceTonight(run, 10, Season.Fall), 3);
        Assert.Equal(0.35, NightRoll.ChanceTonight(run, 11, Season.Fall), 3);
    }

    [Fact]
    public void The_chance_never_goes_below_zero()
    {
        var run = new RunState();
        for (int i = 0; i < 10; i++) NightRoll.RecordStrike(run, 5, Season.Summer);
        Assert.Equal(0.0, NightRoll.ChanceTonight(run, 5, Season.Summer));
    }

    [Fact]
    public void Options_grow_by_season()
    {
        Assert.Empty(NightRoll.Options(Season.Spring));
        Assert.Equal(new[] { DarknessEvent.CropBlight, DarknessEvent.ChestBlight }, NightRoll.Options(Season.Summer));
        Assert.Equal(new[] { DarknessEvent.CropBlight, DarknessEvent.ChestBlight, DarknessEvent.Reversion }, NightRoll.Options(Season.Fall));
        Assert.Equal(new[] { DarknessEvent.CropBlight, DarknessEvent.ChestBlight, DarknessEvent.Reversion, DarknessEvent.Tampering }, NightRoll.Options(Season.Winter));
    }

    [Fact]
    public void The_pick_is_even_among_the_options_that_can_act()
    {
        var counts = new Dictionary<DarknessEvent, int>();
        for (int seed = 0; seed < 4000; seed++)
        {
            DarknessEvent? pick = NightRoll.Pick(NightRoll.Options(Season.Winter), e => e != DarknessEvent.Reversion, new Random(seed));
            Assert.NotNull(pick);
            counts[pick.Value] = counts.GetValueOrDefault(pick.Value) + 1;
        }
        Assert.DoesNotContain(DarknessEvent.Reversion, counts.Keys);
        Assert.All(counts.Values, n => Assert.InRange(n, 1150, 1520));   // about a third each
    }

    [Fact]
    public void No_option_that_can_act_means_no_strike()
        => Assert.Null(NightRoll.Pick(NightRoll.Options(Season.Fall), _ => false, new Random(1)));

    [Fact]
    public void The_first_winter_ever_tampers_on_winter_1_and_later_winters_on_a_week_1_night()
    {
        Assert.Equal(1, NightRoll.GuaranteedTamperDay(12345, firstWinterEver: true));
        for (int seed = 0; seed < 50; seed++)
            Assert.InRange(NightRoll.GuaranteedTamperDay(seed, firstWinterEver: false), 1, 7);
        Assert.Equal(NightRoll.GuaranteedTamperDay(99, false), NightRoll.GuaranteedTamperDay(99, false));
    }

    [Fact]
    public void The_guaranteed_night_retries_until_it_lands_and_never_repeats()
    {
        var run = new RunState { Seed = 7 };
        int day = NightRoll.GuaranteedTamperDay(7, false);
        Assert.False(NightRoll.IsGuaranteedTamperNight(run, false, Season.Fall, day));
        if (day > 1) Assert.False(NightRoll.IsGuaranteedTamperNight(run, false, Season.Winter, day - 1));
        Assert.True(NightRoll.IsGuaranteedTamperNight(run, false, Season.Winter, day));
        Assert.True(NightRoll.IsGuaranteedTamperNight(run, false, Season.Winter, 7));    // skipped nights retry
        Assert.False(NightRoll.IsGuaranteedTamperNight(run, false, Season.Winter, 8));   // week 1 only
        run.GuaranteedTamperDone = true;
        Assert.False(NightRoll.IsGuaranteedTamperNight(run, false, Season.Winter, day));
    }

    [Theory]
    [InlineData(DifficultyStep.Easy, 0.01, false)]
    [InlineData(DifficultyStep.Normal, 0.01, false)]
    [InlineData(DifficultyStep.Hard, 0.05, true)]
    [InlineData(DifficultyStep.Hard, 0.15, false)]
    [InlineData(DifficultyStep.Extreme, 0.25, true)]
    [InlineData(DifficultyStep.Extreme, 0.35, false)]
    public void The_unmoderated_roll_by_level(DifficultyStep level, double die, bool fires)
        => Assert.Equal(fires, NightRoll.UnmoderatedFires(level, spent: false, new FixedRng(die)));

    [Fact]
    public void The_unmoderated_roll_is_once_per_loop()
        => Assert.False(NightRoll.UnmoderatedFires(DifficultyStep.Extreme, spent: true, new FixedRng(0.0)));

    [Fact]
    public void The_night_stream_is_fixed_by_seed_and_day()
    {
        Assert.Equal(SabotageSchedule.Rng(5, 40).Next(), SabotageSchedule.Rng(5, 40).Next());
        Assert.NotEqual(SabotageSchedule.Rng(5, 40).Next(), SabotageSchedule.Rng(5, 41).Next());
        Assert.NotEqual(SabotageSchedule.Rng(5, 40).Next(), SabotageSchedule.Rng(5, 40, SabotageKind.Blight).Next());
    }

    [Fact]
    public void A_new_run_forgets_the_darkness_counters()
    {
        var run = new RunState
        {
            DarknessChanceWeek = 9, DarknessChance = 0.1, UnmoderatedReversionSpent = true,
            UnmoderatedTamperSpent = true, GuaranteedTamperDone = true,
        };
        run.BeginNewRun(7);
        Assert.Equal(-1, run.DarknessChanceWeek);
        Assert.Equal(0.0, run.DarknessChance);
        Assert.False(run.UnmoderatedReversionSpent);
        Assert.False(run.UnmoderatedTamperSpent);
        Assert.False(run.GuaranteedTamperDone);
    }
}
