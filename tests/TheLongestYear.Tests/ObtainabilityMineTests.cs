using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;
using TheLongestYear.Core.Obtainability;

namespace TheLongestYear.Tests;

public class ObtainabilityMineTests
{
    [Fact]
    public void Ores_are_dependable_from_their_floors_and_iridium_needs_skull_cavern()
    {
        var nodes = MineSources.Nodes().ToList();
        var copper = nodes.Single(n => n.ItemId == "(O)378").Source;
        Assert.Equal(Reliability.Dependable, copper.Reliability);
        Assert.Equal(DayTable.Always, copper.Lands);
        Assert.Contains("mines:floor 1", copper.Conditions.Requires);
        Assert.Contains("location:SkullCave", nodes.Single(n => n.ItemId == "(O)386").Source.Conditions.Requires);
    }

    [Fact]
    public void Gems_and_geodes_from_stones_are_chance()
    {
        var nodes = MineSources.Nodes().ToList();
        Assert.Equal(Reliability.Chance, nodes.Single(n => n.ItemId == "(O)72").Source.Reliability);  // diamond
        Assert.Equal(Reliability.Chance, nodes.Single(n => n.ItemId == "(O)536").Source.Reliability); // frozen geode
    }

    [Fact]
    public void Monster_drops_carry_the_monster_floor()
    {
        var rows = new[]
        {
            new MonsterDropRow("Dust Spirit", "(O)382", 0.5),
            new MonsterDropRow("Some Modded Beast", "(O)766", 0.1),
        };
        var list = MineSources.MonsterDrops(rows, new Dictionary<string, ObjInfo>()).ToList();
        Assert.All(list, x => Assert.Equal(Reliability.Chance, x.Source.Reliability));
        Assert.Contains("mines:floor 40", list[0].Source.Conditions.Requires);
        Assert.Contains("monster:Some Modded Beast", list[1].Source.Conditions.Requires);
        Assert.Equal("floor 1", MineSources.MonsterFloor("Green Slime"));
        Assert.Null(MineSources.MonsterFloor("Some Modded Beast"));
    }

    [Fact]
    public void Treasure_is_complete_chance_and_gated_where_the_game_gates_it()
    {
        var all = MineSources.FishingTreasure().ToList();
        var treasure = all.Where(t => !t.ItemId.StartsWith(ItemQueries.UnresolvedPrefix)).ToList();
        Assert.All(treasure, t => Assert.Equal(SourceKind.FishingTreasure, t.Source.Kind));
        Assert.Contains(treasure, t => t.ItemId == "(O)Book_Roe");
        Assert.Contains(treasure, t => t.ItemId == "(O)TroutDerbyTag");
        Assert.Contains(treasure, t => t.ItemId == "(O)812" && t.Source.Conditions.Requires.Contains("book:Book_Roe"));
        Assert.All(treasure, t => Assert.Equal(Reliability.Chance, t.Source.Reliability));
        Assert.Equal(DayTable.InWeeks(WeekMask.ForSeason(Season.Spring)), treasure.Single(t => t.ItemId == "(O)273").Source.Lands);
        Assert.Contains(treasure, t => t.ItemId == "(O)774" && t.Source.Conditions.Requires.Contains("recipe:Wild Bait"));
        Assert.True(treasure.Single(t => t.ItemId == "(O)890").Source.Conditions.GingerIsland);   // Qi beans need Qi's orders
        Assert.Contains(treasure, t => t.ItemId == "(W)14");
        Assert.Contains(treasure, t => t.ItemId == "(B)504");
        Assert.Contains(treasure, t => t.ItemId == "(O)SkillBook_0");
        Assert.Contains(treasure, t => t.ItemId == "(O)119");
        Assert.Contains(treasure, t => t.ItemId == "(O)StardropTea" && t.Source.Conditions.Requires.Contains("fishing:golden treasure chest"));
    }

    [Fact]
    public void Fishing_treasure_no_longer_reports_the_raccoon_seed_as_unresolved()
        => Assert.DoesNotContain(MineSources.FishingTreasure(), s => s.ItemId.StartsWith(ItemQueries.UnresolvedPrefix));
}
