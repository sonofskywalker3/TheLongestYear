using System.Collections.Generic;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class EngineManifestCheckDifferenceTests
{
    [Fact]
    public void No_difference_when_the_live_board_matches()
    {
        var generated = new Dictionary<string, string> { ["Pantry/0"] = "a", ["Vault/34"] = "b" };
        var live = new Dictionary<string, string> { ["Pantry/0"] = "a", ["Vault/34"] = "b", ["Extra/99"] = "mod" };
        Assert.Null(EngineManifestCheck.FirstDifference(generated, live));
    }

    [Fact]
    public void Names_the_first_drifted_key_with_both_values()
    {
        var generated = new Dictionary<string, string> { ["Pantry/0"] = "a", ["Pantry/1"] = "b" };
        var live = new Dictionary<string, string> { ["Pantry/0"] = "a", ["Pantry/1"] = "changed" };
        Assert.Equal("Pantry/1: generated='b' live='changed'", EngineManifestCheck.FirstDifference(generated, live));
    }

    [Fact]
    public void Names_a_missing_live_key()
    {
        var generated = new Dictionary<string, string> { ["Pantry/0"] = "a" };
        Assert.Equal("Pantry/0: generated='a' live=(missing)",
            EngineManifestCheck.FirstDifference(generated, new Dictionary<string, string>()));
    }
}
