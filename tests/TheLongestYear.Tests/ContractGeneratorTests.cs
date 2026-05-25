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

    private static string Serialize(YearPlan plan)
        => string.Join("|", plan.Contracts
            .OrderBy(c => (int)c.Season).ThenBy(c => (int)c.Theme)
            .Select(c => $"{c.Season}.{c.Theme}:{string.Join(",", c.RequiredItemIds.OrderBy(x => x))}"));
}
