namespace TheLongestYear.Core;

public enum GateResult
{
    /// <summary>Keep playing (mid-week, mid-month, or a passed checkpoint that isn't the final win).</summary>
    Continue,
    /// <summary>Reserved — no longer produced by the evaluator (spec 2026-05-26 removed the weekly fail).</summary>
    WeeklyFail,
    /// <summary>Single-season-this-month items still undonated at day 28 — run ends.</summary>
    MonthlyFail,
    /// <summary>Winter cleared with the full CC restored — loop breaks.</summary>
    Win
}

/// <summary>
/// Decides, at the end of a day, whether the run continues, fails, or wins.
/// Spec change 2026-05-26: there is no weekly fail anymore (selection is now JP-bonus opt-in,
/// not a gate). The only gate is monthly: at day 28, every CC item whose obtainable seasons are
/// EXACTLY {thisSeason} must be donated. Multi-season items don't gate any month except Winter —
/// at end of Winter there's no future season, so a missing item (single- or multi-season) is fatal.
/// </summary>
public sealed class GateEvaluator
{
    public GateResult EvaluateDayEnd(int dayOfMonth, int monthIndex, bool monthlyGatePasses, bool fullCcDone)
    {
        if (!Calendar.IsMonthEnd(dayOfMonth))
            return GateResult.Continue;

        if (!monthlyGatePasses)
            return GateResult.MonthlyFail;

        if (monthIndex == Calendar.MonthsPerYear - 1)
        {
            // End of Winter: any leftover CC item (including multi-season) cannot be donated later,
            // so a non-restored CC at Winter day 28 is a fail.
            return fullCcDone ? GateResult.Win : GateResult.MonthlyFail;
        }

        return GateResult.Continue;
    }
}
