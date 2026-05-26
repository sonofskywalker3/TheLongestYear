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

        Assert.Equal(cc.Count, assigned.Count);                       // no duplicates, none dropped
        Assert.Equal(cc.Select(i => i.Id).OrderBy(x => x),
                     assigned.OrderBy(x => x));                       // exactly the CC set
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
    public void Same_seed_produces_an_identical_plan()
    {
        var a = new ContractGenerator().Generate(SampleCc(), seed: 42);
        var b = new ContractGenerator().Generate(SampleCc(), seed: 42);

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
    public void Common_items_lean_toward_earlier_seasons_when_year_round()
    {
        // 8 year-round common items of the same theme.
        var items = new List<CcItem>();
        for (int i = 0; i < 8; i++)
            items.Add(new CcItem(
                $"common-{i}", Theme.Foraging, Rarity.Common,
                new HashSet<Season> { Season.Spring, Season.Summer, Season.Fall, Season.Winter }));

        var plan = new ContractGenerator().Generate(items, seed: 1);
        var byItem = ByItem(plan);

        // Count how many ended up in the Spring half (Spring + Summer) vs. Fall half (Fall + Winter).
        int earlyCount = items.Count(i => (int)byItem[i.Id].Season < 2);
        int lateCount  = items.Count(i => (int)byItem[i.Id].Season >= 2);

        Assert.True(earlyCount >= lateCount,
            $"Common year-round items should bias toward earlier seasons; got early={earlyCount}, late={lateCount}.");
    }

    [Fact]
    public void Very_rare_items_lean_toward_later_seasons_when_year_round()
    {
        // 8 year-round VeryRare items of the same theme.
        var items = new List<CcItem>();
        for (int i = 0; i < 8; i++)
            items.Add(new CcItem(
                $"vrare-{i}", Theme.Mining, Rarity.VeryRare,
                new HashSet<Season> { Season.Spring, Season.Summer, Season.Fall, Season.Winter }));

        var plan = new ContractGenerator().Generate(items, seed: 1);
        var byItem = ByItem(plan);

        int earlyCount = items.Count(i => (int)byItem[i.Id].Season < 2);
        int lateCount  = items.Count(i => (int)byItem[i.Id].Season >= 2);

        Assert.True(lateCount >= earlyCount,
            $"VeryRare year-round items should bias toward later seasons; got early={earlyCount}, late={lateCount}.");
    }

    [Fact]
    public void Spring_contracts_stay_within_cap_for_multi_season_items()
    {
        // 20 multi-season common items — without a cap they'd flood Spring.
        var items = new List<CcItem>();
        for (int i = 0; i < 20; i++)
            items.Add(new CcItem(
                $"flex-{i}", Theme.Foraging, Rarity.Common,
                new HashSet<Season> { Season.Spring, Season.Summer, Season.Fall, Season.Winter }));

        var plan = new ContractGenerator().Generate(items, seed: 1);
        int springForagingCount = plan.Get(Season.Spring, Theme.Foraging).RequiredItemIds.Count;
        Assert.True(springForagingCount <= 4,
            $"Spring Foraging should respect the cap (4); got {springForagingCount}.");
    }

    [Fact]
    public void Single_season_items_override_cap_when_needed()
    {
        // 6 Spring-only common items — must all land in Spring even though cap is 4.
        var items = new List<CcItem>();
        for (int i = 0; i < 6; i++)
            items.Add(new CcItem(
                $"spring-only-{i}", Theme.Foraging, Rarity.Common,
                new HashSet<Season> { Season.Spring }));

        var plan = new ContractGenerator().Generate(items, seed: 1);
        int springForagingCount = plan.Get(Season.Spring, Theme.Foraging).RequiredItemIds.Count;
        Assert.Equal(6, springForagingCount); // overflow accepted — items can't move
    }

    [Fact]
    public void Custom_cap_array_is_respected()
    {
        // Lower-than-default caps should still kick in.
        var items = new List<CcItem>();
        for (int i = 0; i < 12; i++)
            items.Add(new CcItem(
                $"flex-{i}", Theme.Mining, Rarity.Common,
                new HashSet<Season> { Season.Spring, Season.Summer, Season.Fall, Season.Winter }));

        var customCaps = new[] { 2, 2, 2, 6 };
        var plan = new ContractGenerator(customCaps).Generate(items, seed: 1);
        Assert.True(plan.Get(Season.Spring, Theme.Mining).RequiredItemIds.Count <= 2);
        Assert.True(plan.Get(Season.Summer, Theme.Mining).RequiredItemIds.Count <= 2);
        Assert.True(plan.Get(Season.Fall, Theme.Mining).RequiredItemIds.Count <= 2);
    }

    private static string Serialize(YearPlan plan)
        => string.Join("|", plan.Contracts
            .OrderBy(c => (int)c.Season).ThenBy(c => (int)c.Theme)
            .Select(c => $"{c.Season}.{c.Theme}:{string.Join(",", c.RequiredItemIds.OrderBy(x => x))}"));
}
