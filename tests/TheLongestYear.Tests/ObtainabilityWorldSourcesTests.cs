using System.Linq;
using TheLongestYear.Core.Obtainability;
using Xunit;

namespace TheLongestYear.Tests;

public class ObtainabilityWorldSourcesTests
{
    private static ObtainSource One(string id, SourceKind kind)
        => WorldSources.All().Where(s => s.ItemId == id && s.Source.Kind == kind).Select(s => s.Source).First();

    [Fact]
    public void Fiber_from_weeds_is_dependable_from_day_1()
    {
        ObtainSource fiber = One("(O)771", SourceKind.Forage);
        Assert.Equal(Reliability.Dependable, fiber.Reliability);
        Assert.Equal(1, fiber.Lands.Lands(1));
    }

    [Fact]
    public void Salmonberries_land_on_spring_15_and_blackberries_on_fall_8()
    {
        Assert.Equal(15, One("(O)296", SourceKind.Forage).Lands.Lands(1));
        Assert.Null(One("(O)296", SourceKind.Forage).Lands.Lands(19));
        Assert.Equal(64, One("(O)410", SourceKind.Forage).Lands.Lands(1));   // Fall 8 = day 56 + 8
    }

    [Fact]
    public void Snow_yam_is_winter_tilling()
    {
        ObtainSource yam = One("(O)416", SourceKind.Forage);
        Assert.Equal(85, yam.Lands.Lands(1));   // Winter 1
        Assert.Contains("tilling:outdoors off the farm", yam.Conditions.Requires);
    }

    [Fact]
    public void Cave_carrot_comes_from_mine_tilling()
    {
        ObtainSource carrot = One("(O)78", SourceKind.MineNode);
        Assert.Equal(Reliability.Dependable, carrot.Reliability);
        Assert.Contains("mines:floor 1", carrot.Conditions.Requires);
    }

    [Fact]
    public void Dinosaur_egg_from_the_cavern_is_chance()
        => Assert.Equal(Reliability.Chance, One("(O)107", SourceKind.MineNode).Reliability);

    [Fact]
    public void Quartz_lands_from_day_1_in_the_mines()
        => Assert.Equal(1, One("(O)80", SourceKind.MineNode).Lands.Lands(1));

    [Fact]
    public void Coral_is_daily_past_the_bridge_and_anywhere_on_summer_12_to_14()
    {
        var coral = WorldSources.All().Where(s => s.ItemId == "(O)393").Select(s => s.Source).ToList();
        ObtainSource pools = coral.Single(s => s.Conditions.Requires.Contains("mail:beachBridgeFixed"));
        Assert.Equal(1, pools.Lands.Lands(1));
        ObtainSource summer = coral.Single(s => !s.Conditions.Requires.Contains("mail:beachBridgeFixed"));
        Assert.Equal(40, summer.Lands.Lands(1));   // Summer 12 = day 28 + 12
        Assert.All(coral, s => Assert.Equal(Reliability.Dependable, s.Reliability));
    }

    [Fact]
    public void Sea_urchin_follows_the_same_two_routes()
        => Assert.Equal(2, WorldSources.All().Count(s => s.ItemId == "(O)397"));

    [Fact]
    public void Mystery_boxes_are_dependable_from_day_51()
    {
        ObtainSource box = WorldSources.All().Single(s => s.ItemId == "(O)MysteryBox").Source;
        Assert.Equal(Reliability.Dependable, box.Reliability);
        Assert.Equal(51, box.Lands.Lands(1));
    }
}
