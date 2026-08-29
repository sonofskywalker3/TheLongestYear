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
    public void A_renamed_display_name_is_tolerated_but_a_changed_item_is_not()
    {
        const string written = "Night Fishing/R 517 1/(O)269 1 0 (O)149 1 0 (O)132 1 0/1/3//Night Fishing";
        var generated = new Dictionary<string, string> { ["Fish Tank/9"] = written };

        var renamed = new Dictionary<string, string>
            { ["Fish Tank/9"] = "Night Fishing/R 517 1/(O)269 1 0 (O)149 1 0 (O)132 1 0/1/3//Night Fish" };
        Assert.False(EngineManifestCheck.Matches(generated, renamed));
        Assert.True(EngineManifestCheck.MatchesIgnoringDisplayName(generated, renamed));

        var swapped = new Dictionary<string, string>
            { ["Fish Tank/9"] = "Night Fishing/R 517 1/(O)269 1 0 (O)800 1 0 (O)132 1 0/1/3//Night Fish" };
        Assert.False(EngineManifestCheck.MatchesIgnoringDisplayName(generated, swapped));
        Assert.Equal("Night Fishing/R 517 1/(O)269 1 0 (O)149 1 0 (O)132 1 0/1/3/", EngineManifestCheck.Essential(written));
    }

    [Fact]
    public void Names_a_missing_live_key()
    {
        var generated = new Dictionary<string, string> { ["Pantry/0"] = "a" };
        Assert.Equal("Pantry/0: generated='a' live=(missing)",
            EngineManifestCheck.FirstDifference(generated, new Dictionary<string, string>()));
    }
}
