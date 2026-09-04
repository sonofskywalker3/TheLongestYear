using System;
using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>Jeff's ruling, 2026-09-04 (Nexus post, nyxnyx2234: "a bundle asked me to gather 90
/// common mushrooms on my first spring"): the Wild Seed exemption from the measured ceilings is
/// only honest when the item's Wild Seeds can actually grow before the bundle is due. Common
/// Mushroom is a FALL Wild Seed crop; a Spring deadline gets the Spring ceiling like any other
/// forage. The board-level behaviour lives in ForageAskBandsTests since 0.16.182; these cover the
/// ForageAskLimits primitives.</summary>
public class WildSeedDeadlineClampTests
{
    private const string CommonMushroom = "(O)404";   // Fall Wild Seeds
    private const string WildHorseradish = "(O)16";   // Spring Wild Seeds
    private const string RainbowShell = "(O)394";     // never growable

    [Theory]
    [InlineData(CommonMushroom, Season.Spring, false)]
    [InlineData(CommonMushroom, Season.Summer, false)]
    [InlineData(CommonMushroom, Season.Fall, true)]
    [InlineData(CommonMushroom, Season.Winter, true)]
    [InlineData(WildHorseradish, Season.Spring, true)]
    [InlineData(RainbowShell, Season.Winter, false)]
    public void Wild_seeds_only_count_once_their_season_has_arrived(string id, Season deadline, bool growable)
        => Assert.Equal(growable, ForageAskLimits.IsWildSeedGrowableBy(id, deadline));

    [Fact]
    public void A_spring_deadline_clamps_common_mushroom_to_the_spring_ceiling()
    {
        int springCeiling = ForageAskLimits.MeasuredMaxAsk(Season.Spring, CommonMushroom)!.Value;
        Assert.True(springCeiling < 90);
        Assert.Equal(springCeiling, ForageAskLimits.ClampForDeadline(CommonMushroom, 90, Season.Spring));
    }

    [Fact]
    public void A_later_deadline_takes_the_most_generous_season_reached_so_far()
    {
        int spring = ForageAskLimits.MeasuredMaxAsk(Season.Spring, CommonMushroom)!.Value;
        int summer = ForageAskLimits.MeasuredMaxAsk(Season.Summer, CommonMushroom)!.Value;
        Assert.Equal(Math.Max(spring, summer), ForageAskLimits.ClampForDeadline(CommonMushroom, 90, Season.Summer));
    }

    [Fact]
    public void Once_the_seeds_can_grow_the_ask_is_uncapped_again()
    {
        Assert.Equal(90, ForageAskLimits.ClampForDeadline(CommonMushroom, 90, Season.Fall));
        Assert.Equal(90, ForageAskLimits.ClampForDeadline(WildHorseradish, 90, Season.Spring));
    }

    [Fact]
    public void No_deadline_keeps_the_old_any_season_behaviour()
    {
        Assert.Equal(90, ForageAskLimits.ClampForDeadline(CommonMushroom, 90, null));
        Assert.Equal(ForageAskLimits.ClampAnySeason(RainbowShell, 90), ForageAskLimits.ClampForDeadline(RainbowShell, 90, null));
    }
}
