using TheLongestYear.Core.Ending;
using Xunit;

namespace TheLongestYear.Tests;

public class WinNightRuleTests
{
    [Theory]
    [InlineData(true, false, false, true)]   // board done, first time: arm
    [InlineData(true, true, false, false)]   // already acknowledged a win: silent
    [InlineData(true, false, true, false)]   // already armed (festival defer): do not re-arm
    [InlineData(false, false, false, false)] // board not done
    public void ShouldArm_only_on_a_fresh_completed_board(bool done, bool acked, bool armed, bool expected)
        => Assert.Equal(expected, WinNightRule.ShouldArm(done, acked, armed));

    [Theory]
    [InlineData(1, 0, 2, 0)]
    [InlineData(27, 0, 28, 0)]
    [InlineData(28, 0, 1, 1)]
    [InlineData(28, 3, 1, 0)]   // Winter 28 rolls to Spring (month 0)
    public void Tomorrow_rolls_the_month_on_28(int day, int month, int expDay, int expMonth)
        => Assert.Equal((expDay, expMonth), WinNightRule.Tomorrow(day, month));
}
