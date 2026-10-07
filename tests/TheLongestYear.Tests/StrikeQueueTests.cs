using System;
using TheLongestYear.Core;
using TheLongestYear.Core.Sabotage;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>Designer, 2026-10-07: "Queue it for the next free night." A strike postponed because its
/// scene could not have the night waits in a queue on the run; on the next night it can act it fires
/// instead of the normal roll, as that night's one strike, honouring the kind's caps and the spacing
/// between tampers. Cleared at the loop reset; saves from before the queue load with it empty.</summary>
public class StrikeQueueTests
{
    private static readonly Func<DarknessEvent, bool> Anything = _ => true;

    [Fact]
    public void A_postponed_strike_fires_on_the_next_free_night_and_then_leaves_the_queue()
    {
        var run = new RunState();
        StrikeQueue.Enqueue(run, DarknessEvent.CropBlight);           // tonight's slot was taken
        Assert.Equal(DarknessEvent.CropBlight, StrikeQueue.Tonight(run, Anything));
        Assert.True(StrikeQueue.OnCommitted(run, DarknessEvent.CropBlight)); // its scene staged
        Assert.Null(StrikeQueue.Tonight(run, Anything));
        Assert.False(StrikeQueue.IsQueued(run, DarknessEvent.CropBlight));
    }

    [Fact]
    public void A_night_whose_slot_is_taken_again_keeps_it_queued()
    {
        var run = new RunState();
        StrikeQueue.Enqueue(run, DarknessEvent.Reversion);
        Assert.Equal(DarknessEvent.Reversion, StrikeQueue.Tonight(run, Anything));
        // The wedding has the slot tonight: postponed again, never committed.
        StrikeQueue.Enqueue(run, DarknessEvent.Reversion);
        Assert.Equal(DarknessEvent.Reversion, StrikeQueue.Tonight(run, Anything));
        Assert.Single(run.QueuedStrikes);
    }

    [Fact]
    public void A_queued_kind_that_cannot_act_stays_queued_and_the_night_rolls_normally()
    {
        var run = new RunState();
        StrikeQueue.Enqueue(run, DarknessEvent.CropBlight);
        Assert.Null(StrikeQueue.Tonight(run, e => e != DarknessEvent.CropBlight)); // no crops tonight
        Assert.True(StrikeQueue.IsQueued(run, DarknessEvent.CropBlight));
    }

    [Fact]
    public void The_oldest_queued_kind_that_can_act_fires_first()
    {
        var run = new RunState();
        StrikeQueue.Enqueue(run, DarknessEvent.ChestBlight);
        StrikeQueue.Enqueue(run, DarknessEvent.Reversion);
        Assert.Equal(DarknessEvent.ChestBlight, StrikeQueue.Tonight(run, Anything));
        Assert.Equal(DarknessEvent.Reversion, StrikeQueue.Tonight(run, e => e != DarknessEvent.ChestBlight));
    }

    [Fact]
    public void A_queued_tamper_waits_out_the_spacing_after_the_last_one()
    {
        var run = new RunState { Season = Season.Winter, DayOfMonth = 3 };
        int week = run.WeekOfYear;
        int winter3 = Calendar.DayOfYear((int)Season.Winter, 3);
        // One tamper struck on Winter 3; tonight's was postponed by the bus repair.
        StrikeLedger.Record(run, DarknessEvent.Tampering, week, Season.Winter, winter3);
        StrikeQueue.Enqueue(run, DarknessEvent.Tampering);

        Func<DarknessEvent, bool> CanActOn(int dayOfMonth)
            => e => SabotageSchedule.WithinCaps(StrikeLedger.KindOf(e), run, week, Calendar.DayOfYear((int)Season.Winter, dayOfMonth));

        for (int day = 4; day < 3 + SabotageTuning.TamperMinDaysApart; day++)
            Assert.Null(StrikeQueue.Tonight(run, CanActOn(day)));
        Assert.True(StrikeQueue.IsQueued(run, DarknessEvent.Tampering));
        Assert.Equal(DarknessEvent.Tampering, StrikeQueue.Tonight(run, CanActOn(3 + SabotageTuning.TamperMinDaysApart)));
    }

    [Fact]
    public void A_kind_is_queued_once()
    {
        var run = new RunState();
        StrikeQueue.Enqueue(run, DarknessEvent.ChestBlight);
        StrikeQueue.Enqueue(run, DarknessEvent.ChestBlight);
        Assert.Single(run.QueuedStrikes);
    }

    [Fact]
    public void The_loop_reset_clears_the_queue()
    {
        var run = new RunState();
        StrikeQueue.Enqueue(run, DarknessEvent.CropBlight);
        run.BeginNewRun(seed: 42);
        Assert.Empty(run.QueuedStrikes);
        Assert.Null(StrikeQueue.Tonight(run, Anything));
    }

    [Fact]
    public void A_save_from_before_the_queue_loads_with_it_empty()
    {
        RunState fromNewtonsoft = Newtonsoft.Json.JsonConvert.DeserializeObject<RunState>("{\"RunNumber\":2}")!;
        Assert.Empty(fromNewtonsoft.QueuedStrikes);
        RunState fromStj = System.Text.Json.JsonSerializer.Deserialize<RunState>("{}")!;
        Assert.Empty(fromStj.QueuedStrikes);
        RunState nulled = Newtonsoft.Json.JsonConvert.DeserializeObject<RunState>("{\"QueuedStrikes\":null}")!;
        Assert.Null(StrikeQueue.Tonight(nulled, Anything));
        StrikeQueue.Enqueue(nulled, DarknessEvent.Reversion);
        Assert.True(StrikeQueue.IsQueued(nulled, DarknessEvent.Reversion));
    }

    [Fact]
    public void The_queue_survives_a_save()
    {
        var run = new RunState();
        StrikeQueue.Enqueue(run, DarknessEvent.Tampering);
        RunState back = Newtonsoft.Json.JsonConvert.DeserializeObject<RunState>(Newtonsoft.Json.JsonConvert.SerializeObject(run))!;
        Assert.True(StrikeQueue.IsQueued(back, DarknessEvent.Tampering));
    }

    [Fact]
    public void A_name_from_a_later_version_is_passed_over()
    {
        var run = new RunState { QueuedStrikes = new() { "Locusts", "Reversion" } };
        Assert.Equal(DarknessEvent.Reversion, StrikeQueue.Tonight(run, Anything));
    }
}
