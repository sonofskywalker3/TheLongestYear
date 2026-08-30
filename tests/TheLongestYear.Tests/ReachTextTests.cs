using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class ReachTextTests
{
    public ReachTextTests() => I18nFixture.InstallGlobalProvider();

    [Theory]
    [InlineData("skill:farming:3", "unlocked at Farming 3")]
    [InlineData("building:Stable", "unlocked once it's built")]
    [InlineData("mine:40", "unlocked at floor 40")]
    [InlineData("tool:watering_can:2", "unlocked with a Steel Watering Can")]
    public void Describe_names_the_requirement_in_words(string requirement, string contains)
        => Assert.Contains(contains, ReachText.Describe(requirement));

    [Fact]
    public void Empty_requirement_is_empty_text() => Assert.Equal("", ReachText.Describe(null));
}
