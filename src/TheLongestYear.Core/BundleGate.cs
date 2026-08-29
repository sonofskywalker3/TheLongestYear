using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core;

/// <summary>
/// Pure evaluator that ANDs every bundle's <see cref="BundleRequirement.IsSatisfiedAtSeasonEnd"/>
/// with the vault gate. Replaces the older pool-count YearPlan gate (spec round 7, 2026-05-26):
/// the bundle-shape model is the source of truth now, vanilla bundles are donated to directly,
/// and the gate question is "does the per-slot ledger fill every bundle's season-N
/// requirement at day 28."
/// </summary>
public static class BundleGate
{
    /// <summary>True if every bundle passes its season-N gate AND the vault gate is satisfied.
    /// Use at day-28 month-end to decide pass/fail.</summary>
    public static bool IsSatisfied(
        Season currentSeason,
        SlotLedger ledger,
        IReadOnlyList<BundleRequirement> bundles,
        bool vaultGateSatisfied)
    {
        if (ledger is null || bundles is null) return false;
        if (!vaultGateSatisfied) return false;
        return bundles.All(b => b.IsSatisfiedAtSeasonEnd(currentSeason, ledger));
    }

    /// <summary>True if every bundle is fully complete (at least X of its slots filled). Used at end-of-Winter to verify full CC restoration
    /// before declaring Win.</summary>
    public static bool IsFullyDone(
        SlotLedger ledger,
        IReadOnlyList<BundleRequirement> bundles)
    {
        if (ledger is null || bundles is null) return false;
        return bundles.All(b => b.IsFullyComplete(ledger));
    }
}
