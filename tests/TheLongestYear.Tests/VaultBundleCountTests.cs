using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>The Vault under the bundle-count dial (Jeff, 2026-10-09): Easy drops the priciest
/// bundle, Hard adds one at 2x the priciest, Extreme adds a second at 1.6x the first extra.
/// Prices are the board's own, after the Vault multiplier.</summary>
public class VaultBundleCountTests
{
    private static BundleSpec Money(int index, int gold, string reward = "O 220 3", int color = 4)
    {
        string name = gold.ToString("N0", System.Globalization.CultureInfo.InvariantCulture) + "g";
        return new BundleSpec("Vault", index, name, name, reward, color, 0,
            new[] { new BundleSlotSpec("-1", gold, gold) });
    }

    /// <summary>The board's Vault at the default +25% multiplier, in board order.</summary>
    private static List<BundleSpec> ScaledVault() => new()
    {
        Money(23, 3125), Money(24, 6250), Money(25, 12500), Money(26, 31250, "BO 21 1", 1),
    };

    private static List<BundleSpec> VanillaVault() => new()
    {
        Money(23, 2500), Money(24, 5000), Money(25, 10000), Money(26, 25000, "BO 21 1", 1),
    };

    private static System.Func<int> Counter(int first = 9000)
    {
        int next = first;
        return () => next++;
    }

    [Fact]
    public void Rule_deltas_per_step()
    {
        Assert.Equal(-1, BundleCountRule.For(DifficultyStep.Easy).VaultDelta);
        Assert.Equal(0, BundleCountRule.For(DifficultyStep.Normal).VaultDelta);
        Assert.Equal(1, BundleCountRule.For(DifficultyStep.Hard).VaultDelta);
        Assert.Equal(2, BundleCountRule.For(DifficultyStep.Extreme).VaultDelta);
    }

    [Fact]
    public void A_rule_stamped_before_the_vault_joined_keeps_four()
        => Assert.Equal(0, new BundleCountRule().VaultDelta);

    [Fact]
    public void Easy_drops_the_priciest_and_keeps_order()
    {
        IReadOnlyList<BundleSpec> kept = VaultBundleCount.Kept(ScaledVault(), -1);
        Assert.Equal(new[] { 23, 24, 25 }, kept.Select(b => b.Index));
    }

    [Fact]
    public void Easy_finds_the_priciest_wherever_it_sits()
    {
        var vault = new List<BundleSpec> { Money(5, 50000), Money(6, 1000), Money(7, 2000) };
        Assert.Equal(new[] { 6, 7 }, VaultBundleCount.Kept(vault, -1).Select(b => b.Index));
    }

    [Fact]
    public void Kept_never_empties_the_vault()
    {
        var vault = new List<BundleSpec> { Money(23, 2500) };
        Assert.Single(VaultBundleCount.Kept(vault, -1));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Normal_hard_and_extreme_keep_every_bundle(int delta)
        => Assert.Equal(4, VaultBundleCount.Kept(ScaledVault(), delta).Count);

    [Fact]
    public void Extra_prices_on_vanilla_base_prices()
        => Assert.Equal(new[] { 50000, 80000 }, VaultBundleCount.ExtraPrices(25000, 2));

    [Fact]
    public void Extra_prices_on_the_scaled_board_keep_the_ratios()
        => Assert.Equal(new[] { 62500, 100000 }, VaultBundleCount.ExtraPrices(31250, 2));

    [Fact]
    public void Extra_prices_round_to_a_clean_number()
        => Assert.Equal(new[] { 6500, 10500 }, VaultBundleCount.ExtraPrices(3260, 2)); // 6,520 -> 6,500; 10,400 -> 10,500 (1.6 x 6,500)

    [Fact]
    public void Normal_and_easy_add_nothing()
    {
        Assert.Empty(VaultBundleCount.Extras(ScaledVault(), 0, Counter()));
        Assert.Empty(VaultBundleCount.Extras(ScaledVault(), -1, Counter()));
    }

    [Fact]
    public void Hard_adds_one_at_double_the_priciest()
    {
        BundleSpec extra = Assert.Single(VaultBundleCount.Extras(ScaledVault(), 1, Counter()));
        Assert.Equal(62500, VaultBundleCount.Gold(extra));
        Assert.Equal("62,500g", extra.Name);
        Assert.Equal("62,500g", extra.DisplayName);
        Assert.Equal("Vault", extra.Room);
        Assert.Equal(9000, extra.Index);
        Assert.Equal("BO 21 1", extra.RewardField);
        Assert.Single(extra.Slots);
        Assert.Equal("-1", extra.Slots[0].ItemId);
    }

    [Fact]
    public void Extreme_adds_two_with_their_own_indices_and_tints()
    {
        IReadOnlyList<BundleSpec> extras = VaultBundleCount.Extras(VanillaVault(), 2, Counter(9004));
        Assert.Equal(new[] { 50000, 80000 }, extras.Select(VaultBundleCount.Gold));
        Assert.Equal(new[] { "50,000g", "80,000g" }, extras.Select(e => e.Name));
        Assert.Equal(new[] { 9004, 9005 }, extras.Select(e => e.Index));
        Assert.Equal(2, extras.Select(e => e.Color).Distinct().Count());
        Assert.DoesNotContain(extras, e => VanillaVault().Any(v => v.Color == e.Color));
    }

    [Fact]
    public void A_vault_with_no_money_bundle_gets_no_extras()
    {
        var vault = new List<BundleSpec>
        {
            new("Vault", 23, "Odd", "Odd", "O 1 1", 1, 1, new[] { new BundleSlotSpec("24", 1, 0) }),
        };
        Assert.Empty(VaultBundleCount.Extras(vault, 2, Counter()));
    }

    [Theory]
    [InlineData(Season.Spring, 3, 1)]
    [InlineData(Season.Fall, 3, 3)]
    [InlineData(Season.Winter, 3, 3)]
    [InlineData(Season.Winter, 4, 4)]
    [InlineData(Season.Winter, 6, 4)]
    [InlineData(Season.Winter, 0, 4)]
    public void The_gate_never_asks_for_more_than_the_board_has(Season season, int vaultCount, int expected)
        => Assert.Equal(expected, VaultRules.RequiredPaid(season, vaultCount));

    [Fact]
    public void An_easy_board_satisfies_winter_with_three_paid()
    {
        var run = new RunState { Season = Season.Winter };
        run.VaultBundlesPaid.AddRange(new[] { 23, 24, 25 });
        Assert.True(VaultRules.IsVaultGateSatisfied(Season.Winter, run, new MetaState(), vaultCount: 3));
        Assert.False(VaultRules.IsVaultGateSatisfied(Season.Winter, run, new MetaState()));
    }

    [Theory]
    [InlineData(3, 3, 4)]  // Easy board, every Vault bundle paid: counts as the full four
    [InlineData(2, 3, 2)]
    [InlineData(4, 4, 4)]
    [InlineData(4, 5, 4)]  // Hard board: four paid still reads four, as before
    [InlineData(5, 5, 5)]
    [InlineData(0, 0, 0)]
    public void Bus_reach_counts_an_easy_vault_in_full(int paid, int vaultCount, int expected)
        => Assert.Equal(expected, VaultRules.BusReachValue(paid, vaultCount));
}
