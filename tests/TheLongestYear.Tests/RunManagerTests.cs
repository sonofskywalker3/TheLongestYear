using System.Collections.Generic;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class RunManagerTests
{
    // A YearPlan where, per season, Mining requires "ore" and the other four themes require nothing.
    private static YearPlan PlanRequiringMiningOre()
    {
        var contracts = new List<Contract>();
        foreach (Season s in System.Enum.GetValues(typeof(Season)))
            foreach (Theme t in System.Enum.GetValues(typeof(Theme)))
            {
                var required = t == Theme.Mining ? new[] { "ore" } : System.Array.Empty<string>();
                var (b, l) = ThemeModifiers.For(t);
                contracts.Add(new Contract(s, t, required, b, l));
            }
        return new YearPlan(contracts);
    }

    private static RunManager Mgr() => new RunManager(new GateEvaluator());

    [Fact]
    public void Midweek_continues()
    {
        var run = new RunState { Season = Season.Spring, DayOfMonth = 3 };
        run.Champion(Theme.Mining);
        Assert.Equal(RunAction.Continue, Mgr().EvaluateDayEnd(run, PlanRequiringMiningOre()));
    }

    [Fact]
    public void Week_end_with_satisfied_champion_continues()
    {
        var run = new RunState { Season = Season.Spring, DayOfMonth = 7 };
        run.Champion(Theme.Mining);
        run.RecordDonation("ore");
        Assert.Equal(RunAction.Continue, Mgr().EvaluateDayEnd(run, PlanRequiringMiningOre()));
    }

    [Fact]
    public void Week_end_with_unsatisfied_champion_fails()
    {
        var run = new RunState { Season = Season.Spring, DayOfMonth = 7 };
        run.Champion(Theme.Mining); // "ore" not donated
        Assert.Equal(RunAction.FailReset, Mgr().EvaluateDayEnd(run, PlanRequiringMiningOre()));
    }

    [Fact]
    public void Week_end_with_no_champion_fails()
    {
        var run = new RunState { Season = Season.Spring, DayOfMonth = 7 }; // never championed
        run.RecordDonation("ore");
        Assert.Equal(RunAction.FailReset, Mgr().EvaluateDayEnd(run, PlanRequiringMiningOre()));
    }

    [Fact]
    public void Month_end_all_five_done_not_winter_advances_month()
    {
        var run = new RunState { Season = Season.Spring, DayOfMonth = 28 };
        run.Champion(Theme.Mining);
        run.RecordDonation("ore"); // only Mining requires anything; all five now satisfied
        Assert.Equal(RunAction.AdvanceMonth, Mgr().EvaluateDayEnd(run, PlanRequiringMiningOre()));
    }

    [Fact]
    public void Month_end_missing_required_item_fails()
    {
        var run = new RunState { Season = Season.Spring, DayOfMonth = 28 };
        run.Champion(Theme.Foraging); // champion an empty contract -> championed-complete is true
        // "ore" NOT donated, so the (unchampioned) Mining contract is incomplete -> monthly fail.
        Assert.Equal(RunAction.FailReset, Mgr().EvaluateDayEnd(run, PlanRequiringMiningOre()));
    }

    [Fact]
    public void Winter_end_all_done_wins()
    {
        var run = new RunState { Season = Season.Winter, DayOfMonth = 28 };
        run.Champion(Theme.Mining);
        run.RecordDonation("ore");
        Assert.Equal(RunAction.Win, Mgr().EvaluateDayEnd(run, PlanRequiringMiningOre()));
    }
}
