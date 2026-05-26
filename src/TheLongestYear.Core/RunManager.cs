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
/// The day-end decision maker. Spec 2026-05-26 round 3: at day 28, both gates must pass:
///   (a) every item the YearPlan placed in this season is donated, AND
///   (b) the season's Vault bundle is paid (or keep_bus_unlocked is owned — see VaultRules).
/// Multi-season items are pinned at generator time, so "easy fish in two seasons is still
/// required in the first" is enforced before the gate ever runs. End-of-Winter additionally
/// requires fullCcDone (catalog-side belt-and-suspenders).
/// </summary>
public sealed class RunManager
{
    private readonly GateEvaluator _gate;

    public RunManager(GateEvaluator gate) => _gate = gate ?? throw new ArgumentNullException(nameof(gate));

    public RunAction EvaluateDayEnd(
        RunState run,
        YearPlan plan,
        IReadOnlyList<CcItem> catalog,
        bool vaultGateSatisfied)
    {
        if (run is null) throw new ArgumentNullException(nameof(run));
        if (plan is null) throw new ArgumentNullException(nameof(plan));
        if (catalog is null) throw new ArgumentNullException(nameof(catalog));

        ISet<string> donated = run.DonatedSet();

        bool itemsGatePassed = plan.ForSeason(run.Season).All(c => c.IsSatisfiedBy(donated));

        // Both gates must pass at month-end. Off-month days short-circuit true.
        bool monthlyGatePasses = !Calendar.IsMonthEnd(run.DayOfMonth) ||
            (itemsGatePassed && vaultGateSatisfied);

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
