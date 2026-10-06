using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class ShrineGoalSamplerTests
{
    private static readonly string[] Pool = Enumerable.Range(1, 20).Select(i => $"(O){i}").ToArray();
    private static readonly IReadOnlyList<GoalGroupCap> NoCaps = new List<GoalGroupCap>();

    [Theory]
    [InlineData(DifficultyStep.Easy, 3)]
    [InlineData(DifficultyStep.Normal, 4)]
    [InlineData(DifficultyStep.Hard, 5)]
    [InlineData(DifficultyStep.Extreme, 6)]
    public void Target_per_step(DifficultyStep step, int expected)
        => Assert.Equal(expected, ShrineGoalSampler.TargetFor(step));

    [Fact]
    public void Pick_is_deterministic_and_independent_of_input_order()
    {
        var a = ShrineGoalSampler.Pick(7, 3, Theme.Mining, Pool, 4, NoCaps);
        var b = ShrineGoalSampler.Pick(7, 3, Theme.Mining, Pool.Reverse().ToList(), 4, NoCaps);
        Assert.Equal(a, b);
    }

    [Fact]
    public void Pick_is_distinct_and_the_requested_size()
    {
        var p = ShrineGoalSampler.Pick(1, 1, Theme.Fishing, Pool, 6, NoCaps);
        Assert.Equal(6, p.Count);
        Assert.Equal(6, p.Distinct().Count());
    }

    [Fact]
    public void Pick_respects_caps()
    {
        var caps = new List<GoalGroupCap> { new(new HashSet<string>(Pool.Take(15)), 1) };
        for (int seed = 0; seed < 30; seed++)
        {
            var p = ShrineGoalSampler.Pick(seed, 2, Theme.Farming, Pool, 4, caps);
            Assert.True(p.Count(x => caps[0].Ids.Contains(x)) <= 1);
            Assert.Equal(4, p.Count);
        }
    }

    [Fact]
    public void Pick_returns_fewer_when_the_pool_is_short_and_empty_for_zero()
    {
        Assert.Equal(2, ShrineGoalSampler.Pick(1, 1, Theme.Mining, new[] { "a", "b" }, 5, NoCaps).Count);
        Assert.Empty(ShrineGoalSampler.Pick(1, 1, Theme.Mining, Pool, 0, NoCaps));
    }

    [Theory]
    [InlineData(DifficultyStep.Easy)]
    [InlineData(DifficultyStep.Normal)]
    public void Easy_normal_need_placed_in_season_and_goal_week_reached(DifficultyStep step)
    {
        Assert.True(ShrineGoalSampler.IsAllowed(step, true, 2, 9, true, 2));
        Assert.False(ShrineGoalSampler.IsAllowed(step, false, 2, 9, true, 2));
        Assert.False(ShrineGoalSampler.IsAllowed(step, true, 2, 9, false, 2));
        Assert.False(ShrineGoalSampler.IsAllowed(step, true, 3, 1, true, 2));
    }

    [Theory]
    [InlineData(DifficultyStep.Hard)]
    [InlineData(DifficultyStep.Extreme)]
    public void Hard_extreme_ignore_season_and_use_the_hard_week(DifficultyStep step)
    {
        Assert.True(ShrineGoalSampler.IsAllowed(step, true, 9, 2, false, 2));
        Assert.False(ShrineGoalSampler.IsAllowed(step, true, 1, 3, true, 2));
        Assert.False(ShrineGoalSampler.IsAllowed(step, false, 1, 1, true, 2));
    }
}
