using TheLongestYear.Core.Obtainability;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>Pins today's vanilla answers: any change to how the model is built that moves a single
/// landing day, tag, setup step, input group or source shows up here.</summary>
public class ObtainabilityGoldenTests
{
    internal const string VanillaHash = "0A61D39CEB66A2D54E5130AC0411434CF453A330F3F2022DFF1ACB5FD6CC9FB6";
    private const int VanillaPasses = 7;
    private const int VanillaUnresolved = 45;

    [Fact]
    public void ColdBuildMatchesGolden()
    {
        ObtainabilityBuild build = ObtainabilityBuilder.Build(VanillaInputsFixture.Load());

        Assert.False(build.HitPassCap);
        Assert.Equal(VanillaPasses, build.Passes);
        Assert.Equal(VanillaUnresolved, build.Unresolved.Count);
        Assert.Equal(VanillaHash, ObtainabilityGolden.Hash(build.Model));
    }
}
