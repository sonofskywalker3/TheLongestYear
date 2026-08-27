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

public class UpgradeDifficultyPricingTests
{
    private static UpgradeDefinition Def(long cost)
        => new("test_upgrade", UpgradeCategory.Efficiency, cost);

    [Theory]
    [InlineData(1.0, 100)]
    [InlineData(0.75, 75)]
    [InlineData(1.25, 125)]
    [InlineData(1.5, 150)]
    public void Cost_Scales_By_The_Factor(double factor, long expected)
        => Assert.Equal(expected, UpgradePricing.EffectiveCost(Def(100), factor));

    /// <summary>Zero times anything is zero, so a free upgrade stays free at every step.</summary>
    [Fact]
    public void A_Free_Upgrade_Stays_Free()
        => Assert.Equal(0, UpgradePricing.EffectiveCost(Def(0), 1.5));

    [Fact]
    public void Rounding_Is_Away_From_Zero()
        => Assert.Equal(126, UpgradePricing.EffectiveCost(Def(101), 1.25));   // 126.25 -> 126

    [Fact]
    public void The_Profile_Overload_Uses_The_Shrine_Price_Factor()
    {
        var profile = DifficultyResolver.Resolve(
            new DifficultySettings { ShrinePrices = DifficultyStep.Extreme }, new GameplayConfig());

        Assert.Equal(150, UpgradePricing.EffectiveCost(Def(100), profile));
    }

    /// <summary>The affordability check and the deduction must use the same number. This is the
    /// shape of bug 0.14.2 fixed in Shop Discount, where the posted price and the charged price
    /// came from different code paths.</summary>
    [Fact]
    public void Purchase_Charges_Exactly_What_It_Checks()
    {
        var meta = new MetaState { JunimoPoints = 125 };

        Assert.Equal(UpgradePurchase.PurchaseResult.Success,
            UpgradePurchase.TryPurchase(meta, Def(100), 1.25));
        Assert.Equal(0, meta.JunimoPoints);
    }

    [Fact]
    public void Purchase_Is_Refused_When_The_Scaled_Price_Is_Unaffordable()
    {
        var meta = new MetaState { JunimoPoints = 100 };

        Assert.Equal(UpgradePurchase.PurchaseResult.NotEnoughJp,
            UpgradePurchase.TryPurchase(meta, Def(100), 1.25));
        Assert.Equal(100, meta.JunimoPoints);
    }

    [Fact]
    public void An_Easy_Price_Makes_A_Previously_Unaffordable_Upgrade_Buyable()
    {
        var meta = new MetaState { JunimoPoints = 80 };

        Assert.Equal(UpgradePurchase.PurchaseResult.NotEnoughJp,
            UpgradePurchase.TryPurchase(meta, Def(100), 1.0));
        Assert.Equal(UpgradePurchase.PurchaseResult.Success,
            UpgradePurchase.TryPurchase(meta, Def(100), 0.75));
        Assert.Equal(5, meta.JunimoPoints);
    }

    [Fact]
    public void The_Default_Factor_Charges_The_Catalog_Price()
    {
        var meta = new MetaState { JunimoPoints = 100 };

        Assert.Equal(UpgradePurchase.PurchaseResult.Success,
            UpgradePurchase.TryPurchase(meta, Def(100)));
        Assert.Equal(0, meta.JunimoPoints);
    }
}
