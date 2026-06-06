using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class VaultRulesTests
{
    [Theory]
    [InlineData(Season.Spring, VaultRules.Vault2500)]
    [InlineData(Season.Summer, VaultRules.Vault5000)]
    [InlineData(Season.Fall,   VaultRules.Vault10000)]
    [InlineData(Season.Winter, VaultRules.Vault25000)]
    public void BundleIndexForSeason_maps_each_season_to_its_gold_tier(Season season, int expectedIndex)
        => Assert.Equal(expectedIndex, VaultRules.BundleIndexForSeason(season));

    [Fact]
    public void Vault_gate_passes_when_the_season_bundle_is_paid()
    {
        var run = new RunState();
        run.VaultBundlesPaid.Add(VaultRules.Vault2500);
        Assert.True(VaultRules.IsVaultGateSatisfied(Season.Spring, run, new MetaState()));
    }

    [Fact]
    public void Vault_gate_fails_when_paying_a_different_tier()
    {
        // Paying the 5,000g bundle (Summer) doesn't satisfy the Spring gate (2,500g).
        var run = new RunState();
        run.VaultBundlesPaid.Add(VaultRules.Vault5000);
        Assert.False(VaultRules.IsVaultGateSatisfied(Season.Spring, run, new MetaState()));
    }

    [Fact]
    public void Keep_bus_unlocked_short_circuits_every_season()
    {
        var run = new RunState();   // no bundles paid this run
        var meta = new MetaState { OwnedUpgrades = { VaultRules.KeepBusUnlockedId } };
        Assert.True(VaultRules.IsVaultGateSatisfied(Season.Spring, run, meta));
        Assert.True(VaultRules.IsVaultGateSatisfied(Season.Summer, run, meta));
        Assert.True(VaultRules.IsVaultGateSatisfied(Season.Fall,   run, meta));
        Assert.True(VaultRules.IsVaultGateSatisfied(Season.Winter, run, meta));
    }

    [Fact]
    public void Keep_bus_unlocked_is_in_the_upgrade_catalog()
    {
        UpgradeDefinition? def = UpgradeCatalog.TryGet(VaultRules.KeepBusUnlockedId);
        Assert.NotNull(def);
        Assert.Equal(UpgradeCategory.Buildings, def!.Category);
    }

    [Theory]
    [InlineData(Season.Spring, 2500)]
    [InlineData(Season.Summer, 5000)]
    [InlineData(Season.Fall,   10000)]
    [InlineData(Season.Winter, 25000)]
    public void GoldCostForSeason_maps_each_season_to_its_vault_price(Season season, int expectedGold)
        => Assert.Equal(expectedGold, VaultRules.GoldCostForSeason(season));

    [Fact]
    public void DescribeGate_is_Unpaid_when_nothing_paid_and_no_upgrade()
    {
        var run = new RunState();
        Assert.Equal(VaultGateStatus.Unpaid,
            VaultRules.DescribeGate(Season.Spring, run, new MetaState()));
    }

    [Fact]
    public void DescribeGate_is_PaidThisRun_when_the_season_bundle_is_paid()
    {
        var run = new RunState();
        run.VaultBundlesPaid.Add(VaultRules.Vault2500);
        Assert.Equal(VaultGateStatus.PaidThisRun,
            VaultRules.DescribeGate(Season.Spring, run, new MetaState()));
    }

    [Fact]
    public void DescribeGate_is_Unpaid_when_a_different_tier_is_paid()
    {
        var run = new RunState();
        run.VaultBundlesPaid.Add(VaultRules.Vault5000);   // Summer paid, asking about Spring
        Assert.Equal(VaultGateStatus.Unpaid,
            VaultRules.DescribeGate(Season.Spring, run, new MetaState()));
    }

    [Fact]
    public void DescribeGate_is_KeptViaUpgrade_when_keep_bus_unlocked_is_owned()
    {
        var run = new RunState();   // no bundles paid this run
        var meta = new MetaState { OwnedUpgrades = { VaultRules.KeepBusUnlockedId } };
        Assert.Equal(VaultGateStatus.KeptViaUpgrade,
            VaultRules.DescribeGate(Season.Winter, run, meta));
    }
}
