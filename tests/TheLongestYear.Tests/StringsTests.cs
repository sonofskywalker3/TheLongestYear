using System;
using System.Collections.Generic;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class StringsTests : IDisposable
{
    public StringsTests() => Strings.Reset();

    /// <summary>Restores the global i18n provider after this class runs so any later
    /// "i18n"-collection test class doesn't see a null provider left behind by Reset().</summary>
    public void Dispose() => I18nFixture.InstallGlobalProvider();

    [Fact]
    public void Get_WithoutInit_ReturnsKeyItself()
    {
        Assert.Equal("menu.hub.title", Strings.Get("menu.hub.title"));
    }

    [Fact]
    public void Get_UsesInjectedProvider()
    {
        Strings.Init((key, _) => key == "hud.festival-over" ? "The festival is over." : key);
        Assert.Equal("The festival is over.", Strings.Get("hud.festival-over"));
    }

    [Fact]
    public void Get_PassesTokensToProvider()
    {
        IReadOnlyDictionary<string, string>? seen = null;
        Strings.Init((key, tokens) => { seen = tokens; return "x"; });
        Strings.Get("menu.shrine.cost", new Dictionary<string, string> { ["cost"] = "150" });
        Assert.NotNull(seen);
        Assert.Equal("150", seen!["cost"]);
    }

    [Fact]
    public void Get_TokenlessOverload_PassesNullTokens()
    {
        bool sawNull = false;
        Strings.Init((key, tokens) => { sawNull = tokens == null; return "x"; });
        Strings.Get("any.key");
        Assert.True(sawNull);
    }
}
