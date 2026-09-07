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
}
