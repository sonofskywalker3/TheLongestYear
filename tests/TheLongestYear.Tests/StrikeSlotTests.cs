using System;
using TheLongestYear.Core;
using TheLongestYear.Core.Sabotage;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>Jeff, 2026-10-07: "we don't delay scenes without delaying the effect of them". A strike
/// whose scene cannot have tonight's overnight slot does not land at all: it is postponed, and the
/// night is as if no strike happened, so the roll and the every-loop guarantee bring it back.</summary>
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

    [Theory]
    // The scene had the slot (it staged, or it failed after taking it): the strike lands.
    [InlineData(true, true)]
    // It never had the slot (a collision missed, no pickFarmEvent on the first night): postponed.
    [InlineData(false, false)]
    public void A_strike_still_waiting_at_the_save_or_the_morning_lands_only_if_its_scene_had_the_slot(bool sceneHadTheSlot, bool lands)
        => Assert.Equal(lands, StrikeSlot.LandsAtNet(sceneHadTheSlot));

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
