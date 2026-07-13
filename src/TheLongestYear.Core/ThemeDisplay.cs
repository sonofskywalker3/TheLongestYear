namespace TheLongestYear.Core;

/// <summary>Localized display names for enums whose raw .ToString() is persisted/parsed —
/// display sites call these; storage keeps the raw enum name.</summary>
public static class ThemeDisplay
{
    public static string Name(Theme theme)
        => Strings.Get($"theme.{theme.ToString().ToLowerInvariant()}");

    public static string CategoryName(UpgradeCategory category)
        => Strings.Get($"upgrade-category.{category.ToString().ToLowerInvariant()}");
}
