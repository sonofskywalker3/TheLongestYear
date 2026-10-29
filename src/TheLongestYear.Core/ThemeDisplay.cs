namespace TheLongestYear.Core;

/// <summary>Localized display names for enums whose raw .ToString() is persisted/parsed —
/// display sites call these; storage keeps the raw enum name.</summary>
public static class ThemeDisplay
{
    public static string Name(Theme theme)
        => Strings.Get($"theme.{theme.ToString().ToLowerInvariant()}");

    public static string CategoryName(UpgradeCategory category)
        => Strings.Get($"upgrade-category.{category.ToString().ToLowerInvariant()}");

    /// <summary>Short label for the shrine's category tab. "Gifts of the Junimos" is too wide for
    /// a tab, so Gifts has its own key; every other tab reuses the full category name.</summary>
    public static string CategoryTabName(UpgradeCategory category)
        => category == UpgradeCategory.Gifts
            ? Strings.Get("upgrade-category.gifts.tab")
            : CategoryName(category);

    /// <summary>True when the shrine panel is empty and the category has an explanation to draw.
    /// Only Gifts can be legitimately empty (a row appears once a Community Center room is
    /// finished this loop).</summary>
    public static bool ShowsEmptyNote(UpgradeCategory category, int rowCount)
        => rowCount == 0 && category == UpgradeCategory.Gifts;
}
