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

    [Fact] public void A_first_meeting_intro_that_cleared_the_line_is_followed_by_it()
        => Assert.True(WitnessLines.FollowsTopic(lineWasQueued: true, lineOnTop: false, topicPushed: true));

    [Theory]
    [InlineData(false, false, true)]  // no line was waiting
    [InlineData(true, true, true)]    // the line is still the next thing he says
    [InlineData(true, false, false)]  // nothing new was pushed
    public void The_line_only_follows_a_dialogue_that_displaced_it(bool queued, bool onTop, bool pushed)
        => Assert.False(WitnessLines.FollowsTopic(queued, onTop, pushed));

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
