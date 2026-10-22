using System;
using System.Collections.Generic;
using TheLongestYear.Core.Obtainability;
using TheLongestYear.Core.Sabotage;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>Jeff's ruling 2026-09-16, "Mine depth in the landing week": a route that needs a mine floor
/// lands after the days it takes to get there from nothing, at 10 floors a day.</summary>
public class ObtainabilityMineDepthTests
{
    private const string GoldOre = "(O)384";      // mines:floor 80
    private const string CopperOre = "(O)378";    // mines:floor 1
    private const string GoldBar = "(O)336";
    private static readonly ObtainFilter Nodes = ObtainFilter.DependableOnly with { Kinds = new[] { SourceKind.MineNode } };

    [Theory]
    [InlineData(1, 0)]
    [InlineData(10, 0)]
    [InlineData(11, 1)]
    [InlineData(40, 3)]
    [InlineData(80, 7)]
    [InlineData(120, 11)]
    [InlineData(121, 12)]
    public void Days_to_reach_a_floor_from_nothing(int floor, int days)
        => Assert.Equal(days, MineDepth.DaysToReach(floor));

    [Fact]
    public void The_fairness_rule_uses_the_same_pace()
        => Assert.Equal(SabotageTuning.MineFloorsPerDay, MineDepth.FloorsPerDay);

    [Fact]
    public void The_floor_is_the_deepest_mine_floor_named()
    {
        Assert.Equal(80, MineDepth.FloorOf(ObtainConditions.None with { Requires = new[] { "mines:floor 40", "mines:floor 80", "shop:Sandy" } }));
        // Task 11 (2026-09-16, "Skull Cavern needs the mines cleared"): a location:SkullCave requirement
        // now prices as floor 121, one past the mine's bottom.
        Assert.Equal(121, MineDepth.FloorOf(ObtainConditions.None with { Requires = new[] { "location:SkullCave" } }));
        Assert.Equal(121, MineDepth.FloorOf(ObtainConditions.None with { Requires = new[] { "mines:floor 80", "location:SkullCave" } }));
        Assert.Null(MineDepth.FloorOf(ObtainConditions.None));
    }

    [Fact]
    public void A_floor_80_route_lands_after_the_days_it_takes_to_get_there()
    {
        ObtainabilityModel model = ObtainabilityBuilder.Build(new ObtainabilityInputs()).Model;
        Assert.Equal(8, model.Lands(GoldOre, 1, Nodes));
        Assert.Equal(12, model.Lands(GoldOre, 5, Nodes));
        // From Winter 22 nobody reaches floor 80 by the end of the year.
        Assert.Null(model.Lands(GoldOre, 106, Nodes));
        // The table from before the delay is kept for a consumer that knows the real depth.
        ObtainSource gold = Assert.Single(model.Sources(GoldOre), s => s.Kind == SourceKind.MineNode);
        Assert.Equal(106, gold.UndelayedLands!.Lands(106));
        Assert.Null(Assert.Single(model.Sources(CopperOre), s => s.Kind == SourceKind.MineNode).UndelayedLands);
    }

    [Fact]
    public void A_floor_1_route_still_lands_the_day_it_starts()
    {
        ObtainabilityModel model = ObtainabilityBuilder.Build(new ObtainabilityInputs()).Model;
        Assert.Equal(1, model.Lands(CopperOre, 1, Nodes));
        Assert.Equal(40, model.Lands(CopperOre, 40, Nodes));
    }

    [Fact]
    public void A_skull_cavern_route_lands_on_day_13_and_keeps_its_undelayed_table()
    {
        ObtainSource source = MineDepth.WithTravel(new ObtainSource(
            SourceKind.MineNode, DayTable.Always, Reliability.Dependable,
            ObtainConditions.None with { Requires = new[] { "location:SkullCave" } }, "test"));
        Assert.Equal(13, source.Lands.Lands(1));
        Assert.Equal(1, source.UndelayedLands!.Lands(1));
    }

    [Fact]
    public void A_route_made_from_a_mine_item_inherits_the_delay_once()
    {
        var inputs = new ObtainabilityInputs
        {
            Objects = new Dictionary<string, ObjInfo>
            {
                [GoldOre] = new ObjInfo(GoldOre, "Gold Ore", -15, 25, new[] { "id_o_384" }, false),
            },
            Machines = new[]
            {
                new MachineRow("(BC)13", null, new[] { "id_o_384" }, null, new[] { new MachineOutput(GoldBar, null, null) }, 0, 3),
            },
        };
        ObtainabilityModel model = ObtainabilityBuilder.Build(inputs).Model;
        // Gold ore from day 8, then the machine's three days: day 11, not day 18.
        Assert.Equal(11, model.Lands(GoldBar, 1, ObtainFilter.DependableOnly with { Kinds = new[] { SourceKind.Machine } }));
    }
}
