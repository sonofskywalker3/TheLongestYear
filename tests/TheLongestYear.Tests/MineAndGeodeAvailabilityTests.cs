using System.Collections.Generic;
using TheLongestYear.Core;
using TheLongestYear.Core.Availability;
using Xunit;

namespace TheLongestYear.Tests;

public class MineralNodeAvailabilityTests
{
    [Theory]
    [InlineData("(O)80", 1)]   // Quartz, any floor
    [InlineData("(O)66", 1)]   // Amethyst node, floors 1 to 39
    [InlineData("(O)70", 3)]   // Jade node, floors 41 to 79
    [InlineData("(O)64", 5)]   // Ruby node, floors 81 to 119 (spec: a gem node at area 80 scores 5)
    [InlineData("(O)72", 5)]   // Diamond, floor 80 node
    [InlineData("(O)74", 7)]   // Prismatic Shard, Skull Cavern iridium nodes and mystic stones
    public void Node_gems_and_minerals_score_their_shallowest_area(string id, int expected)
    {
        ItemEffort? result = MineralNodeAvailability.Derive(id);
        Assert.NotNull(result);
        Assert.Equal(expected, result!.Effort);
    }

    [Fact]
    public void An_id_the_node_table_does_not_know_yields_null()
        => Assert.Null(MineralNodeAvailability.Derive("(O)24"));

    [Theory]
    [InlineData(0, 1)] [InlineData(10, 1)] [InlineData(40, 3)] [InlineData(80, 5)] [InlineData(121, 7)]
    public void Area_effort_matches_the_metals_scale(int area, int effort)
        => Assert.Equal(effort, MineAreas.Effort(area));
}

public class GeodeAvailabilityTests
{
    [Fact]
    public void A_rare_drop_from_a_frozen_geode_scores_geode_plus_two()
    {
        var drops = new List<RawGeodeDrop> { new("(O)536", "(O)541", 1.0 / 32) };
        ItemEffort? result = GeodeAvailability.Derive("(O)541", drops);
        Assert.NotNull(result);
        Assert.Equal(3 + 2, result!.Effort);
    }

    [Fact]
    public void The_easiest_geode_wins()
    {
        var drops = new List<RawGeodeDrop> { new("(O)537", "(O)541", 0.5), new("(O)535", "(O)541", 0.5) };
        Assert.Equal(1, GeodeAvailability.Derive("(O)541", drops)!.Effort);
    }

    [Theory]
    [InlineData(0.5, 0)] [InlineData(0.125, 0)] [InlineData(0.1, 1)] [InlineData(0.05, 1)] [InlineData(0.01, 2)]
    public void Chance_steps(double chance, int step) => Assert.Equal(step, GeodeAvailability.ChanceStep(chance));

    [Fact]
    public void Unknown_geodes_and_unknown_items_yield_null()
    {
        Assert.Null(GeodeAvailability.Derive("(O)541", new List<RawGeodeDrop> { new("(O)275", "(O)541", 1) }));
        Assert.Null(GeodeAvailability.Derive("(O)24", new List<RawGeodeDrop>()));
    }

    [Fact]
    public void Default_table_covers_the_code_only_ore_and_stone_rows()
    {
        var rows = GeodeAvailability.DefaultTableDrops("(O)535");
        Assert.Contains(rows, r => r.ItemId == "(O)390");   // Stone
        Assert.Contains(rows, r => r.ItemId == "(O)378");   // Copper Ore
        Assert.Contains(rows, r => r.ItemId == "(O)86");    // Earth Crystal
        Assert.Empty(GeodeAvailability.DefaultTableDrops("(O)275"));
    }
}

public class MonsterDropAvailabilityTests
{
    [Fact]
    public void Bat_wing_from_a_shallow_bat_at_ninety_percent_scores_one()
    {
        var drops = new List<RawMonsterDrop> { new("Bat", "(O)767", 0.9) };
        Assert.Equal(1, MonsterDropAvailability.Derive("(O)767", drops)!.Effort);
    }

    [Fact]
    public void Minimum_over_every_monster_that_drops_it()
    {
        var drops = new List<RawMonsterDrop> { new("Serpent", "(O)769", 0.9), new("Dust Spirit", "(O)769", 0.05) };
        // Serpent: Skull Cavern 7 + 0; Dust Spirit: area 40 3 + 2 = 5.
        Assert.Equal(5, MonsterDropAvailability.Derive("(O)769", drops)!.Effort);
    }

    [Fact]
    public void Unknown_monsters_are_skipped_and_an_unclaimed_item_is_null()
    {
        var drops = new List<RawMonsterDrop> { new("SVE Wyvern", "(O)769", 1.0) };
        Assert.Null(MonsterDropAvailability.Derive("(O)769", drops));
    }

    [Theory]
    [InlineData(0.9, 0)] [InlineData(0.5, 0)] [InlineData(0.25, 1)] [InlineData(0.1, 1)] [InlineData(0.05, 2)]
    public void Chance_steps(double chance, int step) => Assert.Equal(step, MonsterDropAvailability.ChanceStep(chance));
}
