using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

[Collection("i18n")]
public class I18nFixtureTests
{
    [Fact]
    public void Fixture_LoadsRealDefaultJson()
    {
        Assert.Equal("The festival is over.", Strings.Get("hud.festival-over"));
    }
}
