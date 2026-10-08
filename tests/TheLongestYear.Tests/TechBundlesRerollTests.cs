using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>Remixed rerolls a fresh Tech's Cross-Mod Bundles board each loop
/// (spec 2026-10-08-custom-board-vanilla-only, addendum 2).</summary>
public class TechBundlesRerollTests
{
    private sealed class FakeTech : ITechBundlesRerollTarget
    {
        public bool IsLoaded { get; set; } = true;
        public System.Exception? Throws { get; set; }
        public int Rerolls { get; private set; }

        public void Reroll()
        {
            Rerolls++;
            if (Throws != null)
                throw Throws;
        }
    }

    private readonly List<string> _info = new();
    private readonly List<string> _warn = new();

    private TechRerollOutcome Run(FakeTech? tech, string source, bool held = false)
        => TechBundlesReroll.Run(tech, source, held, _info.Add, _warn.Add);

    [Fact]
    public void Remixed_with_tech_loaded_rerolls_and_logs_info()
    {
        var tech = new FakeTech();
        Assert.Equal(TechRerollOutcome.Rerolled, Run(tech, BundleSourceNames.Remixed));
        Assert.Equal(1, tech.Rerolls);
        Assert.Equal(new[] { TechBundlesReroll.RerolledInfo }, _info);
        Assert.Empty(_warn);
    }

    [Theory]
    [InlineData(BundleSourceNames.Normal)]
    [InlineData(BundleSourceNames.Engine)]
    [InlineData(BundleSourceNames.LegacyVanilla)]
    public void Other_sources_never_reroll(string source)
    {
        var tech = new FakeTech();
        Assert.Equal(TechRerollOutcome.Skipped, Run(tech, source));
        Assert.Equal(0, tech.Rerolls);
        Assert.Empty(_info);
        Assert.Empty(_warn);
    }

    [Fact]
    public void Kept_board_is_never_rerolled()
    {
        var tech = new FakeTech();
        Assert.Equal(TechRerollOutcome.Skipped, Run(tech, BundleSourceNames.Remixed, held: true));
        Assert.Equal(0, tech.Rerolls);
    }

    [Fact]
    public void Tech_not_loaded_skips()
    {
        var tech = new FakeTech { IsLoaded = false };
        Assert.Equal(TechRerollOutcome.Skipped, Run(tech, BundleSourceNames.Remixed));
        Assert.Equal(0, tech.Rerolls);
        Assert.Empty(_warn);
    }

    [Fact]
    public void No_target_skips()
        => Assert.Equal(TechRerollOutcome.Skipped, Run(null, BundleSourceNames.Remixed));

    [Fact]
    public void Missing_method_fails_soft_with_one_warning()
    {
        var tech = new FakeTech { Throws = new System.MissingMethodException("TechsCrossModBundles.ModEntry", "GenerateBundles") };
        Assert.Equal(TechRerollOutcome.Failed, Run(tech, BundleSourceNames.Remixed));
        Assert.Single(_warn);
        Assert.StartsWith(TechBundlesReroll.FailureWarning, _warn[0]);
        Assert.Empty(_info);
    }

    [Fact]
    public void Exception_inside_the_mod_fails_soft_and_names_the_inner_error()
    {
        var inner = new System.NullReferenceException("boom");
        var tech = new FakeTech { Throws = new System.Reflection.TargetInvocationException(inner) };
        Assert.Equal(TechRerollOutcome.Failed, Run(tech, BundleSourceNames.Remixed));
        Assert.Single(_warn);
        Assert.Contains("NullReferenceException", _warn[0]);
    }

    [Fact]
    public void Source_match_ignores_case()
        => Assert.True(TechBundlesReroll.ShouldReroll("remixed", restoringHeldBoard: false, techLoaded: true));
}
