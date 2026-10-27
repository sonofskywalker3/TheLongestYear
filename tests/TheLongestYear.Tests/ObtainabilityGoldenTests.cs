using TheLongestYear.Core.Obtainability;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>Pins today's vanilla answers: any change to how the model is built that moves a single
/// landing day, tag or source shows up here.</summary>
public class ObtainabilityGoldenTests
{
    private const string VanillaHash = "9EA0517DDC71AC23BC04686023A167C291021D777238ECEE17FCC3D8AF45C793";
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
