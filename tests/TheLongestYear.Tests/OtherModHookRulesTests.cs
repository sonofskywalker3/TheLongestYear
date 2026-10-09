using TheLongestYear.Core;

namespace TheLongestYear.Tests;

public class OtherModHookRulesTests
{
    [Theory]
    [InlineData("ExtraAnimalConfig")]
    [InlineData("BetterPigs")]
    [InlineData("SpaceCore")]
    public void Other_mods_are_followed(string assembly)
        => Assert.True(OtherModHookRules.IsOtherModAssembly(assembly));

    [Theory]
    [InlineData("Stardew Valley")]
    [InlineData("StardewValley.GameData")]
    [InlineData("StardewModdingAPI")]
    [InlineData("SMAPI.Toolkit")]
    [InlineData("MonoGame.Framework")]
    [InlineData("0Harmony")]
    [InlineData("System")]
    [InlineData("System.Private.CoreLib")]
    [InlineData("Microsoft.Xna.Framework")]
    [InlineData("netstandard")]
    [InlineData("TheLongestYear")]
    [InlineData("TheLongestYear.Core")]
    [InlineData("xTile")]
    public void Game_framework_and_TLY_are_not(string assembly)
        => Assert.False(OtherModHookRules.IsOtherModAssembly(assembly));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Missing_names_are_not(string? assembly)
        => Assert.False(OtherModHookRules.IsOtherModAssembly(assembly));

    [Fact]
    public void A_mod_whose_name_starts_with_System_but_no_dot_is_followed()
        => Assert.True(OtherModHookRules.IsOtherModAssembly("SystemShockAnimals"));
}
