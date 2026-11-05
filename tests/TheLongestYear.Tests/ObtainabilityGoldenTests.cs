using TheLongestYear.Core.Obtainability;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>Pins today's vanilla answers: any change to how the model is built that moves a single
/// landing day, tag, setup step, input group or source shows up here.</summary>
public class ObtainabilityGoldenTests
{
    internal const string VanillaHash = "505B15FF15277801230113B3916B652809D3137730AC5EA262BEA7DC363F808E";
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
