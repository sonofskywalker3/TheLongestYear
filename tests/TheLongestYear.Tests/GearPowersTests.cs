using System.Linq;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class GearPowersTests
{
    private static UpgradeDefinition Row(string id) => UpgradeCatalog.TryGet(id)!;

    [Fact]
    public void Rows_have_the_spec_prices()
    {
        Assert.Equal(250, Row("gear_swift_1").Cost);
        Assert.Equal(750, Row("gear_swift_2").Cost);
        Assert.Equal(1500, Row("gear_swift_3").Cost);
        Assert.Equal(250, Row("gear_light_1").Cost);
        Assert.Equal(1000, Row("gear_light_2").Cost);
        var rows = GearPowers.Rows().ToList();
        Assert.Equal(22, rows.Count);
        Assert.Equal(16250, rows.Sum(r => r.Cost));
        Assert.All(rows, r => Assert.Equal(UpgradeCategory.Gear, r.Category));
    }

    [Fact]
    public void Each_tier_requires_the_one_before()
    {
        Assert.Equal("gear_edge_1", Row("gear_edge_2").PrerequisiteId);
        Assert.Null(Row("gear_edge_1").PrerequisiteId);
    }

    [Fact]
    public void Swing_never_beats_a_real_swift()
    {
        Assert.Equal(0.66f, GearPowers.BetterSwing(0.66f, 3));
        Assert.Equal(0.70f, GearPowers.BetterSwing(1f, 3));
        Assert.Equal(1f, GearPowers.SwingFactor(0));
    }

    [Fact]
    public void Refund_is_a_share_of_what_was_spent()
    {
        Assert.Equal(2f, GearPowers.RefundFor(100, 96, 2));
        Assert.Equal(0f, GearPowers.RefundFor(100, 100, 2));
        Assert.Equal(0f, GearPowers.RefundFor(96, 100, 2));
    }

    [Fact]
    public void Power_is_the_better_of_upgrade_and_powerful()
    {
        Assert.Equal(2, GearPowers.ExtraPower(2, false, 0));
        Assert.Equal(4, GearPowers.ExtraPower(2, true, 0));
        Assert.Equal(0, GearPowers.ExtraPower(1, false, 1));
        Assert.Equal(2, GearPowers.ExtraPower(2, true, 1));
    }

    [Fact]
    public void Charge_caps_at_an_enchanted_iridium_tool()
    {
        Assert.Equal(3, GearPowers.MaxChargePower(1, false, 2));
        Assert.Equal(5, GearPowers.MaxChargePower(4, false, 2));
        Assert.Equal(5, GearPowers.MaxChargePower(4, true, 1));
        Assert.Equal(2, GearPowers.MaxChargePower(1, true, 0));
    }

    [Fact]
    public void Weapon_bonuses_never_stack_with_gems()
    {
        Assert.Equal(0, GearPowers.ExtraDamage(3, 3, 40));
        Assert.Equal(8, GearPowers.ExtraDamage(3, 1, 40));
        Assert.Equal(0, GearPowers.ExtraDefense(2, 3));
    }

    [Fact]
    public void Keen_edge_raises_a_low_damage_weapon_at_every_tier()
    {
        // Rusty Sword (base 2-5): Ruby's rule adds at least 1 per level to each end, so tier 1 is 3-6 and tier 3 is 5-8.
        Assert.Equal(1, GearPowers.ExtraDamage(1, 0, 2));
        Assert.Equal(1, GearPowers.ExtraDamage(1, 0, 4));
        Assert.Equal(3, GearPowers.ExtraDamage(3, 0, 2));
        Assert.Equal(3, GearPowers.ExtraDamage(3, 0, 4));
        Assert.Equal(0, GearPowers.ExtraDamage(0, 0, 4));
        Assert.Equal(4, GearPowers.ExtraDamage(1, 0, 40));
    }

    [Fact]
    public void Scythe_and_odd_tools_get_nothing()
    {
        Assert.False(GearPowers.Fits(GearPowers.Edge, ToolKind.Scythe));
        Assert.False(GearPowers.Fits(GearPowers.Swift, ToolKind.WateringCan));
        Assert.False(GearPowers.Fits(GearPowers.Light, ToolKind.Other));
        Assert.True(GearPowers.Fits(GearPowers.Light, ToolKind.FishingRod));
    }
}
