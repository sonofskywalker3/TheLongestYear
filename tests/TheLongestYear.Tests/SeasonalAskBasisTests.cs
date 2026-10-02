using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>Hand rows from the 2026-09-30 quantity spec: gathered goods, pantry staples, Prize
/// Tickets, and the fish tools/fish-sim cannot simulate (mines, Night Market, bobber rows).</summary>
public class SeasonalAskBasisTests
{
    [Theory]
    [InlineData("(O)309", Season.Spring, 20.0)]   // Acorn
    [InlineData("(O)310", Season.Fall, 20.0)]     // Maple Seed: best so far, Summer 20 beats Fall 17
    [InlineData("(O)Moss", Season.Spring, 5.0)]
    [InlineData("(O)Moss", Season.Summer, 99.0)]  // green rain
    [InlineData("(O)635", Season.Summer, 14.0)]   // Orange
    [InlineData("(O)613", Season.Fall, 14.0)]     // Apple
    [InlineData("(O)815", Season.Spring, 7.0)]    // Tea Leaves: Caroline's sunroom bush
    [InlineData("(O)815", Season.Summer, 17.0)]
    [InlineData("(O)78", Season.Spring, 15.0)]    // Cave Carrot
    [InlineData("(O)399", Season.Winter, 35.0)]   // Spring Onion keeps
    [InlineData("(O)296", Season.Spring, 80.0)]   // Salmonberry
    [InlineData("(O)178", Season.Spring, 60.0)]   // Hay
    [InlineData("(O)168", Season.Summer, 8.0)]    // Trash
    [InlineData("(O)246", Season.Spring, 20.0)]   // Wheat Flour
    [InlineData("(O)419", Season.Summer, 20.0)]   // Vinegar
    [InlineData("(O)PrizeTicket", Season.Fall, 3.0)]
    [InlineData("(O)Goby", Season.Winter, 21.0)]
    [InlineData("(O)158", Season.Spring, 10.5)]   // Stonefish, floor 20
    [InlineData("(O)161", Season.Spring, 9.0)]    // Ice Pip, floor 60
    [InlineData("(O)162", Season.Spring, 7.6)]    // Lava Eel, floor 100
    [InlineData("(O)796", Season.Summer, 36.5)]   // Slimejack
    [InlineData("(O)798", Season.Winter, 5.0)]    // Midnight Squid, fishing hours only
    [InlineData("(O)799", Season.Winter, 4.0)]    // Spook Fish
    [InlineData("(O)800", Season.Winter, 2.0)]    // Blobfish
    public void Rows_read_the_best_season_up_to_the_deadline(string id, Season deadline, double expected)
        => Assert.Equal(expected, SeasonalAskBasis.BasisByDeadline(id, deadline));

    [Theory]
    [InlineData("(O)635", Season.Spring)]   // Orange: no fruit before Summer
    [InlineData("(O)613", Season.Summer)]   // Apple: Fall fruit
    [InlineData("(O)798", Season.Fall)]     // Night Market is Winter 15-17
    public void A_season_before_the_item_exists_has_no_basis(string id, Season deadline)
        => Assert.Null(SeasonalAskBasis.BasisByDeadline(id, deadline));

    [Theory]
    [InlineData("(O)634")]   // Apricot: single on purpose (Spring fruit, year-two tree)
    [InlineData("(O)638")]   // Cherry
    [InlineData("(O)MysticSyrup")]
    public void Single_on_purpose_items_have_no_row(string id)
        => Assert.False(SeasonalAskBasis.Rows.ContainsKey(id));

    [Fact]
    public void Every_row_has_four_seasons_and_no_negative_values()
        => Assert.All(SeasonalAskBasis.Rows, kv =>
        {
            Assert.Equal(4, kv.Value.Length);
            Assert.All(kv.Value, v => Assert.True(v >= 0, kv.Key));
        });

    [Fact]
    public void The_pass_reads_the_new_rows()
    {
        Assert.True(QuantityAskPass.Covers("(O)309"));
        Assert.Equal(21.0, QuantityAskPass.BasisByDeadline("(O)Goby", Season.Spring));
    }
}
