using System.Collections.Generic;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>Loop reset hygiene for special orders: the rewind drops town ("" type) orders in progress,
/// clears the town board's offer so it re-rolls, and forgets completed town orders so the
/// non-repeatable ones come back. Mr. Qi's orders and any other board type are left alone.</summary>
public class SpecialOrderResetRulesTests
{
    [Theory]
    [InlineData("", true)]
    [InlineData(null, true)]
    [InlineData("Qi", false)]
    [InlineData("DesertFestivalMarlon", false)]
    public void Only_blank_order_type_is_a_town_order(string? orderType, bool expected)
        => Assert.Equal(expected, SpecialOrderResetRules.IsTownOrder(orderType));

    [Fact]
    public void Forgets_completed_town_orders_only()
    {
        var types = new Dictionary<string, string?>
        {
            ["Robin"] = "",
            ["Demetrius"] = null,
            ["QiChallenge2"] = "Qi",
            ["DesertFestivalMarlon"] = "DesertFestivalMarlon",
        };
        var completed = new[] { "Robin", "Demetrius", "QiChallenge2", "DesertFestivalMarlon", "RemovedModOrder" };

        List<string> forget = SpecialOrderResetRules.CompletedToForget(completed, types);

        Assert.Equal(new[] { "Robin", "Demetrius" }, forget);
    }

    [Fact]
    public void Nothing_completed_means_nothing_to_forget()
        => Assert.Empty(SpecialOrderResetRules.CompletedToForget(
            System.Array.Empty<string>(), new Dictionary<string, string?>()));
}
