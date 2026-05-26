using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class UpgradePurchaseTests
{
    private static UpgradeDefinition Def(string id, long cost = 100, string? prereq = null)
        => new UpgradeDefinition(id, UpgradeCategory.Loadout, id + "-name", "desc", cost, prereq);

    [Fact]
    public void Success_deducts_cost_and_records_ownership()
    {
        var state = new MetaState { JunimoPoints = 250 };
        UpgradePurchase.PurchaseResult result = UpgradePurchase.TryPurchase(state, Def("backpack_1", 100));

        Assert.Equal(UpgradePurchase.PurchaseResult.Success, result);
        Assert.Equal(150, state.JunimoPoints);
        Assert.Contains("backpack_1", state.OwnedUpgrades);
    }

    [Fact]
    public void NotEnoughJp_leaves_state_unchanged()
    {
        var state = new MetaState { JunimoPoints = 50 };
        UpgradePurchase.PurchaseResult result = UpgradePurchase.TryPurchase(state, Def("backpack_1", 100));

        Assert.Equal(UpgradePurchase.PurchaseResult.NotEnoughJp, result);
        Assert.Equal(50, state.JunimoPoints);
        Assert.Empty(state.OwnedUpgrades);
    }

    [Fact]
    public void AlreadyOwned_leaves_state_unchanged()
    {
        var state = new MetaState { JunimoPoints = 500, OwnedUpgrades = { "backpack_1" } };
        UpgradePurchase.PurchaseResult result = UpgradePurchase.TryPurchase(state, Def("backpack_1", 100));

        Assert.Equal(UpgradePurchase.PurchaseResult.AlreadyOwned, result);
        Assert.Equal(500, state.JunimoPoints);
        Assert.Single(state.OwnedUpgrades);
    }

    [Fact]
    public void PrerequisiteMissing_leaves_state_unchanged()
    {
        var state = new MetaState { JunimoPoints = 500 };
        UpgradePurchase.PurchaseResult result =
            UpgradePurchase.TryPurchase(state, Def("backpack_2", 250, prereq: "backpack_1"));

        Assert.Equal(UpgradePurchase.PurchaseResult.PrerequisiteMissing, result);
        Assert.Equal(500, state.JunimoPoints);
        Assert.Empty(state.OwnedUpgrades);
    }

    [Fact]
    public void PrerequisiteOwned_lets_purchase_succeed()
    {
        var state = new MetaState { JunimoPoints = 500, OwnedUpgrades = { "backpack_1" } };
        UpgradePurchase.PurchaseResult result =
            UpgradePurchase.TryPurchase(state, Def("backpack_2", 250, prereq: "backpack_1"));

        Assert.Equal(UpgradePurchase.PurchaseResult.Success, result);
        Assert.Equal(250, state.JunimoPoints);
        Assert.Contains("backpack_2", state.OwnedUpgrades);
    }

    [Fact]
    public void Null_definition_returns_NotInCatalog()
        => Assert.Equal(UpgradePurchase.PurchaseResult.NotInCatalog,
            UpgradePurchase.TryPurchase(new MetaState { JunimoPoints = 100 }, null));

    [Fact]
    public void Exactly_enough_jp_succeeds()
    {
        var state = new MetaState { JunimoPoints = 100 };
        Assert.Equal(UpgradePurchase.PurchaseResult.Success,
            UpgradePurchase.TryPurchase(state, Def("backpack_1", 100)));
        Assert.Equal(0, state.JunimoPoints);
    }
}
