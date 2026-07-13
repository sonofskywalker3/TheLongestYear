using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

[Collection("i18n")]
public class WinSummaryTests
{
    public WinSummaryTests(I18nFixture _) { }

    [Fact]
    public void Loop_one_reads_as_a_first_loop_brag()
        => Assert.Equal("You restored it on your very first loop!", WinSummary.LoopLine(1));

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void Non_positive_run_numbers_fall_back_to_the_first_loop_line(int runNumber)
        => Assert.Equal("You restored it on your very first loop!", WinSummary.LoopLine(runNumber));

    [Theory]
    [InlineData(2, "It took 2 loops.")]
    [InlineData(5, "It took 5 loops.")]
    [InlineData(12, "It took 12 loops.")]
    public void Later_loops_count_the_attempts(int runNumber, string expected)
        => Assert.Equal(expected, WinSummary.LoopLine(runNumber));
}
