using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class ThemeModifiersTests
{
    [Theory]
    [InlineData(Theme.Foraging)]
    [InlineData(Theme.Farming)]
    [InlineData(Theme.Fishing)]
    [InlineData(Theme.Mining)]
    [InlineData(Theme.Mixed)]
    public void Every_theme_has_a_distinct_nonempty_bonus_and_liability(Theme theme)
    {
        var (bonus, liability) = ThemeModifiers.For(theme);
        Assert.False(string.IsNullOrWhiteSpace(bonus));
        Assert.False(string.IsNullOrWhiteSpace(liability));
        Assert.NotEqual(bonus, liability);
    }
}
