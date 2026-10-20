using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class ActionArgsTests
{
    [Theory]
    [InlineData("MarkEventSeen Current 123", new[] { "MarkEventSeen", "Current", "123" })]
    [InlineData("  A   B  ", new[] { "A", "B" })]
    [InlineData("A \"two words\" C", new[] { "A", "two words", "C" })]
    [InlineData("A \"\" C", new[] { "A", "", "C" })]
    [InlineData("A pre\"fix x\"post", new[] { "A", "prefix xpost" })]
    [InlineData("A \\\"quoted\\\" B", new[] { "A", "\"quoted\"", "B" })]
    [InlineData("A \"unclosed text", new[] { "A", "unclosed text" })]
    public void Splits_like_vanilla(string input, string[] expected)
        => Assert.Equal(expected, ActionArgs.SplitBySpaceQuoteAware(input));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Empty_input_gives_no_args(string? input)
        => Assert.Empty(ActionArgs.SplitBySpaceQuoteAware(input));
}
