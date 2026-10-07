using System;
using System.Collections.Generic;
using TheLongestYear.Core.Sabotage;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>Review M2: who takes tonight's one strike. The guaranteed Winter tamper first, then a
/// debug arm, then a strike queued from a postponed night, then the every-loop guarantee, then the
/// roll. Each source is asked lazily and in that order, because asking can spend the night's random
/// stream (a reversion plan) or consume an arm.</summary>
public class NightPrecedenceTests
{
    private sealed class Night
    {
        public readonly List<string> Asked = new();
        public bool GuaranteedNight;
        public bool GuaranteedTakes;
        public bool Dice;
        public DarknessEvent? Armed, Queued, Owed, Rolled;

        public NightChoice Choose() => NightPrecedence.Choose(
            GuaranteedNight,
            () => { Asked.Add("guaranteed"); return GuaranteedTakes; },
            () => { Asked.Add("dice"); return Dice; },
            () => { Asked.Add("armed"); return Armed; },
            () => { Asked.Add("queued"); return Queued; },
            () => { Asked.Add("owed"); return Owed; },
            () => { Asked.Add("roll"); return Rolled; });
    }

    [Fact]
    public void The_guaranteed_tamper_takes_the_night_and_nothing_else_is_asked()
    {
        var n = new Night { GuaranteedNight = true, GuaranteedTakes = true, Armed = DarknessEvent.CropBlight };
        Assert.Equal(new NightChoice(NightSource.Guaranteed, DarknessEvent.Tampering), n.Choose());
        Assert.Equal(new[] { "guaranteed" }, n.Asked);
    }

    [Fact]
    public void A_guaranteed_tamper_with_nothing_fair_falls_through_to_the_rest()
    {
        var n = new Night { GuaranteedNight = true, GuaranteedTakes = false, Queued = DarknessEvent.Reversion };
        Assert.Equal(new NightChoice(NightSource.Queued, DarknessEvent.Reversion), n.Choose());
        Assert.Equal(new[] { "guaranteed", "dice", "armed", "queued" }, n.Asked);
    }

    [Fact]
    public void An_arm_beats_the_queue()
    {
        var n = new Night { Armed = DarknessEvent.ChestBlight, Queued = DarknessEvent.CropBlight };
        Assert.Equal(new NightChoice(NightSource.Armed, DarknessEvent.ChestBlight), n.Choose());
        Assert.DoesNotContain("queued", n.Asked);
    }

    [Fact]
    public void The_queue_beats_the_every_loop_guarantee_and_fires_on_a_quiet_roll()
    {
        var n = new Night { Dice = false, Queued = DarknessEvent.CropBlight, Owed = DarknessEvent.ChestBlight };
        Assert.Equal(new NightChoice(NightSource.Queued, DarknessEvent.CropBlight), n.Choose());
        Assert.DoesNotContain("owed", n.Asked);
    }

    [Fact]
    public void The_every_loop_guarantee_beats_a_quiet_roll()
    {
        var n = new Night { Dice = false, Owed = DarknessEvent.ChestBlight };
        Assert.Equal(new NightChoice(NightSource.Owed, DarknessEvent.ChestBlight), n.Choose());
        Assert.DoesNotContain("roll", n.Asked);
    }

    [Fact]
    public void A_quiet_roll_with_nothing_forced_is_a_quiet_night()
    {
        var n = new Night { Dice = false, Rolled = DarknessEvent.CropBlight };
        Assert.Equal(new NightChoice(NightSource.None, null), n.Choose());
        Assert.Equal(new[] { "dice", "armed", "queued", "owed" }, n.Asked);
    }

    [Fact]
    public void A_strike_roll_picks_from_the_season()
    {
        var n = new Night { Dice = true, Rolled = DarknessEvent.Reversion };
        Assert.Equal(new NightChoice(NightSource.Roll, DarknessEvent.Reversion), n.Choose());
        Assert.Equal(new[] { "dice", "armed", "queued", "owed", "roll" }, n.Asked);
    }

    [Fact]
    public void A_strike_roll_where_nothing_can_act_is_a_quiet_night()
    {
        var n = new Night { Dice = true, Rolled = null };
        Assert.Equal(new NightChoice(NightSource.None, null), n.Choose());
    }

    [Fact]
    public void The_dice_are_thrown_before_the_arm_as_they_always_were()
    {
        var n = new Night { Armed = DarknessEvent.CropBlight };
        n.Choose();
        Assert.Equal(new[] { "dice", "armed" }, n.Asked);
    }
}
