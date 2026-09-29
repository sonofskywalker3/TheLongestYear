using TheLongestYear.Core;

namespace TheLongestYear.Tests;

public class WeeklyGoalDiscountTests
{
    [Theory]
    [InlineData(31, 0.5, 15)]
    [InlineData(31, 0.25, 23)]
    [InlineData(14, 0.5, 10)]
    [InlineData(14, 0.25, 10)]
    [InlineData(11, 0.5, 10)]
    [InlineData(11, 0.25, 10)]
    [InlineData(10, 0.5, 10)]
    [InlineData(9, 0.5, 9)]
    [InlineData(1, 0.5, 1)]
    [InlineData(49, 0.5, 24)]
    [InlineData(43, 0.25, 32)]
    [InlineData(31, 0.0, 31)]
    [InlineData(14, 0.0, 14)]
    [InlineData(11, 0.0, 11)]
    public void Stack_rounds_down_and_never_goes_below_ten(int original, double discount, int expected)
        => Assert.Equal(expected, WeeklyGoalDiscount.Stack(original, discount));

    [Theory]
    [InlineData(DifficultyStep.Easy, 0.5)]
    [InlineData(DifficultyStep.Normal, 0.25)]
    [InlineData(DifficultyStep.Hard, 0.0)]
    [InlineData(DifficultyStep.Extreme, 0.0)]
    public void Resolver_keys_the_discount_on_the_stack_size_dial(DifficultyStep step, double expected)
    {
        var p = DifficultyResolver.Resolve(new DifficultySettings { StackSize = step }, new GameplayConfig());

        Assert.Equal(expected, p.WeeklyGoalStackDiscount);
        Assert.Equal(expected, p.EffectiveWeeklyGoalStackDiscount());
    }

    /// <summary>A profile stamped before the discount existed has no value for it; it falls back to
    /// its own stamped Stack size step, never to live config.</summary>
    [Theory]
    [InlineData(DifficultyStep.Easy, 0.5)]
    [InlineData(DifficultyStep.Normal, 0.25)]
    [InlineData(DifficultyStep.Hard, 0.0)]
    public void A_profile_stamped_before_the_discount_reads_its_stamped_step(DifficultyStep step, double expected)
    {
        var p = new DifficultyProfile
        {
            WeeklyGoalStackDiscount = null,
            Steps = new DifficultySettings { StackSize = step },
        };

        Assert.Equal(expected, p.EffectiveWeeklyGoalStackDiscount());
    }
}
