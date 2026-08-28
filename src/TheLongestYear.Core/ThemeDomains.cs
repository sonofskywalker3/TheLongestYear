using System.Collections.Generic;

namespace TheLongestYear.Core;

/// <summary>Which goal lines a theme may ask for (activity-themes spec 2026-08-28). Room themes
/// match whole bundles by room (RoomThemeMap); the activity themes and Mixed match single lines
/// by item kind anywhere on the board, Mixed meaning anything.</summary>
public static class ThemeDomains
{
    public static readonly IReadOnlyList<Theme> RoomThemes =
        new[] { Theme.Foraging, Theme.Farming, Theme.Fishing, Theme.Mining, Theme.Mixed };

    public static readonly IReadOnlyList<Theme> ActivityThemes =
        new[] { Theme.Spelunking, Theme.Artisan, Theme.Kitchen };

    /// <summary>True when the theme's goals are matched line by line rather than bundle by bundle.</summary>
    public static bool MatchesPerLine(Theme theme)
        => theme is Theme.Spelunking or Theme.Artisan or Theme.Kitchen or Theme.Mixed;

    public static bool Matches(Theme theme, ItemKind kind) => theme switch
    {
        Theme.Spelunking => kind is ItemKind.Gem or ItemKind.Mineral or ItemKind.MonsterLoot or ItemKind.Artifact,
        Theme.Artisan => kind == ItemKind.ArtisanGood,
        Theme.Kitchen => kind is ItemKind.Cooking or ItemKind.Egg or ItemKind.Milk or ItemKind.AnimalProduct,
        Theme.Mixed => true,
        _ => false,
    };
}
