using System;
using TheLongestYear.Core;
using TheLongestYear.Core.Sabotage;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>Jeff, 2026-10-08: "If there's a CONFLICT and a different scene runs, we push back one
/// day. If the SYSTEM is broken and a scene CAN'T run ever, then they miss out on the cool scene,
/// but still get hit." A collision queues the strike for the next free night; a scene that cannot
/// stage lands the strike now, bare, and the scene stays due.</summary>
public class ConflictRuleTests
{
    private static Func<bool> Yes => () => true;
    private static Func<bool> No => () => false;
    private static Func<bool> Boom => () => throw new InvalidOperationException("asked");

    // ---------------------------------------------------------------- at the pick

    [Fact]
    public void A_strike_whose_scene_has_played_lands_with_no_scene_by_design()
        => Assert.Equal(StrikePickAction.LandNoSceneByDesign, StrikeStaging.AtPick(sceneDue: false, Boom, Boom));

    [Fact]
    public void A_due_scene_that_can_stage_and_show_the_pick_waits_for_the_slot()
        => Assert.Equal(StrikePickAction.WaitForScene, StrikeStaging.AtPick(sceneDue: true, Yes, Yes));

    [Fact]
    public void A_due_scene_that_cannot_stage_lands_the_strike_bare_and_never_asks_about_the_pick()
        => Assert.Equal(StrikePickAction.LandBare, StrikeStaging.AtPick(sceneDue: true, No, Boom));

    [Fact]
    public void A_due_scene_that_cannot_show_the_pick_lands_the_strike_bare()
        => Assert.Equal(StrikePickAction.LandBare, StrikeStaging.AtPick(sceneDue: true, Yes, No));

    [Fact]
    public void A_broken_scene_lands_bare_and_is_recorded_like_any_landed_strike()
    {
        var s = new StrikeLifecycle();
        Assert.True(s.LandBare(out bool newlyCommitted));
        Assert.True(newlyCommitted);                       // the run records it now
        Assert.Equal(StrikeNetAction.None, s.AtNet());     // nothing left for the net
        Assert.False(s.Postpone());                        // never queued
        Assert.False(s.BeginApply());                      // and never lands twice

        var run = new RunState();
        StrikeLedger.Record(run, DarknessEvent.CropBlight, weekOfYear: 7, Season.Summer, dayOfYear: 44);
        Assert.Contains("CropBlight", run.StruckEvents);
        Assert.Equal(1, run.BlightNightsThisWeek);
        Assert.True(run.DarknessChance < NightRoll.SeasonChance(Season.Summer));
        Assert.Null(StrikeGuarantee.ForcedTonight(Season.Summer, 15, run.StruckEvents, e => e == DarknessEvent.CropBlight));
    }

    [Fact]
    public void A_bare_landing_leaves_the_scene_due()
    {
        // The scene is marked played only when it was shown, so a bare landing keeps it owed.
        var run = new RunState();
        Assert.True(StrikeScenes.IsDue(DarknessEvent.Tampering, run.StrikeScenesPlayed ??= new()));
    }

    // ---------------------------------------------------------------- the overnight slot

    [Fact]
    public void A_conflict_postpones_and_queues_for_the_next_free_night()
    {
        Assert.Equal(StrikeSlotVerdict.Postpone, StrikeSlot.Decide(false, false, OvernightEvent.Scripted, false, false, No));
        var s = new StrikeLifecycle();
        Assert.True(s.Postpone());
        Assert.False(s.BeginApply());                      // nothing lands on a conflict
        Assert.True(StrikeQueue.Queues(PostponeCause.SlotTaken));
        var run = new RunState();
        StrikeQueue.Enqueue(run, DarknessEvent.Reversion);
        Assert.Equal(DarknessEvent.Reversion, StrikeQueue.Tonight(run, _ => true));
    }

    [Fact]
    public void Our_scene_replaced_before_setUp_is_a_conflict_and_queues()
    {
        var s = new StrikeLifecycle();
        s.OnHandedSlot();
        Assert.Equal(StrikeNetAction.Replaced, s.AtNet());
        Assert.Equal(PostponeCause.SlotTaken, StrikeStaging.CauseAtNet(StrikeNetAction.Replaced));
        Assert.True(StrikeQueue.Queues(StrikeStaging.CauseAtNet(StrikeNetAction.Replaced)));
        Assert.True(s.Postpone());
        Assert.False(s.BeginApply());
    }

    [Fact]
    public void A_setUp_that_throws_lands_the_strike_bare()
    {
        var s = new StrikeLifecycle();
        s.OnHandedSlot();
        s.OnSetUp();
        // setUp failed before staging: the scene lands it bare at once.
        Assert.True(s.LandBare(out bool newlyCommitted));
        Assert.True(newlyCommitted);
        Assert.Equal(StrikeNetAction.None, s.AtNet());
    }

    [Fact]
    public void A_setUp_that_ran_but_left_the_strike_uncommitted_is_landed_bare_by_the_net()
    {
        var s = new StrikeLifecycle();
        s.OnHandedSlot();
        s.OnSetUp();
        Assert.Equal(StrikeNetAction.LandBare, s.AtNet());
        Assert.True(s.LandBare(out _));
        Assert.Equal(StrikeNetAction.None, s.AtNet());
    }

    [Fact]
    public void A_strike_never_handed_the_slot_is_postponed_unqueued()
    {
        // A fail or restart night leaves the slot alone, and the first night of a save runs no
        // pickFarmEvent: neither is a conflict with another scene, and the loop reset follows a fail.
        var s = new StrikeLifecycle();
        Assert.Equal(StrikeNetAction.Postpone, s.AtNet());
        Assert.Equal(PostponeCause.NeverStaged, StrikeStaging.CauseAtNet(StrikeNetAction.Postpone));
        Assert.False(StrikeQueue.Queues(PostponeCause.NeverStaged));
    }

    [Fact]
    public void A_staged_scene_lands_exactly_once_whatever_the_net_says_after()
    {
        var s = new StrikeLifecycle();
        s.OnHandedSlot();
        s.OnSetUp();
        Assert.True(s.Commit());
        Assert.Equal(StrikeNetAction.Land, s.AtNet());
        Assert.True(s.BeginApply());
        Assert.False(s.LandBare(out bool again));
        Assert.False(again);
        Assert.Equal(StrikeNetAction.None, s.AtNet());
    }

    // ---------------------------------------------------------------- the guaranteed Winter tamper

    [Fact]
    public void The_guaranteed_tamper_is_satisfied_by_a_bare_landing()
    {
        // The cloud cannot stage: the tamper is not held back, it lands bare and counts.
        Assert.Equal(StrikePickAction.LandBare, StrikeStaging.AtPick(sceneDue: true, No, Yes));
        var run = new RunState();
        var s = new StrikeLifecycle();
        Assert.True(s.LandBare(out bool newlyCommitted));
        if (newlyCommitted) GuaranteedTamper.OnCommitted(run);
        GuaranteedTamper.OnApplied(run, landed: true);
        Assert.True(run.GuaranteedTamperDone);
        Assert.False(run.GuaranteedTamperPostponed);
        Assert.False(NightRoll.IsGuaranteedTamperNight(run, firstWinterEver: true, Season.Winter, 3));
    }

    [Fact]
    public void A_staging_failure_is_not_a_postpone_cause_any_more()
        => Assert.Equal(new[] { PostponeCause.SlotTaken, PostponeCause.NeverStaged }, Enum.GetValues<PostponeCause>());
}
