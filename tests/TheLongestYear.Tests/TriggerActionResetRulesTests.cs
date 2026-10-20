using System.Collections.Generic;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class TriggerActionResetRulesTests
{
    [Fact]
    public void Heart_gated_invites_and_dated_mails_are_cleared_so_they_re_fire_next_loop()
    {
        List<string> clear = TriggerActionResetRules.IdsToClear(
            new[] { "Mail_Abigail_8heart", "Mail_Emily_8heart", "Mail_Pierre_Fertilizers" }, true);
        Assert.Equal(new[] { "Mail_Abigail_8heart", "Mail_Emily_8heart", "Mail_Pierre_Fertilizers" }, clear);
    }

    [Fact]
    public void Money_earned_mails_are_cleared_because_money_earned_restarts_each_loop()
    {
        List<string> clear = TriggerActionResetRules.IdsToClear(
            new[] { "Mail_Mom_5K", "Mail_Dad_120K", "Mail_Tribune_UpAndComing", "Mail_Abigail_8heart" }, true);
        Assert.Equal(new[] { "Mail_Mom_5K", "Mail_Dad_120K", "Mail_Tribune_UpAndComing", "Mail_Abigail_8heart" }, clear);
    }

    [Fact]
    public void Better_start_gift_follows_the_toggle()
    {
        string[] recorded = { "nebulouscharlotte.betterstart_Welcome" };
        Assert.Equal(recorded, TriggerActionResetRules.IdsToClear(recorded, resendBetterStartGift: true));
        Assert.Empty(TriggerActionResetRules.IdsToClear(recorded, resendBetterStartGift: false));
    }

    [Fact]
    public void Better_start_prefix_match_ignores_case_and_leaves_other_mods_alone()
    {
        Assert.True(TriggerActionResetRules.ShouldKeep("NebulousCharlotte.BetterStart_Robin", false));
        Assert.False(TriggerActionResetRules.ShouldKeep("some.removed.mod_Gift", false));
    }
}
