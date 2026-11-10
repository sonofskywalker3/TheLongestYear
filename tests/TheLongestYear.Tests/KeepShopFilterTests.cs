using System;
using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

[Collection("i18n")]
public class KeepShopFilterTests
{
    public KeepShopFilterTests(I18nFixture fixture) => I18nFixture.InstallGlobalProvider();

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
    public void Keep_horse_needs_a_stable_this_run()
    {
        var meta = new MetaState();
        var withoutStable = KeepShopFilter
            .BuyableInCategory(UpgradeCategory.Animals, meta, _ => false)   // reach always false
            .Select(d => d.Id).ToList();
        Assert.DoesNotContain("early_horse", withoutStable);

        var withStable = KeepShopFilter
            .BuyableInCategory(UpgradeCategory.Animals, meta,
                r => r == "building:Stable")
            .Select(d => d.Id).ToList();
        Assert.Contains("early_horse", withStable);
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
        meta.OwnedUpgrades.Add("keep_silo");

        var leaves = KeepShopFilter
            .OwnedLeavesInCategory(UpgradeCategory.Animals, meta, _ => true)
            .Select(d => d.Id).ToList();

        Assert.Contains("keep_silo", leaves);
    }

    [Fact]
    public void Book_keep_appears_only_once_the_book_was_read_this_loop()
    {
        var meta = new MetaState();
        var reached = new Dictionary<string, int>();
        List<string> Buyable() => KeepShopFilter
            .BuyableInCategory(UpgradeCategory.Carryover, meta, Reach(reached))
            .Select(d => d.Id).ToList();

        Assert.DoesNotContain("keep_book_speed", Buyable());
        reached["book:Book_Speed"] = 1;
        Assert.Contains("keep_book_speed", Buyable());
        Assert.DoesNotContain("keep_book_speed2", Buyable());   // chained on pt. 1

        meta.OwnedUpgrades.Add("keep_book_speed");
        reached["book:Book_Speed2"] = 1;
        Assert.Contains("keep_book_speed2", Buyable());
        Assert.DoesNotContain("keep_book_speed", Buyable());    // owned rows are not buyable
    }

    // --- PinnedKeeps: pins belong to a chain and sort its row to the top of a tab -----------

    [Fact]
    public void Pinning_any_tier_pins_the_whole_chain_by_its_root()
    {
        var meta = new MetaState();

        Assert.True(PinnedKeeps.Toggle(meta, "keep_watering_can_3"));

        Assert.Equal(new[] { "keep_watering_can_1" }, meta.PinnedUpgrades);
        Assert.True(PinnedKeeps.IsPinned(meta, "keep_watering_can_2"));
        Assert.False(PinnedKeeps.IsPinned(meta, "keep_hoe_1"));

        Assert.False(PinnedKeeps.Toggle(meta, "keep_watering_can_2"));   // same chain: unpins
        Assert.Empty(meta.PinnedUpgrades);
    }

    [Fact]
    public void Pinned_rows_come_first_and_both_groups_keep_their_order()
    {
        var meta = new MetaState();
        meta.PinnedUpgrades.Add("keep_hoe_1");
        meta.PinnedUpgrades.Add("keep_silo");
        string?[] rows = { null, "keep_watering_can_2", "keep_silo", "keep_axe_1", "keep_hoe_2" };

        List<string?> sorted = PinnedKeeps.PinnedFirst(rows, id => id, meta);

        Assert.Equal(new[] { "keep_silo", "keep_hoe_2", null, "keep_watering_can_2", "keep_axe_1" }, sorted);
    }

    // --- PinnedBudget: what the banked JP buys of the pinned chains ---------------------------

    [Fact]
    public void Budget_buys_next_tiers_cheapest_first_and_counts_gated_chains_as_locked()
    {
        var meta = new MetaState { JunimoPoints = 400 };
        meta.OwnedUpgrades.AddRange(new[] { "keep_hoe_1", "keep_pickaxe_1", "keep_pickaxe_2", "keep_pickaxe_3", "keep_pickaxe_4" });
        meta.PinnedUpgrades.AddRange(new[] { "keep_hoe_1", "keep_axe_1", "keep_watering_can_1", "keep_pickaxe_1", "no_such_keep" });
        var reached = new Dictionary<string, int> { ["tool:hoe"] = 2, ["tool:axe"] = 1 };   // the can was never upgraded

        PinnedBudgetResult budget = PinnedBudget.Compute(meta, 1.0, Reach(reached));

        // Axe I (150) is bought on paper, Hoe II (300) is 50 short; the can is locked; the pickaxe
        // chain is fully owned and the unknown pin is ignored.
        Assert.Equal(new PinnedBudgetResult(Affordable: 1, Total: 2, Locked: 1, ShortfallJp: 50), budget);
        Assert.Equal(400, meta.JunimoPoints);
        Assert.Equal(5, meta.OwnedUpgrades.Count);
    }

    [Fact]
    public void Budget_reprices_gifts_of_the_junimos_after_each_paper_buy()
    {
        var meta = new MetaState { JunimoPoints = 2500 };
        meta.PinnedUpgrades.AddRange(new[] { "keep_greenhouse", "keep_quarry_bridge" });
        var reached = new Dictionary<string, int> { ["room:Pantry"] = 1, ["room:CraftsRoom"] = 1 };

        // The first Gift costs 1000, which lifts the second to 2000: 500 short, not affordable.
        Assert.Equal(new PinnedBudgetResult(1, 2, 0, 500), PinnedBudget.Compute(meta, 1.0, Reach(reached)));

        meta.JunimoPoints = 3000;
        Assert.Equal(new PinnedBudgetResult(2, 2, 0, 0), PinnedBudget.Compute(meta, 1.0, Reach(reached)));
    }

    [Theory]
    [InlineData(1, 3, 0, 120, new[] { "You can afford 1 of your 3 pinned upgrades. You need 120 more JP for the next one." })]
    [InlineData(0, 2, 1, 40, new[] { "You can afford 0 of your 2 pinned upgrades. You need 40 more JP for the next one.", "1 pinned upgrades are still locked." })]
    [InlineData(2, 2, 0, 0, new[] { "You can afford all 2 of your pinned upgrades." })]
    [InlineData(0, 0, 3, 0, new[] { "Your 3 pinned upgrades are still locked." })]
    [InlineData(0, 0, 0, 0, new string[0])]
    public void Budget_line_wording_follows_the_counts(int affordable, int total, int locked, long shortfall, string[] expected)
        => Assert.Equal(expected, PinnedBudget.Lines(new PinnedBudgetResult(affordable, total, locked, shortfall)));

    // --- KeepLadder and tab counts ------------------------------------------------------------

    [Fact]
    public void Ladder_lists_the_next_unowned_tiers_at_todays_prices_and_skips_single_keeps()
    {
        var meta = new MetaState();
        meta.OwnedUpgrades.Add("keep_hoe_1");

        KeepLadderView ladder = KeepLadder.For("keep_hoe_2", meta, 2.0)!;

        Assert.Equal(1, ladder.Owned);
        Assert.Equal(4, ladder.Total);
        Assert.Equal(new[] { ("keep_hoe_2", 600L), ("keep_hoe_3", 1050L), ("keep_hoe_4", 1750L) },
            ladder.Next.Select(t => (t.Def.Id, t.Cost)));
        Assert.Equal(3400, ladder.LeftToBuy);
        Assert.Equal(KeepLadder.ShownTiers, KeepLadder.For(UpgradeCatalog.HerdBookPrefix + "1", meta, 1.0)!.Next.Count);
        Assert.Null(KeepLadder.For("keep_greenhouse", meta, 1.0));
    }

    [Fact]
    public void Category_progress_counts_owned_definitions_over_all_of_them()
    {
        var meta = new MetaState();
        meta.OwnedUpgrades.AddRange(new[] { "keep_greenhouse", "keep_minecarts", "keep_hoe_1" });

        Assert.Equal((2, 5), KeepShopFilter.CategoryProgress(UpgradeCategory.Gifts, meta));
    }
}
