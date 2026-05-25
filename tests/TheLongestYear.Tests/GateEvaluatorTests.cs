using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class GateEvaluatorTests
{
    private static GateEvaluator E() => new GateEvaluator();

    [Fact]
    public void Midweek_day_always_continues()
        => Assert.Equal(GateResult.Continue,
            E().EvaluateDayEnd(dayOfMonth: 3, monthIndex: 0, championedComplete: false, allFiveComplete: false));

    [Fact]
    public void Week_end_with_incomplete_champion_fails_weekly()
        => Assert.Equal(GateResult.WeeklyFail,
            E().EvaluateDayEnd(7, 0, championedComplete: false, allFiveComplete: false));

    [Fact]
    public void Week_end_with_complete_champion_continues()
        => Assert.Equal(GateResult.Continue,
            E().EvaluateDayEnd(7, 0, championedComplete: true, allFiveComplete: false));

    [Fact]
    public void Month_end_missing_fifth_contract_fails_monthly()
        => Assert.Equal(GateResult.MonthlyFail,
            E().EvaluateDayEnd(28, 0, championedComplete: true, allFiveComplete: false));

    [Fact]
    public void Month_end_all_complete_advances_when_not_winter()
        => Assert.Equal(GateResult.Continue,
            E().EvaluateDayEnd(28, 0, championedComplete: true, allFiveComplete: true));

    [Fact]
    public void Winter_end_all_complete_wins()
        => Assert.Equal(GateResult.Win,
            E().EvaluateDayEnd(28, 3, championedComplete: true, allFiveComplete: true));

    [Fact]
    public void Month_end_incomplete_champion_fails_weekly_first()
        => Assert.Equal(GateResult.WeeklyFail,
            E().EvaluateDayEnd(28, 3, championedComplete: false, allFiveComplete: true));
}
