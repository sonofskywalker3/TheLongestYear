using System;
using System.Collections.Generic;
using TheLongestYear.Core.Availability;

namespace TheLongestYear.Core;

/// <summary>Every judgement number behind "the first week of the year an item can exist" (spec
/// 2026-08-28-even-year-availability). Facts read from game data live in the rules; what lives
/// here is the pacing Jeff ruled on: 30 mine floors a week, Skull Cavern from Fall, a kitchen by
/// Summer, and so on. Weeks are 1 to 16 (Calendar.WeekOfYear).</summary>
public static class AvailabilityWeeks
{
    /// <summary>An item no rule placed. Winter, the safe direction for a deadline.</summary>
    public const int UnknownWeek = 13;
    public const int KitchenWeek = 5;
    /// <summary>A dish the Saloon or a Cookout Kit can supply without a kitchen.</summary>
    public const int ShopDishWeek = 3;
    public const int PondDelayWeeks = 4;
    public const int SaplingWeek = 1;
    public const int ArtifactWeek = 1;
    public const int SalmonberryWeek = 3;
    public const int BlackberryWeek = 10;
    public const int SkullCavernWeek = 9;
    public const int SewerWeek = 5;
    public const int SwampWeek = 13;
    /// <summary>Jeff, 2026-08-28: 30 floors a week for the theme goals.</summary>
    public const int MineFloorsPerWeek = 30;

    /// <summary>Crops whose seeds only a festival or the cart sells: the harvest cannot come
    /// before that week.</summary>
    public static readonly IReadOnlyDictionary<string, int> FestivalCropWeeks =
        new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["(O)400"] = 3,    // Strawberry, Egg Festival Spring 13
            ["(O)417"] = 13,   // Sweet Gem Berry, Rare Seed from the cart, 24 days
        };

    /// <summary>Bush berries have no spawn rows; their weeks are calendar facts.</summary>
    public static readonly IReadOnlyDictionary<string, int> BushBerryWeeks =
        new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["(O)296"] = SalmonberryWeek,
            ["(O)410"] = BlackberryWeek,
        };

    public static Season SeasonOf(int week)
    {
        int clamped = Math.Clamp(week, 1, Calendar.WeeksPerYear);
        return (Season)((clamped - 1) / Calendar.WeeksPerMonth);
    }

    public static int FirstWeekOf(Season season) => (int)season * Calendar.WeeksPerMonth + 1;
    public static int LastWeekOf(Season season) => ((int)season + 1) * Calendar.WeeksPerMonth;

    /// <summary>Theme-goal week for a mine area: floors 1 to 39 week 1, 41 to 79 week 2,
    /// 81 to 119 week 3, Skull Cavern Fall week 9.</summary>
    public static int MineAreaWeek(int area) => area switch
    {
        MineAreas.Area0 or MineAreas.Area10 => 1,
        MineAreas.Area40 => 2,
        MineAreas.Area80 => 3,
        _ => SkullCavernWeek,
    };

    /// <summary>The gate is softer than the goal for the deep mine: below floor 80 a Spring
    /// gate may demand it, 80 and deeper waits for Summer (Jeff accepted this, 2026-08-28).</summary>
    public static Season MineAreaGateSeason(int area) => area switch
    {
        MineAreas.Area0 or MineAreas.Area10 or MineAreas.Area40 => Season.Spring,
        MineAreas.Area80 => Season.Summer,
        _ => Season.Fall,
    };

    /// <summary>Week a machine unlocked at a skill level is realistically running.</summary>
    public static int MachineLevelWeek(int level) => level switch
    {
        <= 2 => 2,
        3 => 3,
        4 or 5 => 4,
        6 or 7 => 6,
        8 or 9 => 7,
        _ => 9,
    };

    /// <summary>Week an animal building tier is realistically up: base coop or barn week 2,
    /// big week 5, deluxe week 9. links = upgrades above the base building.</summary>
    public static int HousingTierWeek(int links) => links switch
    {
        0 => 2,
        1 => 5,
        _ => 9,
    };
}
