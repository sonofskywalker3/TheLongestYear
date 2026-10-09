using TheLongestYear.Core;
using TheLongestYear.Core.Sabotage;
using Xunit;

namespace TheLongestYear.Tests;

public class WitnessLinesTests
{
    [Fact] public void Crows_are_seen_by_Linus() => Assert.Equal("Linus", WitnessLines.NpcFor(DarknessEvent.CropBlight));
    [Fact] public void The_hall_is_seen_by_Shane() => Assert.Equal("Shane", WitnessLines.NpcFor(DarknessEvent.Reversion));
    [Theory]
    [InlineData(DarknessEvent.ChestBlight)]
    [InlineData(DarknessEvent.Tampering)]
    public void Nobody_sees_the_thief_or_the_cloud(DarknessEvent e) => Assert.Null(WitnessLines.NpcFor(e));

    [Fact] public void The_morning_after_says_last_night()
        => Assert.Equal("dialogue.witness.when-last-night", WitnessLines.WhenKey(sceneDay: 40, today: 41));
    [Theory]
    [InlineData(42)]
    [InlineData(47)]
    public void Any_later_day_says_the_other_night(int today)
        => Assert.Equal("dialogue.witness.when-other-night", WitnessLines.WhenKey(40, today));

    [Theory]
    [InlineData(41, true)]
    [InlineData(47, true)]
    [InlineData(48, false)]
    [InlineData(40, false)]
    public void The_line_is_live_for_seven_days_after_the_scene(int today, bool live)
        => Assert.Equal(live, WitnessLines.IsLive(new WitnessRecord { Npc = "Linus", SceneDayOfYear = 40 }, today));

    // Designer, 2026-10-09: "if they haven't talked to the villager yet, it can be pushed back a
    // day." A morning when he has not been met holds the line; vanilla's introduction plays alone.
    [Fact]
    public void A_morning_before_he_is_met_holds_the_line()
    {
        var r = new WitnessRecord { Npc = "Linus", SceneDayOfYear = 40 };
        Assert.Equal(WitnessMorning.Hold, WitnessLines.Decide(r, 41, notMetYet: true));
        Assert.True(r.HeldForIntroduction);
        Assert.False(r.Said);
    }

    [Fact]
    public void The_morning_after_he_is_met_says_the_line()
    {
        var r = new WitnessRecord { Npc = "Linus", SceneDayOfYear = 40 };
        WitnessLines.Decide(r, 41, notMetYet: true);
        Assert.Equal(WitnessMorning.Say, WitnessLines.Decide(r, 42, notMetYet: false));
    }

    [Fact]
    public void A_met_witness_says_it_the_first_morning()
        => Assert.Equal(WitnessMorning.Say, WitnessLines.Decide(new WitnessRecord { Npc = "Shane", SceneDayOfYear = 40 }, 41, notMetYet: false));

    // Met on the window's last day: the introduction took that day, so the line gets one more.
    [Fact]
    public void A_held_line_gets_the_introduction_day_back()
    {
        var r = new WitnessRecord { Npc = "Linus", SceneDayOfYear = 40 };
        Assert.Equal(WitnessMorning.Hold, WitnessLines.Decide(r, 47, notMetYet: true));
        Assert.Equal(WitnessMorning.Say, WitnessLines.Decide(r, 48, notMetYet: false));
        Assert.True(WitnessLines.IsLive(r, 48));
        Assert.Equal(WitnessMorning.Drop, WitnessLines.Decide(r, 49, notMetYet: false));
    }

    [Fact]
    public void A_line_never_held_ends_after_the_week()
        => Assert.Equal(WitnessMorning.Drop, WitnessLines.Decide(new WitnessRecord { Npc = "Linus", SceneDayOfYear = 40 }, 48, notMetYet: false));

    [Fact]
    public void The_window_is_a_week_plus_the_introduction_day()
    {
        Assert.Equal(7, WitnessLines.WindowFor(new WitnessRecord()));
        Assert.Equal(8, WitnessLines.WindowFor(new WitnessRecord { HeldForIntroduction = true }));
    }

    [Fact]
    public void A_said_line_is_dropped_even_if_he_is_not_met()
        => Assert.Equal(WitnessMorning.Drop, WitnessLines.Decide(new WitnessRecord { Npc = "Linus", SceneDayOfYear = 40, Said = true }, 41, notMetYet: true));

    [Fact]
    public void A_scene_not_yet_past_waits()
    {
        var r = new WitnessRecord { Npc = "Linus", SceneDayOfYear = 40 };
        Assert.Equal(WitnessMorning.Wait, WitnessLines.Decide(r, 40, notMetYet: true));
        Assert.False(r.HeldForIntroduction);
    }

    [Fact]
    public void The_held_mark_round_trips_with_the_run()
    {
        var run = new RunState();
        run.WitnessLines.Add(new WitnessRecord { Npc = "Linus", SceneDayOfYear = 9, HeldForIntroduction = true });
        var back = System.Text.Json.JsonSerializer.Deserialize<RunState>(System.Text.Json.JsonSerializer.Serialize(run));
        Assert.True(back!.WitnessLines[0].HeldForIntroduction);
    }
    [Fact] public void A_line_already_said_is_not_live()
        => Assert.False(WitnessLines.IsLive(new WitnessRecord { Npc = "Linus", SceneDayOfYear = 40, Said = true }, 41));

    [Fact] public void Witness_lines_clear_at_the_loop_reset()
    {
        var run = new RunState();
        run.WitnessLines.Add(new WitnessRecord { Npc = "Linus", SceneDayOfYear = 3 });
        run.BeginNewRun(1);
        Assert.Empty(run.WitnessLines);
    }

    [Fact] public void Witness_lines_round_trip_with_the_run()
    {
        var run = new RunState();
        run.WitnessLines.Add(new WitnessRecord { Npc = "Shane", SceneDayOfYear = 9, Said = true });
        var back = System.Text.Json.JsonSerializer.Deserialize<RunState>(System.Text.Json.JsonSerializer.Serialize(run));
        Assert.Equal("Shane", Assert.Single(back.WitnessLines).Npc);
        Assert.True(back.WitnessLines[0].Said);
    }
}
