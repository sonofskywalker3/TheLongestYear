using System.Linq;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>User rulings 2026-08-21 on the hard-gate obtainability upgrades (JP budget note:
/// a strong loop banks ~8–9.5k JP).</summary>
public class UpgradePricingTests
{
    [Fact]
    public void Red_cabbage_roll_costs_5000()
    {
        var def = UpgradeCatalog.TryGet("cult_red_cabbage");
        Assert.NotNull(def);
        Assert.Equal(5000, def!.Cost);
        Assert.Equal(UpgradeCategory.Obtainability, def.Category);
    }

    [Fact]
    public void Pierre_year2_seeds_is_the_10000_sure_thing_with_no_prerequisite()
    {
        var def = UpgradeCatalog.TryGet("pierre_year2_seeds");
        Assert.NotNull(def);
        Assert.Equal(10000, def!.Cost);
        Assert.Equal(UpgradeCategory.Obtainability, def.Category);
        Assert.Null(def.PrerequisiteId);
    }

    [Fact]
    public void Starfruit_cultivation_is_gone()
    {
        Assert.Null(UpgradeCatalog.TryGet("cult_starfruit"));
        Assert.DoesNotContain(UpgradeCatalog.All, u => u.Id.Contains("starfruit"));
    }
}
