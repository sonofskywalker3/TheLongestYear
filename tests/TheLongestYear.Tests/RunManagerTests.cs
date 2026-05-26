using System;
using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class RunManagerTests
{
    private static CcItem Item(string id, Theme theme, params Season[] seasons)
        => new CcItem(id, theme, Rarity.Common, new HashSet<Season>(seasons));

    /// <summary>Catalog with one single-season item per season + one year-round multi-season item.</summary>
    private static List<CcItem> Catalog() => new()
    {
        Item("spring-only", Theme.Foraging, Season.Spring),
        Item("summer-only", Theme.Farming,  Season.Summer),
        Item("fall-only",   Theme.Fishing,  Season.Fall),
        Item("winter-only", Theme.Mining,   Season.Winter),
        Item("year-round",  Theme.Mixed,    Season.Spring, Season.Summer, Season.Fall, Season.Winter),
    };

    private static YearPlan PlanFor(List<CcItem> catalog)
        => new ContractGenerator().Generate(catalog, seed: 0);

    private static RunManager Mgr() => new RunManager(new GateEvaluator());

    // Default vaultGateSatisfied=true for tests that don't care about the vault gate.
    private const bool VaultOk = true;

    [Fact]
    public void Midweek_continues()
    {
        var catalog = Catalog();
        var run = new RunState { Season = Season.Spring, DayOfMonth = 3 };
        Assert.Equal(RunAction.Continue, Mgr().EvaluateDayEnd(run, PlanFor(catalog), catalog, VaultOk));
    }

    [Fact]
    public void Week_end_without_champion_continues()
    {
        var catalog = Catalog();
        var run = new RunState { Season = Season.Spring, DayOfMonth = 7 };
        Assert.Equal(RunAction.Continue, Mgr().EvaluateDayEnd(run, PlanFor(catalog), catalog, VaultOk));
    }

    [Fact]
    public void Month_end_with_all_assigned_items_donated_advances()
    {
        var catalog = Catalog();
        var plan = PlanFor(catalog);
        var run = new RunState { Season = Season.Spring, DayOfMonth = 28 };
        foreach (var c in plan.ForSeason(Season.Spring))
            foreach (var id in c.RequiredItemIds)
                run.RecordDonation(id);
        Assert.Equal(RunAction.AdvanceMonth, Mgr().EvaluateDayEnd(run, plan, catalog, VaultOk));
    }

    [Fact]
    public void Month_end_missing_a_spring_assigned_item_fails()
    {
        var catalog = Catalog();
        var run = new RunState { Season = Season.Spring, DayOfMonth = 28 };
        Assert.Equal(RunAction.FailReset, Mgr().EvaluateDayEnd(run, PlanFor(catalog), catalog, VaultOk));
    }

    [Fact]
    public void Month_end_with_items_done_but_vault_unpaid_fails()
    {
        // Item gate passes but vault gate (2,500g for Spring) hasn't been satisfied -> FailReset.
        var catalog = Catalog();
        var plan = PlanFor(catalog);
        var run = new RunState { Season = Season.Spring, DayOfMonth = 28 };
        foreach (var c in plan.ForSeason(Season.Spring))
            foreach (var id in c.RequiredItemIds)
                run.RecordDonation(id);
        Assert.Equal(RunAction.FailReset,
            Mgr().EvaluateDayEnd(run, plan, catalog, vaultGateSatisfied: false));
    }

    [Fact]
    public void Midweek_with_vault_unpaid_still_continues()
    {
        // Vault gate only applies at month-end; mid-month days never fail on vault.
        var catalog = Catalog();
        var run = new RunState { Season = Season.Spring, DayOfMonth = 14 };
        Assert.Equal(RunAction.Continue,
            Mgr().EvaluateDayEnd(run, PlanFor(catalog), catalog, vaultGateSatisfied: false));
    }

    [Fact]
    public void Winter_end_with_full_cc_and_vault_wins()
    {
        var catalog = Catalog();
        var plan = PlanFor(catalog);
        var run = new RunState { Season = Season.Winter, DayOfMonth = 28 };
        foreach (var item in catalog)
            run.RecordDonation(item.Id);
        Assert.Equal(RunAction.Win, Mgr().EvaluateDayEnd(run, plan, catalog, VaultOk));
    }

    [Fact]
    public void Winter_end_with_full_cc_but_vault_unpaid_fails()
    {
        var catalog = Catalog();
        var plan = PlanFor(catalog);
        var run = new RunState { Season = Season.Winter, DayOfMonth = 28 };
        foreach (var item in catalog)
            run.RecordDonation(item.Id);
        Assert.Equal(RunAction.FailReset,
            Mgr().EvaluateDayEnd(run, plan, catalog, vaultGateSatisfied: false));
    }
}
