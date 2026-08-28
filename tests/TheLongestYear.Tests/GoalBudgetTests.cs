using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>Spec 2026-08-28-theme-week-budget: the weekly goal count is the pool spread over
/// the weeks left in the season, capped by the season ceiling.</summary>
public class GoalBudgetTests
{
    [Theory]
    [InlineData(1, 4)]
    [InlineData(4, 1)]
    [InlineData(5, 4)]
    [InlineData(13, 4)]
    [InlineData(16, 1)]
    public void Weeks_left_counts_this_week(int weekOfYear, int expected)
        => Assert.Equal(expected, GoalBudget.WeeksLeftInSeason(weekOfYear));

    [Fact]
    public void Empty_pool_asks_for_nothing()
        => Assert.Equal(0, GoalBudget.For(7, 0, 0, 99, 4));

    [Fact]
    public void Spring_week_1_spreads_the_due_lines_over_four_weeks()
        => Assert.Equal(3, GoalBudget.For(3, 11, 20, 0, 4));   // ceil(11/4) = 3, filler allowance 0

    [Fact]
    public void Thin_pool_asks_for_less_than_the_cap()
        => Assert.Equal(2, GoalBudget.For(7, 5, 0, 99, 4));    // ceil(5/4) = 2

    [Fact]
    public void Last_week_asks_for_everything_left_up_to_the_cap()
    {
        Assert.Equal(2, GoalBudget.For(7, 2, 0, 99, 1));
        Assert.Equal(7, GoalBudget.For(7, 12, 0, 99, 1));
    }

    [Fact]
    public void Filler_counts_only_as_far_as_the_allowance_lets_it_be_asked()
    {
        // Fall: 1 filler a week, 4 weeks left -> at most 4 of the 20 filler lines count.
        Assert.Equal(2, GoalBudget.For(5, 3, 20, 1, 4));       // ceil((3 + 4) / 4) = 2
        // Winter: unlimited filler, all 20 count.
        Assert.Equal(6, GoalBudget.For(7, 3, 20, 99, 4));      // ceil(23 / 4) = 6
    }

    [Theory]
    [InlineData(2, 2)]
    [InlineData(3, 2)]
    [InlineData(4, 2)]
    [InlineData(8, 2)]
    [InlineData(9, 3)]
    public void Small_domains_stay_offerable_by_asking_at_least_two(int lines, int expected)
        => Assert.Equal(expected, GoalBudget.For(7, lines, 0, 99, 4));

    [Fact]
    public void One_askable_line_still_makes_a_one_goal_week()
        => Assert.Equal(1, GoalBudget.For(7, 1, 0, 99, 4));

    [Fact]
    public void Zero_cap_asks_for_nothing()
        => Assert.Equal(0, GoalBudget.For(0, 9, 9, 99, 4));
}
