using System.Collections.Generic;

namespace TheLongestYear.Core.Sabotage;

/// <summary>The shrine wards that buy off blight (spec 2026-09-09, Jeff's ruling the same day):
/// the Junimos can protect your crops, one Ward of the Fields per blighted season, and nothing
/// else. The hall has no ward; reversion and tampering answer only to their config switches.
/// A ward covers crops in the ground, not what sits in a chest.</summary>
public static class WardIds
{
    public const string CropsSummer = "ward_crops_summer";
    public const string CropsFall = "ward_crops_fall";
    public const string CropsWinter = "ward_crops_winter";

    public const long CropsSummerCost = 250;
    public const long CropsFallCost = 300;
    public const long CropsWinterCost = 300;

    public static readonly IReadOnlyList<string> All = new[] { CropsSummer, CropsFall, CropsWinter };

    /// <summary>The crop ward that covers a season, or null when the season has no blight.</summary>
    public static string? CropWardFor(Season season) => season switch
    {
        Season.Summer => CropsSummer,
        Season.Fall => CropsFall,
        Season.Winter => CropsWinter,
        _ => null,
    };
}
