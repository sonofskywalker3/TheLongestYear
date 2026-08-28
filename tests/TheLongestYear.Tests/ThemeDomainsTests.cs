using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class ThemeDomainsTests
{
    [Theory]
    [InlineData(-2, "Minerals", ItemKind.Gem)] [InlineData(-12, "Minerals", ItemKind.Mineral)] [InlineData(-28, "Basic", ItemKind.MonsterLoot)]
    [InlineData(0, "Arch", ItemKind.Artifact)] [InlineData(-26, "Basic", ItemKind.ArtisanGood)] [InlineData(-7, "Cooking", ItemKind.Cooking)]
    [InlineData(-5, "Basic", ItemKind.Egg)] [InlineData(-6, "Basic", ItemKind.Milk)] [InlineData(-18, "Basic", ItemKind.AnimalProduct)] [InlineData(-75, "Basic", ItemKind.Other)]
    public void Classifier_reads_the_games_category_and_type(int category, string type, ItemKind kind)
        => Assert.Equal(kind, ItemKindClassifier.From(category, type));

    [Theory]
    [InlineData(Theme.Spelunking, ItemKind.Gem, true)] [InlineData(Theme.Spelunking, ItemKind.Mineral, true)]
    [InlineData(Theme.Spelunking, ItemKind.MonsterLoot, true)] [InlineData(Theme.Spelunking, ItemKind.Artifact, true)]
    [InlineData(Theme.Artisan, ItemKind.ArtisanGood, true)]
    [InlineData(Theme.Kitchen, ItemKind.Cooking, true)] [InlineData(Theme.Kitchen, ItemKind.Egg, true)]
    [InlineData(Theme.Kitchen, ItemKind.Milk, true)] [InlineData(Theme.Kitchen, ItemKind.AnimalProduct, true)]
    [InlineData(Theme.Spelunking, ItemKind.Other, false)] [InlineData(Theme.Artisan, ItemKind.Other, false)] [InlineData(Theme.Kitchen, ItemKind.Other, false)]
    [InlineData(Theme.Mixed, ItemKind.Other, true)] [InlineData(Theme.Mixed, ItemKind.Gem, true)]
    [InlineData(Theme.Farming, ItemKind.Other, false)]
    public void Themes_match_kinds(Theme theme, ItemKind kind, bool expected)
        => Assert.Equal(expected, ThemeDomains.Matches(theme, kind));

    [Fact]
    public void Room_and_activity_theme_lists()
    {
        Assert.Equal(new[] { Theme.Foraging, Theme.Farming, Theme.Fishing, Theme.Mining, Theme.Mixed }, ThemeDomains.RoomThemes);
        Assert.Equal(new[] { Theme.Spelunking, Theme.Artisan, Theme.Kitchen }, ThemeDomains.ActivityThemes);
        Assert.True(ThemeDomains.MatchesPerLine(Theme.Mixed));
        Assert.False(ThemeDomains.MatchesPerLine(Theme.Fishing));
    }
}
