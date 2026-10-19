using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class FocusPurchaseTests
{
    private static FocusHand Chosen(string chain)
    {
        var h = new FocusHand { Theme = FocusChains.TryGet(chain)!.Theme, Chosen = chain };
        h.Chains.Add(chain);
        return h;
    }

    [Fact]
    public void Buying_grants_every_unkept_rung_and_charges_the_target_price()
    {
        var meta = new MetaState { JunimoPoints = 400 };
        var hand = Chosen("axe");
        var q = FocusMorph.Quote(FocusChains.TryGet("axe")!, meta, 1, 1.0);
        Assert.Equal(FocusBuyResult.Success, FocusPurchase.TryBuy(meta, hand, q));
        Assert.Equal(100, meta.JunimoPoints);
        Assert.Contains("keep_axe_1", meta.OwnedUpgrades);
        Assert.Contains("keep_axe_2", meta.OwnedUpgrades);
        Assert.True(hand.Bought);
        Assert.Equal("keep_axe_2", hand.BoughtUpgradeId);
    }

    [Fact]
    public void Not_enough_jp_changes_nothing()
    {
        var meta = new MetaState { JunimoPoints = 10 };
        var hand = Chosen("axe");
        var q = FocusMorph.Quote(FocusChains.TryGet("axe")!, meta, 0, 1.0);
        Assert.Equal(FocusBuyResult.NotEnoughJp, FocusPurchase.TryBuy(meta, hand, q));
        Assert.Equal(10, meta.JunimoPoints);
        Assert.Empty(meta.OwnedUpgrades);
        Assert.False(hand.Bought);
        Assert.Null(hand.BoughtUpgradeId);
    }

    [Fact]
    public void Second_buy_is_already_bought()
    {
        var meta = new MetaState { JunimoPoints = 1000 };
        var hand = Chosen("axe");
        var q = FocusMorph.Quote(FocusChains.TryGet("axe")!, meta, 0, 1.0);
        Assert.Equal(FocusBuyResult.Success, FocusPurchase.TryBuy(meta, hand, q));
        Assert.Equal(FocusBuyResult.AlreadyBought, FocusPurchase.TryBuy(meta, hand, q));
        Assert.Equal(850, meta.JunimoPoints);
    }

    [Fact]
    public void An_unpicked_chain_cannot_be_bought()
    {
        var meta = new MetaState { JunimoPoints = 1000 };
        var hand = Chosen("hoe");
        var q = FocusMorph.Quote(FocusChains.TryGet("axe")!, meta, 0, 1.0);
        Assert.Equal(FocusBuyResult.NotChosen, FocusPurchase.TryBuy(meta, hand, q));
        Assert.Equal(1000, meta.JunimoPoints);
        Assert.Empty(meta.OwnedUpgrades);

        var none = new FocusHand { Theme = FocusTheme.Tools };
        Assert.Equal(FocusBuyResult.NotChosen, FocusPurchase.TryBuy(meta, none, q));
    }

    [Fact]
    public void Done_and_locked_quotes_refuse()
    {
        var meta = new MetaState { JunimoPoints = 1000 };
        var hand = Chosen("axe");
        var done = new FocusQuote("axe", FocusQuoteKind.Done, 4, null, new string[0], 0);
        var locked = new FocusQuote("axe", FocusQuoteKind.Locked, 1, "keep_axe_1", new string[0], 150);
        Assert.Equal(FocusBuyResult.Done, FocusPurchase.TryBuy(meta, hand, done));
        Assert.Equal(FocusBuyResult.Locked, FocusPurchase.TryBuy(meta, hand, locked));
        Assert.Equal(1000, meta.JunimoPoints);
        Assert.False(hand.Bought);
    }

    [Fact]
    public void Keep_only_quotes_buy_like_offers()
    {
        var meta = new MetaState { JunimoPoints = 2000 };
        var hand = Chosen("axe");
        var q = FocusMorph.Quote(FocusChains.TryGet("axe")!, meta, 4, 1.0);
        Assert.Equal(FocusQuoteKind.KeepOnly, q.Kind);
        Assert.Equal(FocusBuyResult.Success, FocusPurchase.TryBuy(meta, hand, q));
        Assert.Equal(2000 - 875, meta.JunimoPoints);
        Assert.Equal(4, meta.OwnedUpgrades.Count);
    }
}
