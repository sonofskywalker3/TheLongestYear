using System.Linq;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class DoubleWeekTests
{
    private const int WeeksPerYear = 16, WeeksPerSeason = 4;

    [Fact]
    public void Off_is_never_a_double_week()
        => Assert.All(Enumerable.Range(1, WeeksPerYear), w => Assert.False(DoubleWeek.Is(123, w, false)));

    [Fact]
    public void On_gives_exactly_one_double_week_per_season_in_week_two_or_three()
    {
        for (int seed = 0; seed < 200; seed++)
            for (int season = 0; season < 4; season++)
            {
                var hits = Enumerable.Range(season * WeeksPerSeason + 1, WeeksPerSeason).Where(w => DoubleWeek.Is(seed, w, true)).ToList();
                Assert.Single(hits);
                int weekInMonth = (hits[0] - 1) % WeeksPerSeason + 1;
                Assert.True(weekInMonth == 2 || weekInMonth == 3);
            }
    }

    [Fact]
    public void It_is_deterministic_and_both_weeks_occur()
    {
        Assert.Equal(DoubleWeek.Is(7, 2, true), DoubleWeek.Is(7, 2, true));
        var weeks = Enumerable.Range(0, 200).Select(s => Enumerable.Range(1, WeeksPerSeason).First(w => DoubleWeek.Is(s, w, true))).ToList();
        Assert.Contains(2, weeks);
        Assert.Contains(3, weeks);
    }
}
