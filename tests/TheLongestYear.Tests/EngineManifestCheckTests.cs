using System.Collections.Generic;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class EngineManifestCheckTests
{
    [Fact]
    public void Matches_ReturnsTrue_WhenGeneratedAndLiveAreIdentical()
    {
        var generated = new Dictionary<string, string>
        {
            ["Pantry/0"] = "Spring Crops/reward/24 1 0/0/4//Spring Crops",
            ["CraftsRoom/0"] = "Spring Foraging/reward/16 1 0/0/4//Spring Foraging",
        };
        var live = new Dictionary<string, string>(generated);

        Assert.True(EngineManifestCheck.Matches(generated, live));
    }

    [Fact]
    public void Matches_ReturnsFalse_WhenLiveIsMissingAGeneratedKey()
    {
        var generated = new Dictionary<string, string>
        {
            ["Pantry/0"] = "Spring Crops/reward/24 1 0/0/4//Spring Crops",
            ["CraftsRoom/0"] = "Spring Foraging/reward/16 1 0/0/4//Spring Foraging",
        };
        var live = new Dictionary<string, string>
        {
            ["Pantry/0"] = "Spring Crops/reward/24 1 0/0/4//Spring Crops",
        };

        Assert.False(EngineManifestCheck.Matches(generated, live));
    }

    [Fact]
    public void Matches_ReturnsFalse_WhenSameKeyHasADifferentValue_OlderGenerationScenario()
    {
        // Reproduces the reachable stale-board reload: MetaStore.Save persists the bumped
        // BundlesGeneratedForReset/CompletedResets counters BEFORE ForceFullSave writes the
        // world. A crash/skip in that window reloads with the NEW meta but the OLD generation's
        // bundles still on the board. The key space is invariant across generations, so
        // key-existence alone can't catch this — only the value differs.
        var generated = new Dictionary<string, string>
        {
            ["Pantry/0"] = "Spring Crops/reward/24 1 0/0/4//Spring Crops",
        };
        var live = new Dictionary<string, string>
        {
            ["Pantry/0"] = "Winter Crops/reward/412 1 0/0/4//Winter Crops", // same key, older gen's value
        };

        Assert.False(EngineManifestCheck.Matches(generated, live));
    }

    [Fact]
    public void Matches_ReturnsTrue_WhenLiveHasExtraKeysNotGenerated_ContentModBundlesDontBreakEngineMode()
    {
        var generated = new Dictionary<string, string>
        {
            ["Pantry/0"] = "Spring Crops/reward/24 1 0/0/4//Spring Crops",
        };
        var live = new Dictionary<string, string>
        {
            ["Pantry/0"] = "Spring Crops/reward/24 1 0/0/4//Spring Crops",
            ["Vault/23"] = "2,500g/reward/-1/0/1//2,500g", // e.g. a content-mod-added bundle we never generated
        };

        Assert.True(EngineManifestCheck.Matches(generated, live));
    }
}
