using System.Collections.Generic;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class BundleGateTests
{
    private static IReadOnlyList<BundleRequirement> SpringForage_BlacksmithChain()
    {
        return new[]
        {
            BundleRequirement.CreateSeasonal("Spring Foraging", Theme.Foraging,
                new[] { "Horseradish", "Daffodil" }, Season.Spring, bundleIndex: 0),
            BundleRequirement.CreatePerItem("Blacksmith's", Theme.Mining,
                new Dictionary<string, Season>
                {
                    ["Copper"] = Season.Spring,
                    ["Iron"]   = Season.Summer,
                    ["Gold"]   = Season.Fall
                }, bundleIndex: 1)
        };
    }

    [Fact]
    public void Empty_donations_fail_at_spring_end_when_seasonal_in_play()
    {
        var bundles = SpringForage_BlacksmithChain();
        Assert.False(BundleGate.IsSatisfied(
            Season.Spring, TestLedger.Empty(), bundles, vaultGateSatisfied: true));
    }

    [Fact]
    public void Full_spring_donations_pass_with_vault()
    {
        var bundles = SpringForage_BlacksmithChain();
        var donated = TestLedger.Fill(bundles, "Horseradish", "Daffodil", "Copper");
        Assert.True(BundleGate.IsSatisfied(
            Season.Spring, donated, bundles, vaultGateSatisfied: true));
    }

    [Fact]
    public void Vault_gate_failure_fails_the_whole_gate()
    {
        var bundles = SpringForage_BlacksmithChain();
        var donated = TestLedger.Fill(bundles, "Horseradish", "Daffodil", "Copper");
        Assert.False(BundleGate.IsSatisfied(
            Season.Spring, donated, bundles, vaultGateSatisfied: false));
    }

    [Fact]
    public void Summer_check_demands_iron_in_addition_to_spring()
    {
        var bundles = SpringForage_BlacksmithChain();
        // Got Spring items + Copper, but missing Iron at Summer 28 -> fail.
        var donated = TestLedger.Fill(bundles, "Horseradish", "Daffodil", "Copper");
        Assert.False(BundleGate.IsSatisfied(
            Season.Summer, donated, bundles, vaultGateSatisfied: true));

        donated = TestLedger.Fill(bundles, "Horseradish", "Daffodil", "Copper", "Iron");
        Assert.True(BundleGate.IsSatisfied(
            Season.Summer, donated, bundles, vaultGateSatisfied: true));
    }

    [Fact]
    public void IsFullyDone_requires_every_bundle_to_be_X_complete()
    {
        // Mix all three kinds.
        var bundles = new BundleRequirement[]
        {
            BundleRequirement.CreateSeasonal("Spring Foraging", Theme.Foraging,
                new[] { "A", "B" }, Season.Spring, bundleIndex: 0),
            BundleRequirement.CreatePerItem("Blacksmith's", Theme.Mining,
                new Dictionary<string, Season>
                {
                    ["Copper"] = Season.Spring,
                    ["Iron"]   = Season.Summer
                }, bundleIndex: 1),
            BundleRequirement.CreatePercentage("Crab Pot", Theme.Fishing,
                new[] { "Crab1", "Crab2", "Crab3", "Crab4", "Crab5" },
                numberOfSlots: 3,
                cumulativeRequiredBySeason: new[] { 0, 1, 2, 3 }, bundleIndex: 2)
        };

        var donated = TestLedger.Fill(bundles, "A", "B", "Copper", "Iron", "Crab1", "Crab2");
        Assert.False(BundleGate.IsFullyDone(donated, bundles));   // Crab Pot needs 3, has 2

        donated = TestLedger.Fill(bundles, "A", "B", "Copper", "Iron", "Crab1", "Crab2", "Crab3");
        Assert.True(BundleGate.IsFullyDone(donated, bundles));
    }

    [Fact]
    public void Empty_bundle_list_passes_trivially_when_vault_ok()
    {
        // No bundles classified — gate degenerates to vault-only.
        Assert.True(BundleGate.IsSatisfied(
            Season.Spring, TestLedger.Empty(), new BundleRequirement[0], vaultGateSatisfied: true));
        Assert.False(BundleGate.IsSatisfied(
            Season.Spring, TestLedger.Empty(), new BundleRequirement[0], vaultGateSatisfied: false));
    }
}
