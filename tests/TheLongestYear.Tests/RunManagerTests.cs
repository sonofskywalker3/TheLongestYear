using System.Collections.Generic;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class RunManagerTests
{
    // A canonical 4-bundle requirement set: one Seasonal per season, all year-round coverage.
    // Enough to exercise the season-gate, vault-gate, and end-of-Winter full-CC paths.
    private static IReadOnlyList<BundleRequirement> SimpleBundles() => new[]
    {
        BundleRequirement.CreateSeasonal("Spring Foraging", Theme.Foraging,
            new[] { "spring-1", "spring-2" }, Season.Spring, bundleIndex: 0),
        BundleRequirement.CreateSeasonal("Summer Foraging", Theme.Foraging,
            new[] { "summer-1" }, Season.Summer, bundleIndex: 1),
        BundleRequirement.CreateSeasonal("Fall Foraging",   Theme.Foraging,
            new[] { "fall-1" }, Season.Fall, bundleIndex: 2),
        BundleRequirement.CreatePerItem("Winter Group", Theme.Mining,
            new Dictionary<string, Season>
            {
                ["winter-1"] = Season.Winter,
                ["winter-2"] = Season.Winter
            }, bundleIndex: 3)
    };

    private static IReadOnlyList<BundleRequirement> BoardWithConstruction() => new[]
    {
        BundleRequirement.CreatePerItem("Construction", Theme.Foraging,
            new[] { "wood", "stone", "hardwood" },
            new Dictionary<string, Season> { ["wood"] = Season.Winter, ["stone"] = Season.Winter, ["hardwood"] = Season.Winter },
            bundleIndex: 13,
            slots: new[] { new BundleSlot(0, "wood"), new BundleSlot(1, "wood"), new BundleSlot(2, "stone"), new BundleSlot(3, "hardwood") })
    };

    /// <summary>Fill every slot of every bundle whose id is named (the old id-level donation).</summary>
    private static void Donate(RunState run, IReadOnlyList<BundleRequirement> bundles, params string[] ids)
    {
        var wanted = new HashSet<string>(ids);
        foreach (BundleRequirement b in bundles)
            foreach (BundleSlot slot in b.Slots)
                if (wanted.Contains(slot.ItemId))
                    run.RecordDonation(b.BundleIndex, slot.IngredientIndex, slot.ItemId);
    }

    private static RunManager Mgr() => new RunManager(new GateEvaluator());

    private const bool VaultOk = true;

    [Fact]
    public void Midweek_continues()
    {
        var run = new RunState { Season = Season.Spring, DayOfMonth = 3 };
        Assert.Equal(RunAction.Continue, Mgr().EvaluateDayEnd(run, SimpleBundles(), VaultOk));
    }

    [Fact]
    public void Week_end_off_month_continues()
    {
        var run = new RunState { Season = Season.Spring, DayOfMonth = 7 };
        Assert.Equal(RunAction.Continue, Mgr().EvaluateDayEnd(run, SimpleBundles(), VaultOk));
    }

    [Fact]
    public void Month_end_with_all_in_season_items_donated_advances()
    {
        var run = new RunState { Season = Season.Spring, DayOfMonth = 28 };
        var bundles = SimpleBundles();
        // Spring requires only the Seasonal Spring bundle (2 items).
        Donate(run, bundles, "spring-1", "spring-2");
        Assert.Equal(RunAction.AdvanceMonth, Mgr().EvaluateDayEnd(run, bundles, VaultOk));
    }

    [Fact]
    public void Month_end_missing_a_seasonal_item_fails()
    {
        var run = new RunState { Season = Season.Spring, DayOfMonth = 28 };
        var bundles = SimpleBundles();
        Donate(run, bundles, "spring-1"); // missing spring-2
        Assert.Equal(RunAction.FailReset, Mgr().EvaluateDayEnd(run, bundles, VaultOk));
    }

    [Fact]
    public void Month_end_with_items_done_but_vault_unpaid_fails()
    {
        var run = new RunState { Season = Season.Spring, DayOfMonth = 28 };
        var bundles = SimpleBundles();
        Donate(run, bundles, "spring-1", "spring-2");
        Assert.Equal(RunAction.FailReset,
            Mgr().EvaluateDayEnd(run, bundles, vaultGateSatisfied: false));
    }

    [Fact]
    public void Midweek_with_vault_unpaid_still_continues()
    {
        // Vault gate only applies at month-end; mid-month days never fail on vault.
        var run = new RunState { Season = Season.Spring, DayOfMonth = 14 };
        Assert.Equal(RunAction.Continue,
            Mgr().EvaluateDayEnd(run, SimpleBundles(), vaultGateSatisfied: false));
    }

    [Fact]
    public void Winter_end_with_full_cc_and_vault_wins()
    {
        var run = new RunState { Season = Season.Winter, DayOfMonth = 28 };
        var bundles = SimpleBundles();
        // Every bundle's slots filled -> fullCcDone = true.
        Donate(run, bundles, "spring-1", "spring-2", "summer-1", "fall-1", "winter-1", "winter-2");
        Assert.Equal(RunAction.Win, Mgr().EvaluateDayEnd(run, bundles, VaultOk));
    }

    [Fact]
    public void Winter_end_with_full_cc_but_vault_unpaid_fails()
    {
        var run = new RunState { Season = Season.Winter, DayOfMonth = 28 };
        var bundles = SimpleBundles();
        Donate(run, bundles, "spring-1", "spring-2", "summer-1", "fall-1", "winter-1", "winter-2");
        Assert.Equal(RunAction.FailReset,
            Mgr().EvaluateDayEnd(run, bundles, vaultGateSatisfied: false));
    }

    [Fact]
    public void Winter_end_with_one_seasonal_item_missing_fails_even_with_vault_ok()
    {
        // Spring Foraging passed in Spring but we never donated spring-2 by Winter day 28 ->
        // BundleGate.IsSatisfied(Winter, ...) returns false because Spring's gate (long
        // past due) still needs both ingredients.
        var run = new RunState { Season = Season.Winter, DayOfMonth = 28 };
        var bundles = SimpleBundles();
        Donate(run, bundles, "spring-1", "summer-1", "fall-1", "winter-1", "winter-2");
        Assert.Equal(RunAction.FailReset, Mgr().EvaluateDayEnd(run, bundles, VaultOk));
    }

    [Fact]
    public void Winter_end_with_a_doubled_slot_open_does_not_win()
    {
        var run = new RunState { Season = Season.Winter, DayOfMonth = 28 };
        run.RecordDonation(13, 0, "wood");
        run.RecordDonation(13, 2, "stone");
        run.RecordDonation(13, 3, "hardwood");
        Assert.Equal(RunAction.FailReset, Mgr().EvaluateDayEnd(run, BoardWithConstruction(), VaultOk));
        run.RecordDonation(13, 1, "wood");
        Assert.Equal(RunAction.Win, Mgr().EvaluateDayEnd(run, BoardWithConstruction(), VaultOk));
    }
}
