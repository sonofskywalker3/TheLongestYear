using System;
using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class KeepShopFilterTests
{
    // Build a reachMet predicate from a dict of metric:key -> reached value.
    private static Func<string?, bool> Reach(Dictionary<string, int> reached) => req =>
    {
        RunReachRequirement? r = RunReachRequirement.Parse(req);
        if (r == null) return true;                       // no requirement -> always met
        string key = r.Key == null ? r.Metric : $"{r.Metric}:{r.Key}";
        return r.IsMet(reached.TryGetValue(key, out int v) ? v : 0);
    };

    [Fact]
    public void Watering_can_chain_reveals_one_tier_at_a_time_gated_by_reach()
    {
        // This run reached a Steel watering can (tier 2) and a Copper hoe (tier 1).
        var reached = new Dictionary<string, int>
        {
            ["tool:watering_can"] = 2,
            ["tool:hoe"] = 1,
        };
        var meta = new MetaState();   // owns nothing yet

        List<string> Buyable() => KeepShopFilter
            .BuyableInCategory(UpgradeCategory.Loadout, meta, Reach(reached))
            .Select(d => d.Id).ToList();

        Assert.Contains("keep_watering_can_1", Buyable());
        Assert.Contains("keep_hoe_1", Buyable());
        Assert.DoesNotContain("keep_watering_can_2", Buyable());

        meta.OwnedUpgrades.Add("keep_watering_can_1");
        Assert.Contains("keep_watering_can_2", Buyable());
        Assert.DoesNotContain("keep_watering_can_1", Buyable());   // owned -> not buyable

        meta.OwnedUpgrades.Add("keep_watering_can_2");
        Assert.DoesNotContain("keep_watering_can_3", Buyable());

        reached["tool:watering_can"] = 3;
        Assert.Contains("keep_watering_can_3", Buyable());
    }

    [Fact]
    public void Non_reach_upgrades_are_unaffected_by_reach()
    {
        var meta = new MetaState();
        var buyable = KeepShopFilter
            .BuyableInCategory(UpgradeCategory.Efficiency, meta, _ => false)   // reach always false
            .Select(d => d.Id).ToList();
        Assert.Contains("early_horse", buyable);
    }
}
