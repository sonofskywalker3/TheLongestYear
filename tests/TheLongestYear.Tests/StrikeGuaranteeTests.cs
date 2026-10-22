using System;
using System.Collections.Generic;
using TheLongestYear.Core;
using TheLongestYear.Core.Sabotage;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>Spec 2026-09-21: every kind of strike lands at least once every loop. A kind not yet
/// struck by day 15 of its debut season is forced on the first night it can act.</summary>
public class StrikeGuaranteeTests
{
    private static readonly string[] None = Array.Empty<string>();
    private static bool Always(DarknessEvent e) => true;

    [Fact]
    public void Nothing_is_owed_before_day_15()
        => Assert.Empty(StrikeGuarantee.Owed(Season.Summer, 14, None));

    [Fact]
    public void Summer_owes_crows_and_thief_from_day_15()
        => Assert.Equal(new[] { DarknessEvent.CropBlight, DarknessEvent.ChestBlight }, StrikeGuarantee.Owed(Season.Summer, 15, None));

    [Fact]
    public void A_kind_that_already_struck_is_not_owed()
        => Assert.Equal(new[] { DarknessEvent.ChestBlight }, StrikeGuarantee.Owed(Season.Summer, 20, new[] { "CropBlight" }));

    [Fact]
    public void Fall_owes_only_the_hall()
        => Assert.Equal(new[] { DarknessEvent.Reversion }, StrikeGuarantee.Owed(Season.Fall, 15, None));

    [Theory]
    [InlineData(Season.Spring)]
    [InlineData(Season.Winter)]
    public void Spring_and_Winter_owe_nothing(Season season)
        => Assert.Empty(StrikeGuarantee.Owed(season, 20, None));

    [Fact]
    public void Crows_missed_in_Summer_are_not_owed_in_Fall()
        => Assert.DoesNotContain(DarknessEvent.CropBlight, StrikeGuarantee.Owed(Season.Fall, 20, None));

    [Fact]
    public void The_first_owed_kind_that_can_act_is_forced()
        => Assert.Equal(DarknessEvent.CropBlight, StrikeGuarantee.ForcedTonight(Season.Summer, 15, None, Always));

    [Fact]
    public void A_kind_that_cannot_act_hands_the_night_to_the_next_owed_kind()
        => Assert.Equal(DarknessEvent.ChestBlight,
            StrikeGuarantee.ForcedTonight(Season.Summer, 15, None, e => e != DarknessEvent.CropBlight));

    [Fact]
    public void Nothing_is_forced_when_no_owed_kind_can_act()
        => Assert.Null(StrikeGuarantee.ForcedTonight(Season.Summer, 15, None, e => false));

    [Fact]
    public void With_both_owed_they_land_on_consecutive_nights()
    {
        var struck = new HashSet<string>();
        DarknessEvent? first = StrikeGuarantee.ForcedTonight(Season.Summer, 15, struck, Always);
        struck.Add(first.Value.ToString());
        DarknessEvent? second = StrikeGuarantee.ForcedTonight(Season.Summer, 16, struck, Always);
        Assert.Equal(DarknessEvent.CropBlight, first);
        Assert.Equal(DarknessEvent.ChestBlight, second);
    }
}
