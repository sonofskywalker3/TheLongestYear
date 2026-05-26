using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core;

/// <summary>What the mod should do after a day ends.</summary>
public enum RunAction
{
    /// <summary>Keep playing (mid-week, or a passed weekly checkpoint that isn't month-end).</summary>
    Continue,
    /// <summary>A gate failed — perform the in-place reset and start a fresh run.</summary>
    FailReset,
    /// <summary>Month cleared (not Winter) — let the game advance; clear this month's championing.</summary>
    AdvanceMonth,
    /// <summary>Winter cleared with the whole CC done — the loop breaks.</summary>
    Win
}

/// <summary>
/// The day-end decision maker. Derives the gate inputs from the run's ledger against its YearPlan,
/// runs <see cref="GateEvaluator"/>, and maps the result to a <see cref="RunAction"/>.
/// </summary>
public sealed class RunManager
{
    private readonly GateEvaluator _gate;

    public RunManager(GateEvaluator gate) => _gate = gate ?? throw new ArgumentNullException(nameof(gate));

    public RunAction EvaluateDayEnd(RunState run, YearPlan plan)
    {
        if (run is null) throw new ArgumentNullException(nameof(run));
        if (plan is null) throw new ArgumentNullException(nameof(plan));

        ISet<string> donated = run.DonatedSet();

        // No champion at week's end is a weekly failure (you must champion one each week).
        bool championedComplete =
            run.CurrentChampion.HasValue &&
            plan.Get(run.Season, run.CurrentChampion.Value).IsSatisfiedBy(donated);

        bool allFiveComplete = plan.ForSeason(run.Season).All(c => c.IsSatisfiedBy(donated));

        GateResult result = _gate.EvaluateDayEnd(
            run.DayOfMonth, (int)run.Season, championedComplete, allFiveComplete);

        return result switch
        {
            GateResult.WeeklyFail => RunAction.FailReset,
            GateResult.MonthlyFail => RunAction.FailReset,
            GateResult.Win => RunAction.Win,
            GateResult.Continue when Calendar.IsMonthEnd(run.DayOfMonth) && allFiveComplete
                => RunAction.AdvanceMonth,
            _ => RunAction.Continue
        };
    }
}
