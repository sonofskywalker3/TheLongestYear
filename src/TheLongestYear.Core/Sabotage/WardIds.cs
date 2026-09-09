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

    /// <summary>Circle of Warding (Jeff, 2026-09-09): a placeable 3x3 circle, bought up to three
    /// times on an escalating ladder. Crops and chests on its tiles are beyond the darkness.</summary>
    public const string Circle1 = "ward_circle_1";
    public const string Circle2 = "ward_circle_2";
    public const string Circle3 = "ward_circle_3";
    public const long Circle1Cost = 400;
    public const long Circle2Cost = 800;
    public const long Circle3Cost = 1600;
    public static readonly IReadOnlyList<string> Circles = new[] { Circle1, Circle2, Circle3 };

    public static readonly IReadOnlyList<string> All = new[] { CropsSummer, CropsFall, CropsWinter, Circle1, Circle2, Circle3 };

    /// <summary>How many circles the player owns: the highest tier held (tiers chain).</summary>
    public static int CircleCount(System.Func<string, bool> hasUpgrade)
    {
        if (hasUpgrade is null) return 0;
        int n = 0;
        foreach (string id in Circles) if (hasUpgrade(id)) n++;
        return n;
    }

    /// <summary>The crop ward that covers a season, or null when the season has no blight.</summary>
    public static string? CropWardFor(Season season) => season switch
    {
        Season.Summer => CropsSummer,
        Season.Fall => CropsFall,
        Season.Winter => CropsWinter,
        _ => null,
    };
}
