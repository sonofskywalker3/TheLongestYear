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
/// The day-end decision maker. Derives the gate inputs from the run's donation ledger against the
/// CC catalog, runs <see cref="GateEvaluator"/>, and maps the result to a <see cref="RunAction"/>.
/// Spec change 2026-05-26: gating is now catalog-driven (single-season-this-month items), not
/// contract-driven. The YearPlan is no longer needed for gate evaluation.
/// </summary>
public sealed class RunManager
{
    private readonly GateEvaluator _gate;

    public RunManager(GateEvaluator gate) => _gate = gate ?? throw new ArgumentNullException(nameof(gate));

    public RunAction EvaluateDayEnd(RunState run, IReadOnlyList<CcItem> catalog)
    {
        if (run is null) throw new ArgumentNullException(nameof(run));
        if (catalog is null) throw new ArgumentNullException(nameof(catalog));

        ISet<string> donated = run.DonatedSet();

        // Monthly gate: items that can ONLY be obtained this season must be donated by day 28.
        // Multi-season items get a pass — they have future seasons to land in (except at Winter end,
        // which the GateEvaluator handles via fullCcDone).
        bool monthlyGatePasses = !Calendar.IsMonthEnd(run.DayOfMonth) ||
            catalog
                .Where(i => i.ObtainableSeasons.Count == 1 && i.ObtainableSeasons.Contains(run.Season))
                .All(i => donated.Contains(i.Id));

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
