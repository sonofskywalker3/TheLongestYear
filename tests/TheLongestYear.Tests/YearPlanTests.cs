using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class YearPlanTests
{
    private static YearPlan TwentyEmptyContracts()
    {
        var contracts = new List<Contract>();
        foreach (Season s in System.Enum.GetValues(typeof(Season)))
            foreach (Theme t in System.Enum.GetValues(typeof(Theme)))
                contracts.Add(new Contract(s, t, new string[0], "b", "l"));
        return new YearPlan(contracts);
    }

    [Fact]
    public void Get_returns_the_contract_for_a_season_and_theme()
    {
        var plan = TwentyEmptyContracts();
        var c = plan.Get(Season.Fall, Theme.Mining);
        Assert.Equal(Season.Fall, c.Season);
        Assert.Equal(Theme.Mining, c.Theme);
    }

    [Fact]
    public void ForSeason_returns_the_five_theme_contracts()
    {
        var plan = TwentyEmptyContracts();
        var fall = plan.ForSeason(Season.Fall).ToList();
        Assert.Equal(5, fall.Count);
        Assert.Equal(new[] { Theme.Foraging, Theme.Farming, Theme.Fishing, Theme.Mining, Theme.Mixed },
            fall.Select(c => c.Theme).OrderBy(t => (int)t));
    }

    [Fact]
    public void Constructor_rejects_a_plan_missing_a_season_theme_slot()
    {
        var contracts = new List<Contract> { new Contract(Season.Spring, Theme.Mining, new string[0], "b", "l") };
        Assert.Throws<System.ArgumentException>(() => new YearPlan(contracts));
    }
}
