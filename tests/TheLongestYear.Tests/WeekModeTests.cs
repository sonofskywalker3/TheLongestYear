using System.Collections.Generic;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class WeekModeTests
{
    private static ItemAvailabilityModel Model(WeekMode mode)
    {
        var derived = new Dictionary<string, ItemAvailability>
        {
            // Desert forage: pacing week 9 (Fall), hard week 6 (Summer).
            ["(O)90"] = new ItemAvailability(Season.Fall, 3, "cactus", EffortSource.Derived, 9, Season.Fall, HardWeek: 6),
        };
        return new ItemAvailabilityModel(derived, mode: mode);
    }

    [Fact]
    public void Pacing_mode_reads_the_pacing_week_for_gates_and_goals()
    {
        ItemAvailability a = Model(WeekMode.Pacing).For("(O)90");
        Assert.Equal(9, a.Week);
        Assert.Equal(9, a.GoalWeek);
        Assert.Equal(Season.Fall, a.Gate);
        Assert.Equal(6, a.HardWeek);
    }

    [Fact]
    public void HardGates_mode_moves_the_gate_but_not_the_goal()
    {
        ItemAvailability a = Model(WeekMode.HardGates).For("(O)90");
        Assert.Equal(6, a.Week);
        Assert.Equal(Season.Summer, a.Gate);
        Assert.Equal(9, a.GoalWeek);
    }

    [Fact]
    public void HardAll_mode_moves_both()
    {
        ItemAvailability a = Model(WeekMode.HardAll).For("(O)90");
        Assert.Equal(6, a.Week);
        Assert.Equal(6, a.GoalWeek);
    }

    [Fact]
    public void Hard_week_defaults_to_the_pacing_week()
    {
        var a = new ItemAvailability(Season.Spring, 1, "quartz", EffortSource.Derived, 1, Season.Spring);
        Assert.Equal(1, a.HardWeekOrPacing);
    }

    [Theory]
    [InlineData(DifficultyStep.Easy, WeekMode.Pacing)] [InlineData(DifficultyStep.Normal, WeekMode.Pacing)]
    [InlineData(DifficultyStep.Hard, WeekMode.HardGates)] [InlineData(DifficultyStep.Extreme, WeekMode.HardAll)]
    public void Difficulty_picks_the_week_mode(DifficultyStep step, WeekMode mode) => Assert.Equal(mode, WeekModes.For(step));
}
