using System.Linq;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class GiftLadderTests
{
    [Fact]
    public void Five_gifts_live_in_their_own_category_with_a_room_reach_each()
    {
        var gifts = UpgradeCatalog.ByCategory(UpgradeCategory.Gifts).Select(d => d.Id).ToList();
        Assert.Equal(new[] { "keep_greenhouse", "keep_quarry_bridge", "keep_boulder_cleared", "keep_minecarts", "keep_bus_unlocked" }, gifts);
        Assert.Equal("room:Pantry", UpgradeCatalog.TryGet("keep_greenhouse")!.RunReachRequirement);
        Assert.Equal("room:CraftsRoom", UpgradeCatalog.TryGet("keep_quarry_bridge")!.RunReachRequirement);
        Assert.Equal("bus:4", UpgradeCatalog.TryGet("keep_bus_unlocked")!.RunReachRequirement);
        Assert.DoesNotContain(UpgradeCatalog.ByCategory(UpgradeCategory.Buildings), d => d.Id == "keep_bus_unlocked");
    }

    [Fact]
    public void Every_gift_owned_raises_the_next_price_by_a_thousand_up_to_five_thousand()
    {
        var state = new MetaState();
        UpgradeDefinition greenhouse = UpgradeCatalog.TryGet("keep_greenhouse")!;
        Assert.Equal(1000, UpgradePricing.EffectiveCost(greenhouse, 1.0, state));
        state.OwnedUpgrades.Add("keep_bus_unlocked");
        Assert.Equal(2000, UpgradePricing.EffectiveCost(greenhouse, 1.0, state));
        state.OwnedUpgrades.Add("keep_minecarts");
        state.OwnedUpgrades.Add("keep_boulder_cleared");
        state.OwnedUpgrades.Add("keep_quarry_bridge");
        Assert.Equal(5000, UpgradePricing.EffectiveCost(greenhouse, 1.0, state));
        state.OwnedUpgrades.Add("keep_greenhouse");
        Assert.Equal(5000, GiftLadder.CostFor(state));   // capped
        Assert.Equal(5, GiftLadder.OwnedCount(state));
    }

    [Fact]
    public void The_ladder_ignores_non_gifts_and_scales_with_the_shrine_factor()
    {
        var state = new MetaState { OwnedUpgrades = { "keep_bus_unlocked" } };
        UpgradeDefinition silo = UpgradeCatalog.TryGet("keep_silo")!;
        Assert.Equal(UpgradePricing.EffectiveCost(silo, 1.0), UpgradePricing.EffectiveCost(silo, 1.0, state));
        Assert.Equal(3000, UpgradePricing.EffectiveCost(UpgradeCatalog.TryGet("keep_minecarts")!, 1.5, state));
    }

    [Fact]
    public void Buying_a_gift_through_UpgradePurchase_charges_the_ladder_and_raises_the_next()
    {
        var state = new MetaState { JunimoPoints = 3500 };
        UpgradeDefinition minecarts = UpgradeCatalog.TryGet("keep_minecarts")!;
        UpgradeDefinition bridge = UpgradeCatalog.TryGet("keep_quarry_bridge")!;
        Assert.Equal(UpgradePurchase.PurchaseResult.Success, UpgradePurchase.TryPurchase(state, minecarts, 1.0));
        Assert.Equal(2500, state.JunimoPoints);
        Assert.Equal(2000, UpgradePricing.EffectiveCost(bridge, 1.0, state));
        Assert.Equal(UpgradePurchase.PurchaseResult.Success, UpgradePurchase.TryPurchase(state, bridge, 1.0));
        Assert.Equal(500, state.JunimoPoints);
    }

    [Fact]
    public void Kept_gift_mails_follow_ownership_and_reach_the_baseline()
    {
        var state = new MetaState { OwnedUpgrades = { "keep_greenhouse", "keep_bus_unlocked" } };
        Assert.Equal(new[] { "ccPantry", "ccVault" }, GiftLadder.KeptMails(state));
        RunBaseline baseline = RunBaselineBuilder.Build(state, new RunState(), new PlayerSnapshot(), 500);
        Assert.Equal(new[] { "ccPantry", "ccVault" }, baseline.KeptGiftMails);
        Assert.True(baseline.BusUnlocked);
    }
}
