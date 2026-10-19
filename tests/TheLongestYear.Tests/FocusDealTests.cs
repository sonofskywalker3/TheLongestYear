using System.Linq;
using System.Text.Json;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class FocusDealTests
{
    private static string Json(object o) => JsonSerializer.Serialize(o);

    [Fact]
    public void Same_seed_same_hands()
        => Assert.Equal(Json(FocusDeal.Deal(42, _ => true)), Json(FocusDeal.Deal(42, _ => true)));

    [Fact]
    public void Different_seeds_deal_different_hands()
        => Assert.NotEqual(Json(FocusDeal.Deal(1, _ => true)), Json(FocusDeal.Deal(2, _ => true)));

    [Fact]
    public void Always_three_hands_in_theme_order()
        => Assert.Equal(new[] { FocusTheme.Tools, FocusTheme.Buildings, FocusTheme.Knacks },
            FocusDeal.Deal(5, _ => true).Select(h => h.Theme).ToArray());

    [Fact]
    public void Each_hand_is_at_most_three_distinct_chains_of_its_theme()
    {
        for (int seed = 0; seed < 50; seed++)
            foreach (var h in FocusDeal.Deal(seed, _ => true))
            {
                Assert.Equal(3, h.Chains.Count);
                Assert.Equal(h.Chains.Count, h.Chains.Distinct().Count());
                Assert.All(h.Chains, id => Assert.Equal(h.Theme, FocusChains.TryGet(id)!.Theme));
                Assert.Null(h.Chosen);
                Assert.False(h.Bought);
            }
    }

    [Fact]
    public void Infeasible_chains_are_never_dealt()
    {
        for (int seed = 0; seed < 50; seed++)
            Assert.DoesNotContain("axe", FocusDeal.Deal(seed, c => c.Id != "axe").SelectMany(h => h.Chains));
    }

    [Theory]
    [InlineData(FocusQuoteKind.Offer, true)]
    [InlineData(FocusQuoteKind.KeepOnly, false)]
    [InlineData(FocusQuoteKind.Done, false)]
    [InlineData(FocusQuoteKind.Locked, false)]
    public void Only_an_offer_is_dealable(FocusQuoteKind kind, bool dealable)
        => Assert.Equal(dealable, FocusDeal.IsDealable(new FocusQuote("axe", kind, 1, "keep_axe_1", new[] { "keep_axe_1" }, 100)));

    [Fact]
    public void A_world_reached_top_rung_is_not_dealt()
    {
        var chain = FocusChains.TryGet("axe")!;
        var quote = FocusMorph.Quote(chain, new MetaState(), chain.Rungs.Count, 1.0);
        Assert.Equal(FocusQuoteKind.KeepOnly, quote.Kind);
        Assert.False(FocusDeal.IsDealable(quote));
    }

    [Fact]
    public void A_theme_with_two_feasible_deals_two_and_none_deals_empty()
    {
        var h = FocusDeal.Deal(7, c => c.Theme != FocusTheme.Buildings && (c.Theme != FocusTheme.Tools || c.Id is "hoe" or "axe"));
        Assert.Equal(2, h[0].Chains.Count);
        Assert.Empty(h[1].Chains);
        Assert.Equal(3, h[2].Chains.Count);
    }
}
