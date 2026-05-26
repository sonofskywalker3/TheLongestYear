using System;
using System.Collections.Generic;

namespace TheLongestYear.Core;

/// <summary>What the mod should do after a day ends.</summary>
public enum RunAction
{
    /// <summary>Keep playing (mid-week, or a passed weekly checkpoint that isn't month-end).</summary>
    Continue,
    /// <summary>A gate failed — perform the in-place reset and start a fresh run.</summary>
    FailReset,
    /// <summary>Month cleared (not Winter) — let the game advance; clear this month's selections.</summary>
    AdvanceMonth,
    /// <summary>Winter cleared with the whole CC done — the loop breaks.</summary>
    Win
}

/// <summary>
/// The day-end decision maker. Spec round 7 (2026-05-26): gate is per-bundle now, not pooled
/// per (season, theme) contract. At day 28, every <see cref="BundleRequirement"/>'s
/// <see cref="BundleRequirement.IsSatisfiedAtSeasonEnd"/> must pass for the current season AND
/// the vault gate (paid bundle or keep_bus_unlocked upgrade) must be satisfied. End-of-Winter
/// additionally demands every bundle be fully complete (X distinct ingredients donated).
/// </summary>
public sealed class RunManager
{
    private readonly GateEvaluator _gate;

    public RunManager(GateEvaluator gate) => _gate = gate ?? throw new ArgumentNullException(nameof(gate));

    /// <summary>
    /// Evaluate the end-of-day gate. Returns the action the controller should take.
    /// </summary>
    /// <param name="run">Live run state — only Season, DayOfMonth, and DonatedItemIds are read.</param>
    /// <param name="bundles">Classified bundle requirements (from <c>BundleCatalogBuilder.BuildRequirements</c>).</param>
    /// <param name="vaultGateSatisfied">True if this season's vault bundle is paid, or
    /// the player owns the keep_bus_unlocked meta upgrade — see <see cref="VaultRules"/>.</param>
    public RunAction EvaluateDayEnd(
        RunState run,
        IReadOnlyList<BundleRequirement> bundles,
        bool vaultGateSatisfied)
    {
        if (run is null) throw new ArgumentNullException(nameof(run));
        if (bundles is null) throw new ArgumentNullException(nameof(bundles));

        ISet<string> donated = run.DonatedSet();

        // Bundle gate (AND with vault) only matters at month-end; off-month days short-circuit true.
        bool monthlyGatePasses = !Calendar.IsMonthEnd(run.DayOfMonth)
            || BundleGate.IsSatisfied(run.Season, donated, bundles, vaultGateSatisfied);

        bool fullCcDone = BundleGate.IsFullyDone(donated, bundles);

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
