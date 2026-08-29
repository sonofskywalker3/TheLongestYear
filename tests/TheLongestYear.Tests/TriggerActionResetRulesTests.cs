using System.Collections.Generic;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class TriggerActionResetRulesTests
{
    private static readonly Dictionary<string, string?> Rows = new()
    {
        ["Mail_Abigail_8heart"] = "PLAYER_FRIENDSHIP_POINTS Current Abigail 2010",
        ["Mail_Emily_8heart"] = "PLAYER_HEARTS Current Emily 8, PLAYER_HAS_SEEN_EVENT Current 917409",
        ["Mail_Mom_5K"] = "PLAYER_MONEY_EARNED Current 5000, PLAYER_GENDER Current Male",
        ["Mail_Tribune_UpAndComing"] = "PLAYER_MONEY_EARNED Current 27000",
        ["Mail_Pierre_Fertilizers"] = "DAY_OF_MONTH 15",
        ["nebulouscharlotte.betterstart_Welcome"] = null,
    };

    [Fact]
    public void Heart_gated_invites_and_dated_mails_are_cleared_so_they_re_fire_next_loop()
    {
        List<string> clear = TriggerActionResetRules.IdsToClear(
            new[] { "Mail_Abigail_8heart", "Mail_Emily_8heart", "Mail_Pierre_Fertilizers" }, Rows, true);
        Assert.Equal(new[] { "Mail_Abigail_8heart", "Mail_Emily_8heart", "Mail_Pierre_Fertilizers" }, clear);
    }

    [Fact]
    public void Lifetime_money_mails_stay_recorded_because_totalMoneyEarned_is_never_rewound()
    {
        List<string> clear = TriggerActionResetRules.IdsToClear(
            new[] { "Mail_Mom_5K", "Mail_Tribune_UpAndComing", "Mail_Abigail_8heart" }, Rows, true);
        Assert.Equal(new[] { "Mail_Abigail_8heart" }, clear);
    }

    [Fact]
    public void Better_start_gift_follows_the_toggle()
    {
        string[] recorded = { "nebulouscharlotte.betterstart_Welcome" };
        Assert.Equal(recorded, TriggerActionResetRules.IdsToClear(recorded, Rows, resendBetterStartGift: true));
        Assert.Empty(TriggerActionResetRules.IdsToClear(recorded, Rows, resendBetterStartGift: false));
    }

    [Fact]
    public void A_recorded_id_with_no_data_row_is_cleared()
    {
        Assert.Equal(new[] { "some.removed.mod_Gift" },
            TriggerActionResetRules.IdsToClear(new[] { "some.removed.mod_Gift" }, Rows, true));
    }
}
