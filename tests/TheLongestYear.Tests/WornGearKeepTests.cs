using System.Linq;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

[Collection("i18n")]
public class WornGearKeepTests
{
    [Fact]
    public void Catalog_row_is_one_level_1000_jp_in_loadout()
    {
        UpgradeDefinition row = UpgradeCatalog.All.Single(u => u.Id == WornGearKeep.UpgradeId);
        Assert.Equal(1000L, row.Cost);
        Assert.Equal(UpgradeCategory.Loadout, row.Category);
        Assert.Null(row.PrerequisiteId);
        Assert.Null(row.RunReachRequirement);
        Assert.Equal(1, UpgradeCatalog.All.Count(u => u.Id.StartsWith("keep_worn_gear")));
    }

    [Fact]
    public void Baseline_keeps_worn_gear_only_when_owned()
    {
        var owned = new MetaState { OwnedUpgrades = { WornGearKeep.UpgradeId } };
        Assert.True(RunBaselineBuilder.Build(owned, new RunState(), PlayerSnapshot.Empty, 500).KeepWornGear);
        Assert.False(RunBaselineBuilder.Build(new MetaState(), new RunState(), PlayerSnapshot.Empty, 500).KeepWornGear);
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    public void Mod_slot_item_is_kept_only_when_worn_gear_is_kept_and_it_is_not_a_weapon(bool keepWornGear, bool isTool, bool kept)
        => Assert.Equal(kept, WornGearKeep.KeepsModSlotItem(keepWornGear, isTool));
}
