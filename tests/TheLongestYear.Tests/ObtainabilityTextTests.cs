using System.Collections.Generic;
using TheLongestYear.Core;
using TheLongestYear.Core.Obtainability;

namespace TheLongestYear.Tests;

public class ObtainabilityTextTests
{
    [Fact]
    public void Describe_lists_every_source_with_its_weeks_and_conditions()
    {
        var model = new ObtainabilityModel(new Dictionary<string, IReadOnlyList<ObtainSource>>
        {
            ["(O)775"] = new[]
            {
                new ObtainSource(SourceKind.Fish, WeekMask.ForSeason(Season.Winter), Reliability.Dependable,
                    ObtainConditions.None with { Skill = "Fishing", SkillLevel = 6, CatchLimit = 1, RainOnly = true,
                                                 Unresolved = true, Requires = new[] { "location:Forest" } }, "Fish at Forest"),
            },
        });
        string text = ObtainabilityText.Describe("775", model, id => "Glacierfish");
        Assert.Contains("(O)775 Glacierfish: 1 source(s)", text);
        Assert.Contains("dependable weeks 13-16", text);
        Assert.Contains("Fish, Dependable, weeks 13-16", text);
        Assert.Contains("Fishing 6", text);
        Assert.Contains("catch limit 1", text);
        Assert.Contains("rain only", text);
        Assert.Contains("UNRESOLVED", text);
        Assert.Contains("location:Forest", text);
        Assert.Contains("no source", ObtainabilityText.Describe("(O)1", model, id => null));
    }
}
