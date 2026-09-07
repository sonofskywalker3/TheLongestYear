using TheLongestYear.Core.Ending;
using Xunit;

namespace TheLongestYear.Tests;

public class Year2WallRuleTests
{
    [Theory]
    [InlineData(2, true, false, true)]    // the case: year 2, armed, no year-2 content
    [InlineData(3, true, false, true)]    // any later year too
    [InlineData(1, true, false, false)]   // still year 1: the prep season
    [InlineData(2, false, false, false)]  // legacy keep-playing save: exempt
    [InlineData(2, true, true, false)]    // the Year 2 update has taken over
    public void ShouldShow(int year, bool armed, bool started, bool expected)
        => Assert.Equal(expected, Year2WallRule.ShouldShow(year, armed, started));
}
