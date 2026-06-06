using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class VaultRulesTests
{
    [Theory]
    [InlineData(Season.Spring, 1)]
    [InlineData(Season.Summer, 2)]
    [InlineData(Season.Fall,   3)]
    [InlineData(Season.Winter, 4)]
    public void SeasonOrdinal_is_one_based(Season season, int expected)
        => Assert.Equal(expected, VaultRules.SeasonOrdinal(season));

    [Theory]
    [InlineData(VaultRules.Vault2500,  2500)]
    [InlineData(VaultRules.Vault5000,  5000)]
    [InlineData(VaultRules.Vault10000, 10000)]
    [InlineData(VaultRules.Vault25000, 25000)]
    public void GoldForIndex_maps_each_index_to_its_price(int index, int gold)
        => Assert.Equal(gold, VaultRules.GoldForIndex(index));

    [Theory]
    [InlineData(34, true)]
    [InlineData(37, true)]
    [InlineData(33, false)]
    [InlineData(38, false)]
    public void IsVaultIndex_only_true_for_34_to_37(int index, bool expected)
        => Assert.Equal(expected, VaultRules.IsVaultIndex(index));

    [Fact]
    public void Gate_needs_count_at_least_season_ordinal()
    {
        var run = new RunState();
        var meta = new MetaState();

        // Spring (ordinal 1): 0 paid fails, 1 paid passes — any tier.
        Assert.False(VaultRules.IsVaultGateSatisfied(Season.Spring, run, meta));
        run.VaultBundlesPaid.Add(VaultRules.Vault25000);   // tier doesn't matter
        Assert.True(VaultRules.IsVaultGateSatisfied(Season.Spring, run, meta));

        // Summer (ordinal 2): still only 1 paid → fails until a second.
        Assert.False(VaultRules.IsVaultGateSatisfied(Season.Summer, run, meta));
        run.VaultBundlesPaid.Add(VaultRules.Vault2500);
        Assert.True(VaultRules.IsVaultGateSatisfied(Season.Summer, run, meta));
    }

    [Fact]
    public void Paying_all_four_in_spring_satisfies_winter()
    {
        var run = new RunState();
        run.VaultBundlesPaid.AddRange(new[]
            { VaultRules.Vault2500, VaultRules.Vault5000, VaultRules.Vault10000, VaultRules.Vault25000 });
        Assert.True(VaultRules.IsVaultGateSatisfied(Season.Winter, run, new MetaState()));
    }

    [Fact]
    public void Keep_bus_unlocked_short_circuits_with_zero_paid()
    {
        var run = new RunState();   // nothing paid
        var meta = new MetaState { OwnedUpgrades = { VaultRules.KeepBusUnlockedId } };
        Assert.True(VaultRules.IsVaultGateSatisfied(Season.Winter, run, meta));
    }

    [Fact]
    public void PaidCount_reflects_the_list()
    {
        var run = new RunState();
        run.VaultBundlesPaid.Add(VaultRules.Vault2500);
        run.VaultBundlesPaid.Add(VaultRules.Vault5000);
        Assert.Equal(2, VaultRules.PaidCount(run));
    }

    [Fact]
    public void Keep_bus_unlocked_is_in_the_upgrade_catalog()
    {
        UpgradeDefinition? def = UpgradeCatalog.TryGet(VaultRules.KeepBusUnlockedId);
        Assert.NotNull(def);
        Assert.Equal(UpgradeCategory.Buildings, def!.Category);
    }
}
