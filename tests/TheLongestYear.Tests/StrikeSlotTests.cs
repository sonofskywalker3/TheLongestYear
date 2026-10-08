using System;
using TheLongestYear.Core;
using TheLongestYear.Core.Sabotage;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>Jeff, 2026-10-07 and 2026-10-08: a strike whose scene cannot have tonight's overnight
/// slot (a conflict) does not land at all: it is postponed, the night is as if no strike happened,
/// and it is queued for the next free night (StrikeQueueTests). A broken scene lands it bare
/// (ConflictRuleTests).</summary>
public class StrikeSlotTests
{
    private static Func<bool> Personal(bool owns) => () => owns;

    [Fact]
    public void An_empty_slot_with_nothing_waiting_plays_the_scene()
        => Assert.Equal(StrikeSlotVerdict.PlayScene, StrikeSlot.Decide(false, false, OvernightEvent.None, false, false, Personal(false)));

    [Fact]
    public void A_random_vanilla_event_gives_way_to_the_scene()
        => Assert.Equal(StrikeSlotVerdict.PlayScene, StrikeSlot.Decide(false, false, OvernightEvent.Random, false, false, Personal(true)));

    [Fact]
    public void A_scripted_event_postpones_the_strike()
        => Assert.Equal(StrikeSlotVerdict.Postpone, StrikeSlot.Decide(false, false, OvernightEvent.Scripted, false, false, Personal(false)));

    [Fact]
    public void The_wildcard_night_event_postpones_the_strike_whatever_vanilla_picked()
    {
        Assert.Equal(StrikeSlotVerdict.Postpone, StrikeSlot.Decide(false, true, OvernightEvent.None, false, false, Personal(false)));
        Assert.Equal(StrikeSlotVerdict.Postpone, StrikeSlot.Decide(false, true, OvernightEvent.Random, false, false, Personal(false)));
    }

    [Theory]
    [InlineData(true, false, false)]   // a wedding
    [InlineData(false, true, false)]   // another mod's farmEventOverride
    [InlineData(false, false, true)]   // a birth, a couple's birth, a pregnancy question
    public void An_empty_slot_another_event_reads_again_postpones_the_strike(bool wedding, bool farmEventOverride, bool personal)
        => Assert.Equal(StrikeSlotVerdict.Postpone, StrikeSlot.Decide(false, false, OvernightEvent.None, wedding, farmEventOverride, Personal(personal)));

    [Fact]
    public void A_fail_night_leaves_the_slot_alone()
        => Assert.Equal(StrikeSlotVerdict.LeaveAlone, StrikeSlot.Decide(true, true, OvernightEvent.Scripted, true, true, Personal(true)));

    [Fact]
    public void The_personal_probe_is_only_asked_on_an_empty_slot()
    {
        Func<bool> boom = () => throw new InvalidOperationException("asked");
        Assert.Equal(StrikeSlotVerdict.PlayScene, StrikeSlot.Decide(false, false, OvernightEvent.Random, false, false, boom));
        Assert.Equal(StrikeSlotVerdict.Postpone, StrikeSlot.Decide(false, false, OvernightEvent.Scripted, false, false, boom));
    }

    // ---------------------------------------------------------------- one strike's life (review C1)

    [Fact]
    public void A_scene_that_stages_commits_once_and_lands_once()
    {
        var s = new StrikeLifecycle();
        Assert.True(s.Commit());            // setUp staged: record it now
        Assert.False(s.Commit());           // never twice
        Assert.True(s.BeginApply());        // the scene's beat
        Assert.False(s.BeginApply());       // the end-of-scene net does nothing more
        Assert.Equal(StrikeNetAction.None, s.AtNet());
    }

    [Fact]
    public void A_scene_that_cannot_stage_lands_bare_and_the_net_has_nothing_left()
    {
        // Took the slot, then setUp found no ground beside the chest (or threw): the system is
        // broken, so the strike lands now with no scene (Jeff, 2026-10-08), once.
        var s = new StrikeLifecycle();
        s.OnHandedSlot();
        s.OnSetUp();
        Assert.False(s.BeginApply());
        Assert.True(s.LandBare(out bool newlyCommitted));
        Assert.True(newlyCommitted);
        Assert.False(s.Postpone());
        Assert.False(s.BeginApply());
        Assert.Equal(StrikeNetAction.None, s.AtNet());
    }

    [Fact]
    public void Another_mod_replacing_our_scene_after_the_postfix_is_a_conflict_the_net_queues()
    {
        // pickFarmEvent handed out our scene, but vanilla never set it up: nothing was committed.
        var s = new StrikeLifecycle();
        s.OnHandedSlot();
        Assert.Equal(StrikeNetAction.Replaced, s.AtNet());
        Assert.Equal(PostponeCause.SlotTaken, StrikeStaging.CauseAtNet(s.AtNet()));
    }

    [Fact]
    public void A_staged_scene_that_ended_before_its_beat_lands_at_the_net()
    {
        var s = new StrikeLifecycle();
        s.Commit();
        Assert.Equal(StrikeNetAction.Land, s.AtNet());
        Assert.False(s.Postpone());         // committed: tonight is spent, it cannot be postponed
        Assert.True(s.BeginApply());
    }

    [Fact]
    public void A_strike_with_no_scene_by_design_commits_and_lands_at_once()
    {
        var s = new StrikeLifecycle();
        Assert.True(s.LandNow(out bool newlyCommitted));
        Assert.True(newlyCommitted);
        Assert.False(s.LandNow(out newlyCommitted));
        Assert.False(newlyCommitted);
    }

    // ---------------------------------------------------------------- the guaranteed Winter tamper (review I1)

    [Fact]
    public void A_postponed_then_failed_guaranteed_tamper_is_still_owed()
    {
        var run = new RunState();
        GuaranteedTamper.OnPostponed(run);
        GuaranteedTamper.OnCommitted(run);
        Assert.True(run.GuaranteedTamperDone);
        GuaranteedTamper.OnApplied(run, landed: false);
        Assert.False(run.GuaranteedTamperDone);
        Assert.True(run.GuaranteedTamperPostponed);
        Assert.True(NightRoll.IsGuaranteedTamperNight(run, firstWinterEver: false, Season.Winter, 9));
    }

    [Fact]
    public void A_postponed_guaranteed_tamper_that_lands_is_done_and_no_longer_carried()
    {
        var run = new RunState();
        GuaranteedTamper.OnPostponed(run);
        GuaranteedTamper.OnCommitted(run);
        GuaranteedTamper.OnApplied(run, landed: true);
        Assert.True(run.GuaranteedTamperDone);
        Assert.False(run.GuaranteedTamperPostponed);
        Assert.False(NightRoll.IsGuaranteedTamperNight(run, firstWinterEver: false, Season.Winter, 9));
    }

    // ---------------------------------------------------------------- the ledger

    [Fact]
    public void Recording_a_strike_spends_the_chance_the_cap_and_the_guarantee()
    {
        var run = new RunState();
        StrikeLedger.Record(run, DarknessEvent.ChestBlight, weekOfYear: 7, Season.Summer, dayOfYear: 44);
        Assert.Equal(7, run.DarknessChanceWeek);
        Assert.True(run.DarknessChance < NightRoll.SeasonChance(Season.Summer));
        Assert.Equal(1, run.BlightNightsThisWeek);
        Assert.Contains("ChestBlight", run.StruckEvents);

        StrikeLedger.Record(run, DarknessEvent.Tampering, weekOfYear: 13, Season.Winter, dayOfYear: 86);
        Assert.Contains(86, run.TamperDays);
    }

    [Fact]
    public void A_postponed_night_leaves_the_kind_owed_and_the_chance_whole()
    {
        // Summer 15, nothing struck: the crows are forced. The night collides with the bus repair, so
        // the strike is postponed and nothing is recorded. On Summer 16 they are still forced, at the
        // week's full chance; once a night lands them, they are no longer owed.
        var run = new RunState();
        Func<DarknessEvent, bool> canAct = _ => true;
        Assert.Equal(DarknessEvent.CropBlight, StrikeGuarantee.ForcedTonight(Season.Summer, 15, run.StruckEvents, canAct));
        Assert.Equal(DarknessEvent.CropBlight, StrikeGuarantee.ForcedTonight(Season.Summer, 16, run.StruckEvents, canAct));
        Assert.Equal(NightRoll.SeasonChance(Season.Summer), NightRoll.ChanceTonight(run, 7, Season.Summer));
        Assert.Empty(run.TamperDays);

        StrikeLedger.Record(run, DarknessEvent.CropBlight, 7, Season.Summer, 16);
        Assert.Equal(DarknessEvent.ChestBlight, StrikeGuarantee.ForcedTonight(Season.Summer, 17, run.StruckEvents, canAct));
    }

    [Fact]
    public void A_postponed_guaranteed_winter_tamper_stays_owed_past_week_one_until_it_lands()
    {
        // Postponed on Winter 7, the last night of week 1: without the carry, Winter 8 would no
        // longer be a guaranteed night and the Winter would lose its tamper.
        var run = new RunState { GuaranteedTamperPostponed = true };
        Assert.True(NightRoll.IsGuaranteedTamperNight(run, firstWinterEver: true, Season.Winter, NightRoll.Week1Nights + 1));
        Assert.True(NightRoll.IsGuaranteedTamperNight(run, firstWinterEver: false, Season.Winter, 20));
        Assert.False(NightRoll.IsGuaranteedTamperNight(run, firstWinterEver: true, Season.Fall, 8));
        run.GuaranteedTamperDone = true;
        Assert.False(NightRoll.IsGuaranteedTamperNight(run, firstWinterEver: true, Season.Winter, 9));
        run.BeginNewRun(1);
        Assert.False(run.GuaranteedTamperPostponed);
    }

    [Theory]
    [InlineData(DarknessEvent.CropBlight, SabotageKind.Blight)]
    [InlineData(DarknessEvent.ChestBlight, SabotageKind.Blight)]
    [InlineData(DarknessEvent.Reversion, SabotageKind.Reversion)]
    [InlineData(DarknessEvent.Tampering, SabotageKind.Tampering)]
    public void Each_event_belongs_to_its_front(DarknessEvent e, SabotageKind kind)
        => Assert.Equal(kind, StrikeLedger.KindOf(e));
}
