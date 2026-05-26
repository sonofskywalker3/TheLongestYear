using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class GateEvaluatorTests
{
    private static GateEvaluator E() => new GateEvaluator();

    [Fact]
    public void Midweek_day_always_continues()
        => Assert.Equal(GateResult.Continue,
            E().EvaluateDayEnd(dayOfMonth: 3, monthIndex: 0, monthlyGatePasses: false, fullCcDone: false));

    [Fact]
    public void Week_end_that_is_not_month_end_always_continues()
        // Spec change 2026-05-26: no weekly fail; championing is opt-in.
        => Assert.Equal(GateResult.Continue,
            E().EvaluateDayEnd(7, 0, monthlyGatePasses: false, fullCcDone: false));

    [Fact]
    public void Day_14_with_failing_gate_continues()
        => Assert.Equal(GateResult.Continue,
            E().EvaluateDayEnd(14, 0, monthlyGatePasses: false, fullCcDone: false));

    [Fact]
    public void Month_end_with_failing_gate_fails_monthly()
        => Assert.Equal(GateResult.MonthlyFail,
            E().EvaluateDayEnd(28, 0, monthlyGatePasses: false, fullCcDone: false));

    [Fact]
    public void Month_end_with_passing_gate_continues_when_not_winter()
        => Assert.Equal(GateResult.Continue,
            E().EvaluateDayEnd(28, 0, monthlyGatePasses: true, fullCcDone: false));

    [Fact]
    public void Winter_end_with_full_cc_done_wins()
        => Assert.Equal(GateResult.Win,
            E().EvaluateDayEnd(28, 3, monthlyGatePasses: true, fullCcDone: true));

    [Fact]
    public void Winter_end_with_passing_gate_but_cc_incomplete_fails()
        // Multi-season items left over at end of Winter can never be donated later -> fail.
        => Assert.Equal(GateResult.MonthlyFail,
            E().EvaluateDayEnd(28, 3, monthlyGatePasses: true, fullCcDone: false));

    [Fact]
    public void Winter_end_with_failing_gate_fails()
        => Assert.Equal(GateResult.MonthlyFail,
            E().EvaluateDayEnd(28, 3, monthlyGatePasses: false, fullCcDone: false));
}
