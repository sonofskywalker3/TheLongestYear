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
/// The day-end decision maker. Spec 2026-05-26 round 2: the gate is now per-contract-assignment.
/// At day 28, every item the YearPlan placed in this season must be in the run's donation ledger;
/// missing any one fails the run. Multi-season items are pinned at generator time (not at gate
/// time), so the rule "easy fish in two seasons is still required in the first" is enforced
/// before the gate ever runs. End-of-Winter additionally requires fullCcDone (belt-and-suspenders).
/// </summary>
public sealed class RunManager
{
    private readonly GateEvaluator _gate;

    public RunManager(GateEvaluator gate) => _gate = gate ?? throw new ArgumentNullException(nameof(gate));

    public RunAction EvaluateDayEnd(RunState run, YearPlan plan, IReadOnlyList<CcItem> catalog)
    {
        if (run is null) throw new ArgumentNullException(nameof(run));
        if (plan is null) throw new ArgumentNullException(nameof(plan));
        if (catalog is null) throw new ArgumentNullException(nameof(catalog));

        ISet<string> donated = run.DonatedSet();

        // Monthly gate: every contract assigned to this season is satisfied. (Off-month days
        // short-circuit true so they don't gate mid-month.)
        bool monthlyGatePasses = !Calendar.IsMonthEnd(run.DayOfMonth) ||
            plan.ForSeason(run.Season).All(c => c.IsSatisfiedBy(donated));

        bool fullCcDone = catalog.All(i => donated.Contains(i.Id));

        GateResult result = _gate.EvaluateDayEnd(
            run.DayOfMonth, (int)run.Season, monthlyGatePasses, fullCcDone);

        return result switch
        {
            GateResult.MonthlyFail => RunAction.FailReset,
            GateResult.Win => RunAction.Win,
            GateResult.Continue when Calendar.IsMonthEnd(run.DayOfMonth)
                => RunAction.AdvanceMonth,
            _ => RunAction.Continue
        };
    }
}
