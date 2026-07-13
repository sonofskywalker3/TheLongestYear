// tests/TheLongestYear.Tests/ThemeDisplayTests.cs
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

[Collection("i18n")]
public class ThemeDisplayTests
{
    public ThemeDisplayTests(I18nFixture _) { }

    [Fact]
    public void EveryTheme_HasDisplayName()
    {
        foreach (Theme t in System.Enum.GetValues<Theme>())
            Assert.False(ThemeDisplay.Name(t).StartsWith("theme."), $"missing theme key for {t}");
    }

    [Fact]
    public void EveryCategory_HasDisplayName()
    {
        foreach (UpgradeCategory c in System.Enum.GetValues<UpgradeCategory>())
            Assert.False(ThemeDisplay.CategoryName(c).StartsWith("upgrade-category."), $"missing category key for {c}");
    }

    [Fact]
    public void EveryModifierId_HasDisplayName()
    {
        foreach (Theme t in System.Enum.GetValues<Theme>())
        {
            var (bonus, liability) = ThemeModifiers.For(t);
            Assert.False(ThemeModifiers.DisplayNameFor(bonus).StartsWith("modifier."), bonus);
            Assert.False(ThemeModifiers.DisplayNameFor(liability).StartsWith("modifier."), liability);
        }
    }

    [Fact]
    public void KnownStrings_ByteIdentical()
    {
        Assert.Equal("Farming", ThemeDisplay.Name(Theme.Farming));
        Assert.Equal("Loadout", ThemeDisplay.CategoryName(UpgradeCategory.Loadout));
        Assert.Equal("Mine entrance closed all week", ThemeModifiers.DisplayNameFor("mines_closed"));
    }
}
