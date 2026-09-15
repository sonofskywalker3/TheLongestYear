using System.Collections.Generic;
using TheLongestYear.Core;
using TheLongestYear.Core.Obtainability;

namespace TheLongestYear.Tests;

public class ObtainabilityTextTests
{
    [Fact]
    public void Describe_prints_every_source_with_its_landing_weeks_from_the_start_day()
    {
        var model = new ObtainabilityModel(new Dictionary<string, IReadOnlyList<ObtainSource>>
        {
            ["(O)147"] = new[]
            {
                new ObtainSource(SourceKind.Fish, DayTable.InWeeks(WeekMask.ForSeason(Season.Winter)), Reliability.Dependable,
                    ObtainConditions.None with { Skill = "Fishing", SkillLevel = 3, RainOnly = true, Requires = new[] { "location:Beach" } }, "Fish at Beach")
                { Setup = new[] { new SetupStep("building:Fish Pond", 2) } },
                new ObtainSource(SourceKind.Cart, DayTable.Always, Reliability.Chance, ObtainConditions.None, "shop Traveler"),
            },
        });
        string text = ObtainabilityText.Describe("(O)147", model, _ => "Herring", startDay: 29);
        Assert.Contains("(O)147 Herring: 2 source(s); from day 29 dependable lands week 13, any lands week 5", text);
        Assert.Contains("Fish, Dependable, lands wk13/wk13/wk13/wk13, Fishing 3, rain only, needs location:Beach, setup building:Fish Pond 2d | Fish at Beach", text);
        Assert.Contains("Cart, Chance, lands wk1/wk5/wk9/wk13 | shop Traveler", text);
        Assert.Contains("no source", ObtainabilityText.Describe("(O)1", model, _ => null));
        Assert.DoesNotContain("\u2014", text);
    }
}
