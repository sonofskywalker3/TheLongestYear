using System.Collections.Generic;
using TheLongestYear.Core;
using TheLongestYear.Core.Availability;
using Xunit;

namespace TheLongestYear.Tests;

public class MineRuleWeekTests
{
    [Theory]
    [InlineData("(O)80", 1, Season.Spring)]  // Quartz
    [InlineData("(O)84", 2, Season.Spring)]  // Frozen Tear
    [InlineData("(O)64", 3, Season.Summer)]  // Ruby
    [InlineData("(O)74", 9, Season.Fall)]    // Prismatic Shard
    public void Node_week_and_gate(string id, int week, Season gate)
    {
        ItemEffort e = MineralNodeAvailability.Derive(id)!;
        Assert.Equal(week, e.EarliestWeek);
        Assert.Equal(gate, e.GateSeason);
    }

    [Fact]
    public void Geode_mineral_takes_the_shallowest_geode_that_drops_it()
    {
        var drops = new List<RawGeodeDrop>
        {
            new("(O)537", "(O)541", 0.1),   // Magma, week 3
            new("(O)535", "(O)541", 0.05),  // Geode, week 1
        };
        ItemEffort e = GeodeAvailability.Derive("(O)541", drops)!;
        Assert.Equal(1, e.EarliestWeek);
        Assert.Equal(Season.Spring, e.GateSeason);
    }

    [Fact]
    public void Monster_drop_takes_the_shallowest_monster()
    {
        var drops = new List<RawMonsterDrop>
        {
            new("Serpent", "(O)766", 0.9),
            new("Green Slime", "(O)766", 0.9),
        };
        ItemEffort e = MonsterDropAvailability.Derive("(O)766", drops)!;
        Assert.Equal(1, e.EarliestWeek);
    }

    [Fact]
    public void Skull_cavern_only_drop_is_fall()
    {
        var drops = new List<RawMonsterDrop> { new("Pepper Rex", "(O)107", 0.1) };
        ItemEffort e = MonsterDropAvailability.Derive("(O)107", drops)!;
        Assert.Equal(9, e.EarliestWeek);
        Assert.Equal(Season.Fall, e.GateSeason);
    }
}
