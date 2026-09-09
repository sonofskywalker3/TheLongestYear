using System.Collections.Generic;

namespace TheLongestYear.Core.Sabotage;

/// <summary>The shrine wards that buy off a front (spec 2026-09-09): one Ward of the Fields per
/// blighted season, one Ward of the Hall per item room. Tampering has no ward by design.</summary>
public static class WardIds
{
    public const string CropsSummer = "ward_crops_summer";
    public const string CropsFall = "ward_crops_fall";
    public const string HallPantry = "ward_hall_pantry";
    public const string HallCraftsRoom = "ward_hall_craftsroom";
    public const string HallFishTank = "ward_hall_fishtank";
    public const string HallBoilerRoom = "ward_hall_boilerroom";
    public const string HallBulletin = "ward_hall_bulletin";

    public const long CropsSummerCost = 250;
    public const long CropsFallCost = 300;
    public const long HallCost = 200;

    public static readonly IReadOnlyList<string> All = new[]
    {
        CropsSummer, CropsFall, HallPantry, HallCraftsRoom, HallFishTank, HallBoilerRoom, HallBulletin,
    };

    /// <summary>The crop ward that covers a season, or null when the season has no blight.</summary>
    public static string? CropWardFor(Season season) => season switch
    {
        Season.Summer => CropsSummer,
        Season.Fall => CropsFall,
        _ => null,
    };

    /// <summary>The hall ward that covers a bundle, by its room theme (BundleRequirement.Theme is
    /// the room's theme, see RoomThemeMap). Null for a theme no item room carries.</summary>
    public static string? HallWardFor(Theme roomTheme) => roomTheme switch
    {
        Theme.Farming => HallPantry,
        Theme.Foraging => HallCraftsRoom,
        Theme.Fishing => HallFishTank,
        Theme.Mining => HallBoilerRoom,
        Theme.Mixed => HallBulletin,
        _ => null,
    };
}
