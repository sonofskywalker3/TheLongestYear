using System.Text.Json;
using TheLongestYear.Core;
using TheLongestYear.Core.Day28;

namespace TheLongestYear.Tests;

/// <summary>Voluntary restart at the Junimo Shrine (spec 2026-09-24-voluntary-restart-design):
/// when the button shows, how the won-run flag is cleared, and which branches rewind.</summary>
public class VoluntaryRestartTests
{
    private static readonly RestartSituation OrdinaryDay = new(
        DayOfMonth: 12, EventUp: false, ResetRunning: false);

    [Fact]
    public void Shown_on_an_ordinary_day()
    {
        Assert.Equal(RestartBlock.None, VoluntaryRestart.BlockedBy(OrdinaryDay));
        Assert.True(VoluntaryRestart.IsOffered(OrdinaryDay));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(27)]
    public void Shown_on_the_first_day_and_the_day_before_the_season_ends(int day)
        => Assert.True(VoluntaryRestart.IsOffered(OrdinaryDay with { DayOfMonth = day }));

    [Fact]
    public void Hidden_on_day_28_because_that_night_belongs_to_the_real_gate()
        => Assert.Equal(RestartBlock.SeasonEndDay, VoluntaryRestart.BlockedBy(OrdinaryDay with { DayOfMonth = 28 }));

    [Fact]
    public void Hidden_during_an_event_or_cutscene()
        => Assert.Equal(RestartBlock.EventUp, VoluntaryRestart.BlockedBy(OrdinaryDay with { EventUp = true }));

    [Fact]
    public void Hidden_while_another_reset_chain_is_running()
        => Assert.Equal(RestartBlock.ResetRunning, VoluntaryRestart.BlockedBy(OrdinaryDay with { ResetRunning = true }));

    [Fact]
    public void A_running_reset_is_reported_before_every_other_reason()
    {
        RestartSituation all = new(DayOfMonth: 28, EventUp: true, ResetRunning: true);
        Assert.Equal(RestartBlock.ResetRunning, VoluntaryRestart.BlockedBy(all));
    }

    [Fact]
    public void Restart_after_keep_playing_clears_the_won_run_flag()
    {
        var meta = new MetaState { VictoryAcknowledged = true };
        Assert.True(VoluntaryRestart.ClearWonRun(meta));
        Assert.False(meta.VictoryAcknowledged);
    }

    [Fact]
    public void Restart_before_any_win_leaves_the_flag_alone()
    {
        var meta = new MetaState { VictoryAcknowledged = false };
        Assert.False(VoluntaryRestart.ClearWonRun(meta));
        Assert.False(meta.VictoryAcknowledged);
    }

    [Theory]
    [InlineData(Day28Branch.Fail, true)]
    [InlineData(Day28Branch.Restart, true)]
    [InlineData(Day28Branch.None, false)]
    [InlineData(Day28Branch.Continue, false)]
    [InlineData(Day28Branch.Win, false)]
    public void Fail_and_restart_are_the_rewind_branches(Day28Branch branch, bool rewinds)
        => Assert.Equal(rewinds, VoluntaryRestart.IsRewind(branch));

    // PendingDay28 is persisted in the night save; appending Restart must not renumber the
    // values old saves already hold.
    [Fact]
    public void Existing_branch_values_keep_their_numbers()
    {
        Assert.Equal(0, (int)Day28Branch.None);
        Assert.Equal(1, (int)Day28Branch.Fail);
        Assert.Equal(2, (int)Day28Branch.Continue);
        Assert.Equal(3, (int)Day28Branch.Win);
        Assert.Equal(4, (int)Day28Branch.Restart);
    }

    [Fact]
    public void A_pending_restart_survives_a_save_round_trip()
    {
        var run = new RunState { PendingDay28 = Day28Branch.Restart };
        RunState back = JsonSerializer.Deserialize<RunState>(JsonSerializer.Serialize(run))!;
        Assert.Equal(Day28Branch.Restart, back.PendingDay28);
    }

    // A quit after the restart night's save must replay the chain, not roll the month.
    [Fact]
    public void A_pending_restart_blocks_the_load_time_month_rollover()
    {
        var run = new RunState { Season = Season.Spring, DayOfMonth = 27, PendingDay28 = Day28Branch.Restart };
        Assert.False(run.OwesMonthRolloverOnLoad(Season.Summer));
    }
}
