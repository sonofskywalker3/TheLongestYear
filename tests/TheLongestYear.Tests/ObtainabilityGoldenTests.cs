using TheLongestYear.Core.Obtainability;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>Pins today's vanilla answers: any change to how the model is built that moves a single
/// landing day, tag, setup step, input group or source shows up here.</summary>
public class ObtainabilityGoldenTests
{
    internal const string VanillaHash = "F64E946410492FF56664BFE316BDEBAF6C6EFB80BAF1145A4FE4ED88F173FD5A";
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
