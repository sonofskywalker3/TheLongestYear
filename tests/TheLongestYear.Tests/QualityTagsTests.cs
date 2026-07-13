using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

[Collection("i18n")]
public class QualityTagsTests
{
    public QualityTagsTests(I18nFixture _) { }

    [Theory]
    [InlineData(0, "")]
    [InlineData(1, " (silver)")]
    [InlineData(2, " (gold)")]
    [InlineData(4, " (iridium)")]
    public void For_MatchesCurrentEnglish(int quality, string expected)
        => Assert.Equal(expected, QualityTags.For(quality));
}
