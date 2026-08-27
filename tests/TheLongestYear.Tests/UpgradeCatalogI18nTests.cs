using System.Collections.Generic;
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
    public void Item_token_resolves_through_item_name_provider()
    {
        Strings.InitItemNames(id => id == "(O)Book_Speed" ? "Way Of The Wind pt. 1" : id);
        try
        {
            var def = new UpgradeDefinition("t_item", UpgradeCategory.Carryover,
                "upgrade-tpl.keep-book.name", "upgrade-tpl.keep-book.desc",
                new Dictionary<string, string> { ["book"] = "item:(O)Book_Speed" }, 10);
            Assert.Equal("Keep Way Of The Wind pt. 1", def.DisplayName);
            Assert.Equal("Start each loop with Way Of The Wind pt. 1 already read. Its power stays with you.", def.Description);
        }
        finally { Strings.ResetItemNames(); }
    }

    [Fact]
    public void Item_token_falls_back_to_the_id_without_a_provider()
    {
        Strings.ResetItemNames();
        var def = new UpgradeDefinition("t_item2", UpgradeCategory.Carryover,
            "upgrade-tpl.keep-book.name", "upgrade-tpl.keep-book.desc",
            new Dictionary<string, string> { ["book"] = "item:(O)Book_Speed" }, 10);
        Assert.Equal("Keep (O)Book_Speed", def.DisplayName);
    }

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
