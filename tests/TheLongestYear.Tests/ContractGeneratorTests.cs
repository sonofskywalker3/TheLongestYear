using System;
using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class ContractGeneratorTests
{
    private static List<CcItem> SampleCc() => new()
    {
        new CcItem("Parsnip", Theme.Farming, Rarity.Common, new HashSet<Season> { Season.Spring }),
        new CcItem("RedCabbage", Theme.Farming, Rarity.VeryRare, new HashSet<Season> { Season.Summer }),
        new CcItem("Salmon", Theme.Fishing, Rarity.Uncommon, new HashSet<Season> { Season.Fall }),
        new CcItem("CopperBar", Theme.Mining, Rarity.Common, new HashSet<Season> { Season.Spring, Season.Summer, Season.Fall, Season.Winter }),
        new CcItem("Daffodil", Theme.Foraging, Rarity.Common, new HashSet<Season> { Season.Spring }),
    };

    private static Dictionary<string, Contract> ByItem(YearPlan plan)
    {
        var map = new Dictionary<string, Contract>();
        foreach (var c in plan.Contracts)
            foreach (var id in c.RequiredItemIds)
                map[id] = c;
        return map;
    }

    [Fact]
    public void Plan_has_twenty_contracts()
    {
        var plan = new ContractGenerator().Generate(SampleCc(), seed: 1);
        Assert.Equal(20, plan.Contracts.Count);
    }

    [Fact]
    public void Every_item_is_assigned_exactly_once()
    {
        var cc = SampleCc();
        var plan = new ContractGenerator().Generate(cc, seed: 1);
        var assigned = plan.Contracts.SelectMany(c => c.RequiredItemIds).ToList();

        Assert.Equal(cc.Count, assigned.Count);
        Assert.Equal(cc.Select(i => i.Id).OrderBy(x => x),
                     assigned.OrderBy(x => x));
    }

    [Fact]
    public void Each_item_lands_in_an_obtainable_season_and_its_theme()
    {
        var cc = SampleCc();
        var plan = new ContractGenerator().Generate(cc, seed: 7);
        var byItem = ByItem(plan);

        foreach (var item in cc)
        {
            var contract = byItem[item.Id];
            Assert.Equal(item.Theme, contract.Theme);
            Assert.True(item.IsObtainableIn(contract.Season),
                $"{item.Id} placed in {contract.Season} but is only obtainable in [{string.Join(",", item.ObtainableSeasons)}]");
        }
    }

    [Fact]
    public void Placement_is_seed_independent()
    {
        // Spec 2026-05-26 round 2: placement is deterministic (the seed param is kept for API
        // compatibility with the reroll plumbing but doesn't influence which item lands where).
        var a = new ContractGenerator().Generate(SampleCc(), seed: 42);
        var b = new ContractGenerator().Generate(SampleCc(), seed: 0);
        Assert.Equal(Serialize(a), Serialize(b));
    }

    [Fact]
    public void Contracts_carry_their_theme_modifiers()
    {
        var plan = new ContractGenerator().Generate(SampleCc(), seed: 1);
        var mining = plan.Get(Season.Spring, Theme.Mining);
        var (bonus, liability) = ThemeModifiers.For(Theme.Mining);
        Assert.Equal(bonus, mining.BonusId);
        Assert.Equal(liability, mining.LiabilityId);
    }

    [Fact]
    public void Multi_season_items_spread_across_seasons()
    {
        // 8 year-round common items, same theme. New policy distributes them 2/2/2/2 across
        // seasons rather than stacking them all into one (the previous round's empty-Spring bug).
        var items = new List<CcItem>();
        for (int i = 0; i < 8; i++)
            items.Add(new CcItem(
                $"common-{i}", Theme.Foraging, Rarity.Common,
                new HashSet<Season> { Season.Spring, Season.Summer, Season.Fall, Season.Winter }));

        var plan = new ContractGenerator().Generate(items, seed: 1);

        foreach (Season s in Enum.GetValues(typeof(Season)))
        {
            int count = plan.Get(s, Theme.Foraging).RequiredItemIds.Count;
            Assert.True(count >= 1,
                $"{s} Foraging should have at least one item; got {count}.");
        }
    }

    [Fact]
    public void Two_season_item_lands_in_the_earlier_season_when_both_are_empty()
    {
        // User rule (2026-05-26): "easy fish that are available in 2 seasons should still be
        // required for the first." A Spring|Fall fish with no prior load lands in Spring.
        var items = new List<CcItem>
        {
            new CcItem("fish-A", Theme.Fishing, Rarity.Common,
                new HashSet<Season> { Season.Spring, Season.Fall })
        };
        var plan = new ContractGenerator().Generate(items, seed: 1);
        Assert.Contains("fish-A", plan.Get(Season.Spring, Theme.Fishing).RequiredItemIds);
    }

    [Fact]
    public void Common_first_sort_pushes_rares_to_later_seasons_when_competing()
    {
        // User rule (2026-05-26): "copper Spring, iron Summer, gold Fall, iridium Winter."
        // Common-first sort + least-loaded-then-earliest placement produces exactly that
        // distribution when four year-round items of ascending rarity share a theme.
        var items = new List<CcItem>
        {
            new CcItem("copper", Theme.Mining, Rarity.Common,   AllYear()),
            new CcItem("iron",   Theme.Mining, Rarity.Uncommon, AllYear()),
            new CcItem("gold",   Theme.Mining, Rarity.Rare,     AllYear()),
            new CcItem("irid",   Theme.Mining, Rarity.VeryRare, AllYear()),
        };
        var plan = new ContractGenerator().Generate(items, seed: 1);
        var byItem = ByItem(plan);
        Assert.Equal(Season.Spring, byItem["copper"].Season);
        Assert.Equal(Season.Summer, byItem["iron"].Season);
        Assert.Equal(Season.Fall,   byItem["gold"].Season);
        Assert.Equal(Season.Winter, byItem["irid"].Season);
    }

    [Fact]
    public void Spring_contracts_stay_within_cap_for_multi_season_items()
    {
        var items = new List<CcItem>();
        for (int i = 0; i < 20; i++)
            items.Add(new CcItem(
                $"flex-{i}", Theme.Foraging, Rarity.Common, AllYear()));

        var plan = new ContractGenerator().Generate(items, seed: 1);
        int springForagingCount = plan.Get(Season.Spring, Theme.Foraging).RequiredItemIds.Count;
        Assert.True(springForagingCount <= 4,
            $"Spring Foraging should respect the cap (4); got {springForagingCount}.");
    }

    [Fact]
    public void Single_season_items_override_cap_when_needed()
    {
        var items = new List<CcItem>();
        for (int i = 0; i < 6; i++)
            items.Add(new CcItem(
                $"spring-only-{i}", Theme.Foraging, Rarity.Common,
                new HashSet<Season> { Season.Spring }));

        var plan = new ContractGenerator().Generate(items, seed: 1);
        Assert.Equal(6, plan.Get(Season.Spring, Theme.Foraging).RequiredItemIds.Count);
    }

    [Fact]
    public void Multi_season_items_with_no_room_force_to_earliest_obtainable()
    {
        // 30 year-round common items, default caps {4,5,6,9}. Total cap = 24, so 6 items can't
        // fit under any cap. Pre-v3 they'd be dropped; now they overflow to the earliest
        // obtainable season (Spring) so Win is reachable.
        var items = new List<CcItem>();
        for (int i = 0; i < 30; i++)
            items.Add(new CcItem(
                $"flex-{i}", Theme.Foraging, Rarity.Common, AllYear()));

        var plan = new ContractGenerator().Generate(items, seed: 1);
        int totalAssigned = plan.Contracts.Sum(c => c.RequiredItemIds.Count);
        Assert.Equal(30, totalAssigned);
    }

    [Fact]
    public void Custom_cap_array_is_respected()
    {
        var items = new List<CcItem>();
        for (int i = 0; i < 12; i++)
            items.Add(new CcItem(
                $"flex-{i}", Theme.Mining, Rarity.Common, AllYear()));

        var customCaps = new[] { 2, 2, 2, 6 };
        var plan = new ContractGenerator(customCaps).Generate(items, seed: 1);
        Assert.True(plan.Get(Season.Spring, Theme.Mining).RequiredItemIds.Count <= 2);
        Assert.True(plan.Get(Season.Summer, Theme.Mining).RequiredItemIds.Count <= 2);
        Assert.True(plan.Get(Season.Fall, Theme.Mining).RequiredItemIds.Count <= 2);
    }

    [Fact]
    public void Override_pins_an_item_to_its_pinned_season()
    {
        var items = new List<CcItem>
        {
            new CcItem("(O)24", Theme.Farming, Rarity.Common,
                new HashSet<Season> { Season.Spring, Season.Summer, Season.Fall, Season.Winter })
        };
        var overrides = new Dictionary<string, Season> { ["(O)24"] = Season.Fall };
        var plan = new ContractGenerator(new[] { 4, 5, 6, 9 }, overrides).Generate(items, seed: 1);

        Assert.Contains("(O)24", plan.Get(Season.Fall, Theme.Farming).RequiredItemIds);
        Assert.DoesNotContain("(O)24", plan.Get(Season.Spring, Theme.Farming).RequiredItemIds);
    }

    [Fact]
    public void Override_ignored_when_pinned_season_is_not_obtainable()
    {
        // Pinning a Fall-only item to Spring is invalid (not obtainable in Spring); the algorithm
        // ignores the bad pin and falls back to the standard placement.
        var items = new List<CcItem>
        {
            new CcItem("ginger", Theme.Foraging, Rarity.Common,
                new HashSet<Season> { Season.Fall })
        };
        var overrides = new Dictionary<string, Season> { ["ginger"] = Season.Spring };
        var plan = new ContractGenerator(new[] { 4, 5, 6, 9 }, overrides).Generate(items, seed: 1);

        Assert.Contains("ginger", plan.Get(Season.Fall, Theme.Foraging).RequiredItemIds);
    }

    private static HashSet<Season> AllYear()
        => new HashSet<Season> { Season.Spring, Season.Summer, Season.Fall, Season.Winter };

    private static string Serialize(YearPlan plan)
        => string.Join("|", plan.Contracts
            .OrderBy(c => (int)c.Season).ThenBy(c => (int)c.Theme)
            .Select(c => $"{c.Season}.{c.Theme}:{string.Join(",", c.RequiredItemIds.OrderBy(x => x))}"));
}
