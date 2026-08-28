using System.Collections.Generic;
using TheLongestYear.Core;
using TheLongestYear.Core.Availability;
using Xunit;

namespace TheLongestYear.Tests;

public class ShopAvailabilityTests
{
    [Theory]
    [InlineData("(O)245", 1)]   // Sugar
    [InlineData("(O)216", 1)]   // Bread, Saloon
    [InlineData("(H)27", 2)]    // Hard Hat
    [InlineData("(O)523", 5)]   // Savage Ring
    [InlineData("(O)PrizeTicket", 1)]
    public void Shop_and_reward_items_have_weeks(string id, int week)
        => Assert.Equal(week, ShopAvailability.Derive(id)!.EarliestWeek);

    [Fact]
    public void Savage_ring_gates_in_summer_like_the_deep_mine()
        => Assert.Equal(Season.Summer, ShopAvailability.Derive("(O)523")!.GateSeason);

    [Fact]
    public void Unclaimed_item_is_null() => Assert.Null(ShopAvailability.Derive("(O)24"));

    [Fact]
    public void Composer_takes_the_earliest_week_across_rules()
    {
        // Red Mushroom: a Mushroom Box machine rule (quest unlock, week 9) and a mine forage row (week 1).
        var data = new EffortData
        {
            MachineRules = new List<RawMachineRule> { new("(BC)128", null, new string[0], new[] { "(O)420" }, 1440, -1) },
            MachineUnlocks = new Dictionary<string, string> { ["(BC)128"] = "null" },
            ForageSpawns = new List<RawSpawnEntry> { new("(O)420", null, null, "UndergroundMine20") },
        };
        var composer = new EffortComposer(data, new Dictionary<string, ItemAvailability>(), hasKitchen: false);
        Assert.Equal(1, composer.Derive("(O)420")!.EarliestWeek);
    }

    [Fact]
    public void Builder_places_every_trap_fish_in_the_data()
    {
        var pools = new ItemPools { TrapFishIds = new HashSet<string> { "(O)715", "(O)716" } };
        var model = ItemAvailabilityBuilder.Build(pools);
        Assert.True(model.IsPlaced("(O)715"));
        Assert.Equal(AvailabilityWeeks.TrapFishWeek, model.For("(O)716").Week);
    }
}
