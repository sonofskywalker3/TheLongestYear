using System;
using System.Linq;
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
    [InlineData(23, true)]
    [InlineData(26, true)]
    [InlineData(22, false)]
    [InlineData(27, false)]
    public void IsVaultIndex_only_true_for_23_to_26(int index, bool expected)
        => Assert.Equal(expected, VaultRules.IsVaultIndex(index));

    /// <summary>The fallback layout is the unmodded 1.6.15 board's own Vault keys and prices
    /// (VanillaBundleBoard.Generated.cs), not the pre-1.6 34 to 37 numbering.</summary>
    [Fact]
    public void Fallback_indices_match_the_vanilla_board()
    {
        var vanillaVault = VanillaBundleBoard.Standard
            .Where(kv => kv.Key.StartsWith("Vault/", StringComparison.Ordinal))
            .ToDictionary(
                kv => int.Parse(kv.Key.Split('/')[1]),
                kv => int.Parse(kv.Value.Split('/')[2].Split(' ')[1]));

        Assert.Equal(vanillaVault.Keys.OrderBy(i => i), VaultRules.VaultIndices);
        foreach (var (index, gold) in vanillaVault)
            Assert.Equal(gold, VaultRules.GoldForIndex(index));
    }

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
        Assert.Equal(UpgradeCategory.Gifts, def!.Category);
    }

    // VaultPaymentSync backstop (fixed 2026-10-09): vanilla's purchase button sets only slot 0 of a
    // money bundle's 3-slot array, so isBundleComplete (all slots) never saw a paid Vault bundle.

    [Fact]
    public void A_vanilla_paid_money_bundle_reads_as_paid()
        => Assert.True(VaultRules.IsMoneyBundlePaid(new[] { true, false, false }));

    [Theory]
    [InlineData(new[] { false, false, false })]
    [InlineData(new[] { false, true, true })]
    [InlineData(new bool[0])]
    public void An_unpaid_money_bundle_reads_as_unpaid(bool[] slots)
        => Assert.False(VaultRules.IsMoneyBundlePaid(slots));

    [Fact]
    public void A_missing_slot_array_reads_as_unpaid()
        => Assert.False(VaultRules.IsMoneyBundlePaid(null));

    [Fact]
    public void PaidOnBoard_lists_the_paid_vault_bundles_in_board_order()
    {
        var slots = new System.Collections.Generic.Dictionary<int, bool[]>
        {
            [23] = new[] { true, false, false },
            [24] = new[] { false, false, false },
            [25] = new[] { true, false, false },
            // 26 missing from the slot state: skipped, never throws
            [40] = new[] { true, false, false },
            [5] = new[] { true, true, true },   // not a Vault bundle: ignored
        };
        Assert.Equal(new[] { 23, 25, 40 }, VaultRules.PaidOnBoard(new[] { 23, 24, 25, 26, 40 }, slots));
    }

    [Fact]
    public void The_backstop_and_the_live_tracker_pay_a_vault_bundle_once()
    {
        // The live observer marks first, then the day-end reconcile sees the same paid slot:
        // only the first mark records (and so pays JP).
        var run = new RunState();
        Assert.True(run.TryMarkVaultBundlePaid(23));
        var slots = new System.Collections.Generic.Dictionary<int, bool[]> { [23] = new[] { true, false, false } };
        int newlyMarked = VaultRules.PaidOnBoard(new[] { 23 }, slots).Count(i => run.TryMarkVaultBundlePaid(i));
        Assert.Equal(0, newlyMarked);
        Assert.Equal(new[] { 23 }, run.VaultBundlesPaid);
    }

    [Fact]
    public void The_backstop_records_a_payment_the_live_tracker_missed()
    {
        var run = new RunState();
        var slots = new System.Collections.Generic.Dictionary<int, bool[]>
        {
            [23] = new[] { true, false, false },
            [24] = new[] { true, false, false },
        };
        int newlyMarked = VaultRules.PaidOnBoard(new[] { 23, 24, 25, 26 }, slots).Count(i => run.TryMarkVaultBundlePaid(i));
        Assert.Equal(2, newlyMarked);
        // A second reconcile the same day adds nothing.
        Assert.Equal(0, VaultRules.PaidOnBoard(new[] { 23, 24, 25, 26 }, slots).Count(i => run.TryMarkVaultBundlePaid(i)));
    }
}
