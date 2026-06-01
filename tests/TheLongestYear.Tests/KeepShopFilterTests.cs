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

    // --- OwnedLeavesInCategory: the green "what you already own" rows ----------------
    // A keep is shown as an owned leaf when it's owned, it's the TOP owned tier in its
    // chain, and its next tier is not currently buyable (so the buyable list doesn't
    // already represent the chain).

    [Fact]
    public void Owned_leaf_hidden_when_next_tier_is_buyable()
    {
        // Owns Copper can; this run reached Steel (tier 2) so keep_watering_can_2 is buyable.
        var reached = new Dictionary<string, int> { ["tool:watering_can"] = 2 };
        var meta = new MetaState();
        meta.OwnedUpgrades.Add("keep_watering_can_1");

        var leaves = KeepShopFilter
            .OwnedLeavesInCategory(UpgradeCategory.Loadout, meta, Reach(reached))
            .Select(d => d.Id).ToList();

        // The buyable _2 row already implies _1 is owned — don't double up.
        Assert.DoesNotContain("keep_watering_can_1", leaves);
    }

    [Fact]
    public void Owned_leaf_shown_when_next_tier_not_reachable()
    {
        // Owns Copper can but this run only reached tier 1, so _2 is NOT buyable.
        var reached = new Dictionary<string, int> { ["tool:watering_can"] = 1 };
        var meta = new MetaState();
        meta.OwnedUpgrades.Add("keep_watering_can_1");

        var leaves = KeepShopFilter
            .OwnedLeavesInCategory(UpgradeCategory.Loadout, meta, Reach(reached))
            .Select(d => d.Id).ToList();

        Assert.Contains("keep_watering_can_1", leaves);
    }

    [Fact]
    public void Owned_leaf_shows_only_the_top_owned_tier_in_a_chain()
    {
        // Owns Copper + Steel; reached tier 2 so _3 is not buyable. Only _2 should show.
        var reached = new Dictionary<string, int> { ["tool:watering_can"] = 2 };
        var meta = new MetaState();
        meta.OwnedUpgrades.Add("keep_watering_can_1");
        meta.OwnedUpgrades.Add("keep_watering_can_2");

        var leaves = KeepShopFilter
            .OwnedLeavesInCategory(UpgradeCategory.Loadout, meta, Reach(reached))
            .Select(d => d.Id).ToList();

        Assert.DoesNotContain("keep_watering_can_1", leaves);
        Assert.Contains("keep_watering_can_2", leaves);
    }

    [Fact]
    public void Owned_standalone_with_no_successor_is_always_a_leaf()
    {
        var meta = new MetaState();
        meta.OwnedUpgrades.Add("early_horse");

        var leaves = KeepShopFilter
            .OwnedLeavesInCategory(UpgradeCategory.Efficiency, meta, _ => true)
            .Select(d => d.Id).ToList();

        Assert.Contains("early_horse", leaves);
    }
}
