using System;
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
    private static Contract Make(int gate, IEnumerable<string>? bonus = null, params string[] pool)
        => new Contract(
            Season.Spring, Theme.Farming, pool, gate,
            bonus ?? Array.Empty<string>(),
            "crop_growth_up", "forage_drops_off");

    [Fact]
    public void Contract_is_satisfied_when_gate_count_of_pool_donated()
    {
        var c = Make(gate: 2, pool: new[] { "Parsnip", "Potato", "Daffodil", "Tulip" });
        var donated = new HashSet<string> { "Parsnip", "Potato" };
        Assert.True(c.IsSatisfiedBy(donated));
    }

    [Fact]
    public void Contract_satisfied_with_one_extra_pool_item_donated()
    {
        var c = Make(gate: 2, pool: new[] { "Parsnip", "Potato", "Daffodil", "Tulip" });
        var donated = new HashSet<string> { "Parsnip", "Potato", "Tulip" };
        Assert.True(c.IsSatisfiedBy(donated));
    }

    [Fact]
    public void Contract_not_satisfied_below_gate_count()
    {
        var c = Make(gate: 2, pool: new[] { "Parsnip", "Potato", "Daffodil" });
        var donated = new HashSet<string> { "Parsnip" };
        Assert.False(c.IsSatisfiedBy(donated));
    }

    [Fact]
    public void Donations_outside_pool_dont_count()
    {
        var c = Make(gate: 2, pool: new[] { "Parsnip", "Potato" });
        var donated = new HashSet<string> { "Parsnip", "Salmon", "CopperBar" };   // only Parsnip is in pool
        Assert.False(c.IsSatisfiedBy(donated));
    }

    [Fact]
    public void Empty_pool_is_trivially_satisfied()
    {
        var c = Make(gate: 0);   // pool also empty
        Assert.True(c.IsSatisfiedBy(new HashSet<string>()));
    }

    [Fact]
    public void Gate_caps_at_pool_size()
    {
        // Configured gate of 5 but only 2 items in pool -> effective gate is 2.
        var c = Make(gate: 5, pool: new[] { "Parsnip", "Potato" });
        Assert.Equal(2, c.GateRequirement);
        var donated = new HashSet<string> { "Parsnip", "Potato" };
        Assert.True(c.IsSatisfiedBy(donated));
    }

    [Fact]
    public void Bonus_items_default_to_empty_when_omitted()
    {
        var c = Make(gate: 1, pool: new[] { "Parsnip" });
        Assert.Empty(c.BonusItemIds);
    }

    [Fact]
    public void Bonus_items_carry_through_constructor()
    {
        var c = Make(gate: 1, bonus: new[] { "Daffodil" }, pool: new[] { "Parsnip", "Daffodil" });
        Assert.Contains("Daffodil", c.BonusItemIds);
    }
}
