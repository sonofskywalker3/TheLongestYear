using System.Linq;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

[Collection("i18n")]
public class UpgradeCatalogI18nTests
{
    public UpgradeCatalogI18nTests(I18nFixture fixture) => _fixture = fixture;
    private readonly I18nFixture _fixture;

    [Fact]
    public void EveryCatalogRow_ResolvesNameAndDescription()
    {
        foreach (var def in UpgradeCatalog.All)
        {
            Assert.False(def.DisplayName.StartsWith("upgrade."),
                $"{def.Id}: DisplayName did not resolve — missing key '{def.DisplayName}' in default.json");
            Assert.False(def.Description.StartsWith("upgrade."),
                $"{def.Id}: Description did not resolve — missing key '{def.Description}' in default.json");
            Assert.DoesNotContain("{{", def.DisplayName);
            Assert.DoesNotContain("{{", def.Description);
        }
    }

    [Fact]
    public void KnownRow_KeepsByteIdenticalEnglish()
    {
        var def = UpgradeCatalog.TryGet("backpack_1")!;
        Assert.Equal("Backpack I", def.DisplayName);
        Assert.Equal("Start each loop with the 24-slot backpack.", def.Description);
    }

    [Fact]
    public void GeneratedRows_KeepByteIdenticalEnglish()
    {
        Assert.Equal("Keep Copper Hoe", UpgradeCatalog.TryGet("keep_hoe_1")!.DisplayName);
        Assert.Equal("Start each loop with your Hoe at the Copper tier.", UpgradeCatalog.TryGet("keep_hoe_1")!.Description);
        Assert.Equal("Keep Farming Level 5", UpgradeCatalog.TryGet("keep_farming_level_5")!.DisplayName);
        Assert.EndsWith("Re-triggers the profession picker for Level 5.", UpgradeCatalog.TryGet("keep_farming_level_5")!.Description);
        Assert.Equal("Keep Mine Elevator Floor 120", UpgradeCatalog.TryGet("keep_mine_elevator_120")!.DisplayName);
    }
}
