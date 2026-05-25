using System.Collections.Generic;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class CcItemTests
{
    [Fact]
    public void CcItem_exposes_its_properties()
    {
        var item = new CcItem("RedCabbage", Theme.Farming, Rarity.VeryRare, new HashSet<Season> { Season.Summer });

        Assert.Equal("RedCabbage", item.Id);
        Assert.Equal(Theme.Farming, item.Theme);
        Assert.Equal(Rarity.VeryRare, item.Rarity);
        Assert.True(item.IsObtainableIn(Season.Summer));
        Assert.False(item.IsObtainableIn(Season.Winter));
    }

    [Fact]
    public void Season_maps_to_month_index()
    {
        Assert.Equal(0, (int)Season.Spring);
        Assert.Equal(3, (int)Season.Winter);
        Assert.Equal(Season.Fall, SeasonExtensions.FromMonthIndex(2));
    }
}

public class ContractSatisfactionTests
{
    private static Contract Make(params string[] required)
        => new Contract(Season.Spring, Theme.Farming, required, "crop_growth_up", "forage_drops_off");

    [Fact]
    public void Contract_is_satisfied_when_all_required_ids_donated()
    {
        var c = Make("Parsnip", "Potato");
        var donated = new HashSet<string> { "Parsnip", "Potato", "Daffodil" };
        Assert.True(c.IsSatisfiedBy(donated));
    }

    [Fact]
    public void Contract_is_not_satisfied_when_an_item_is_missing()
    {
        var c = Make("Parsnip", "Potato");
        var donated = new HashSet<string> { "Parsnip" };
        Assert.False(c.IsSatisfiedBy(donated));
    }

    [Fact]
    public void Empty_contract_is_always_satisfied()
    {
        var c = Make();
        Assert.True(c.IsSatisfiedBy(new HashSet<string>()));
    }
}
