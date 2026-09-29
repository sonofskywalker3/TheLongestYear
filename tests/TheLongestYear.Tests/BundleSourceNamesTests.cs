using TheLongestYear.Core;

namespace TheLongestYear.Tests;

public class BundleSourceNamesTests
{
    [Fact]
    public void The_Menu_Offers_Exactly_Three_Choices()
        => Assert.Equal(
            new[] { BundleSourceNames.Engine, BundleSourceNames.Normal, BundleSourceNames.Remixed },
            BundleSourceNames.All);

    /// <summary>The legacy value is never offered: it cannot say which layout the save is on.</summary>
    [Fact]
    public void The_Legacy_Value_Is_Not_In_The_Menu()
        => Assert.DoesNotContain(BundleSourceNames.LegacyVanilla, BundleSourceNames.All);

    [Theory]
    [InlineData("Normal", true)]
    [InlineData("Remixed", true)]
    [InlineData("Vanilla", true)]
    [InlineData("Engine", false)]
    [InlineData(null, false)]
    [InlineData("nonsense", false)]
    public void IsVanilla_Covers_Both_Layouts_And_The_Legacy_Value(string? source, bool expected)
        => Assert.Equal(expected, BundleSourceNames.IsVanilla(source));

    [Theory]
    [InlineData("Normal", "Default")]
    [InlineData("Remixed", "Remixed")]
    [InlineData("normal", "Default")]
    public void VanillaTypeFor_Names_The_Game_Bundle_Type(string source, string expected)
        => Assert.Equal(expected, BundleSourceNames.VanillaTypeFor(source));

    /// <summary>Null means "leave the save's existing layout alone", which is what the legacy
    /// value and the engine source both need.</summary>
    [Theory]
    [InlineData("Engine")]
    [InlineData("Vanilla")]
    [InlineData(null)]
    public void VanillaTypeFor_Is_Null_When_No_Layout_Is_Named(string? source)
        => Assert.Null(BundleSourceNames.VanillaTypeFor(source));

    /// <summary>Folding the legacy value into Normal would silently move a remixed save onto the
    /// standard board at its next reset. It has to survive normalization.</summary>
    [Fact]
    public void Normalize_Preserves_The_Legacy_Value()
        => Assert.Equal(BundleSourceNames.LegacyVanilla, BundleSourceNames.Normalize("vanilla"));

    [Theory]
    [InlineData("engine", "Engine")]
    [InlineData("REMIXED", "Remixed")]
    [InlineData("normal", "Normal")]
    [InlineData("nonsense", "Engine")]
    [InlineData(null, "Engine")]
    public void Normalize_Canonicalises_Everything_Else(string? input, string expected)
        => Assert.Equal(expected, BundleSourceNames.Normalize(input));

    [Theory]
    [InlineData("Remixed", "Remixed")]
    [InlineData("Default", "Normal")]
    [InlineData(null, "Normal")]
    public void ForVanillaType_Maps_A_Saves_Layout_Back_To_A_Menu_Choice(string? type, string expected)
        => Assert.Equal(expected, BundleSourceNames.ForVanillaType(type));

    /// <summary>Round trip: every offered vanilla choice maps to a game bundle type and back to
    /// itself, so the menu can always show what the save is actually on.</summary>
    [Theory]
    [InlineData("Normal")]
    [InlineData("Remixed")]
    public void A_Vanilla_Choice_Round_Trips_Through_The_Game_Bundle_Type(string source)
        => Assert.Equal(source, BundleSourceNames.ForVanillaType(BundleSourceNames.VanillaTypeFor(source)));

    /// <summary>A save's own choice wins. The config is shared by every save, so reading it at a
    /// reset let a newer TLY Custom game flip an older Normal save to custom bundles
    /// (victoriatauanem, Nexus 2026-09-28).</summary>
    [Theory]
    [InlineData("Normal", "Engine", null, "Normal")]
    [InlineData("Remixed", "Vanilla", "Default", "Remixed")]
    [InlineData("Engine", "Vanilla", "Default", "Engine")]
    [InlineData("normal", "Engine", null, "Normal")]
    public void ForSave_Uses_The_Saves_Chosen_Source(string chosen, string stamped, string? type, string expected)
        => Assert.Equal(expected, BundleSourceNames.ForSave(chosen, stamped, type));

    /// <summary>Saves from before the choice was kept per save carry on with the board they run
    /// under now, never whatever the shared config says.</summary>
    [Theory]
    [InlineData(null, "Vanilla", "Default", "Normal")]
    [InlineData(null, "Vanilla", "Remixed", "Remixed")]
    [InlineData(null, "Vanilla", null, "Normal")]
    [InlineData(null, "Engine", null, "Engine")]
    [InlineData(null, null, null, "Engine")]
    [InlineData("", "Vanilla", "Remixed", "Remixed")]
    [InlineData("Vanilla", "Vanilla", "Remixed", "Remixed")]
    public void ForSave_Falls_Back_To_The_Board_The_Save_Runs_Under(string? chosen, string? stamped, string? type, string expected)
        => Assert.Equal(expected, BundleSourceNames.ForSave(chosen, stamped, type));
}
