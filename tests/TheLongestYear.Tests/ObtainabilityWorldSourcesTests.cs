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
}
