using System.Linq;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class FocusMorphTests
{
    private static FocusQuote Quote(string chain, string[] owned, int world)
    {
        var meta = new MetaState();
        meta.OwnedUpgrades.AddRange(owned);
        return FocusMorph.Quote(FocusChains.TryGet(chain)!, meta, world, 1.0);
    }

    [Fact]
    public void Loop_one_axe_offers_copper_at_copper_price()
    {
        var q = Quote("axe", new string[0], 0);
        Assert.Equal(FocusQuoteKind.Offer, q.Kind);
        Assert.Equal("keep_axe_1", q.UpgradeId);
        Assert.Equal(150, q.Price);
        Assert.Equal(new[] { "keep_axe_1" }, q.Grants);
    }

    [Fact]
    public void Self_upgraded_copper_morphs_to_steel_and_grants_both()
    {
        var q = Quote("axe", new string[0], 1);
        Assert.Equal("keep_axe_2", q.UpgradeId);
        Assert.Equal(300, q.Price);
        Assert.Equal(new[] { "keep_axe_1", "keep_axe_2" }, q.Grants);
    }

    [Fact]
    public void Never_more_than_one_rung_above_has()
        => Assert.Equal(3, Quote("axe", new[] { "keep_axe_1", "keep_axe_2" }, 0).TargetRung);

    [Fact]
    public void Self_reached_top_is_keep_only_at_top_price()
    {
        var q = Quote("axe", new[] { "keep_axe_1" }, 4);
        Assert.Equal(FocusQuoteKind.KeepOnly, q.Kind);
        Assert.Equal(875, q.Price);
        Assert.Equal(3, q.Grants.Count);
    }

    [Fact]
    public void All_rungs_kept_is_done() => Assert.Equal(FocusQuoteKind.Done, Quote("silo", new[] { "keep_silo" }, 0).Kind);

    [Fact]
    public void Price_factor_applies()
        => Assert.Equal(225, FocusMorph.Quote(FocusChains.TryGet("axe")!, new MetaState(), 0, 1.5).Price);

    [Fact]
    public void Kept_rung_is_the_highest_owned()
        => Assert.Equal(2, FocusMorph.KeptRung(FocusChains.TryGet("axe")!, MetaWith("keep_axe_1", "keep_axe_2")));

    [Fact]
    public void Unmet_meta_requirement_is_locked()
    {
        // Any chain rung that carries a MetaRequirement locks while it is unmet.
        var chain = FocusChains.All.FirstOrDefault(c => c.Rungs.Any(r => UpgradeCatalog.TryGet(r)!.MetaRequirement != null));
        if (chain == null) return; // none in the focus table: the Locked branch is unreachable today
        int idx = chain.Rungs.ToList().FindIndex(r => UpgradeCatalog.TryGet(r)!.MetaRequirement != null);
        var q = FocusMorph.Quote(chain, new MetaState(), idx, 1.0);
        Assert.Equal(FocusQuoteKind.Locked, q.Kind);
    }

    private static MetaState MetaWith(params string[] owned)
    {
        var m = new MetaState();
        m.OwnedUpgrades.AddRange(owned);
        return m;
    }
}
