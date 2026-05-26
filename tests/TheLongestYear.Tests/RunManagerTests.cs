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

    /// <summary>Build a plan from the catalog using the live generator (deterministic).</summary>
    private static YearPlan PlanFor(List<CcItem> catalog)
        => new ContractGenerator().Generate(catalog, seed: 0);

    private static RunManager Mgr() => new RunManager(new GateEvaluator());

    [Fact]
    public void Midweek_continues()
    {
        var catalog = Catalog();
        var run = new RunState { Season = Season.Spring, DayOfMonth = 3 };
        Assert.Equal(RunAction.Continue, Mgr().EvaluateDayEnd(run, PlanFor(catalog), catalog));
    }

    [Fact]
    public void Week_end_without_champion_continues()
    {
        // Spec change 2026-05-26: no weekly fail.
        var catalog = Catalog();
        var run = new RunState { Season = Season.Spring, DayOfMonth = 7 };
        Assert.Equal(RunAction.Continue, Mgr().EvaluateDayEnd(run, PlanFor(catalog), catalog));
    }

    [Fact]
    public void Month_end_with_all_assigned_items_donated_advances()
    {
        var catalog = Catalog();
        var plan = PlanFor(catalog);
        var run = new RunState { Season = Season.Spring, DayOfMonth = 28 };
        // Donate everything the plan assigned to Spring.
        foreach (var c in plan.ForSeason(Season.Spring))
            foreach (var id in c.RequiredItemIds)
                run.RecordDonation(id);
        Assert.Equal(RunAction.AdvanceMonth, Mgr().EvaluateDayEnd(run, plan, catalog));
    }

    [Fact]
    public void Month_end_missing_a_spring_assigned_item_fails()
    {
        // "spring-only" is necessarily in the Spring contract; not donating it fails the gate.
        var catalog = Catalog();
        var run = new RunState { Season = Season.Spring, DayOfMonth = 28 };
        Assert.Equal(RunAction.FailReset, Mgr().EvaluateDayEnd(run, PlanFor(catalog), catalog));
    }

    [Fact]
    public void Spring_gate_does_not_require_summer_or_later_items()
    {
        var catalog = Catalog();
        var plan = PlanFor(catalog);
        var run = new RunState { Season = Season.Spring, DayOfMonth = 28 };
        foreach (var c in plan.ForSeason(Season.Spring))
            foreach (var id in c.RequiredItemIds)
                run.RecordDonation(id);
        // Summer/Fall/Winter assigned items NOT donated yet — Spring still advances.
        Assert.Equal(RunAction.AdvanceMonth, Mgr().EvaluateDayEnd(run, plan, catalog));
    }

    [Fact]
    public void Winter_end_with_full_cc_wins()
    {
        var catalog = Catalog();
        var plan = PlanFor(catalog);
        var run = new RunState { Season = Season.Winter, DayOfMonth = 28 };
        foreach (var item in catalog)
            run.RecordDonation(item.Id);
        Assert.Equal(RunAction.Win, Mgr().EvaluateDayEnd(run, plan, catalog));
    }

    [Fact]
    public void Winter_end_missing_a_winter_assigned_item_fails()
    {
        var catalog = Catalog();
        var plan = PlanFor(catalog);
        var run = new RunState { Season = Season.Winter, DayOfMonth = 28 };
        // Donate everything except items assigned to Winter contracts.
        var winterIds = plan.ForSeason(Season.Winter).SelectMany(c => c.RequiredItemIds).ToHashSet();
        foreach (var item in catalog)
            if (!winterIds.Contains(item.Id))
                run.RecordDonation(item.Id);
        Assert.Equal(RunAction.FailReset, Mgr().EvaluateDayEnd(run, plan, catalog));
    }
}
