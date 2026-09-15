namespace TheLongestYear.Core.Sabotage;

/// <summary>Every number the Darkness dial changes (spec 2026-09-15 darkness-obtainability-wiring,
/// sections 1.5, 2.4 and 2.5). Jeff's numbers, 2026-09-14 and 2026-09-15; retune here.</summary>
public static class DarknessLevels
{
    private const double ShareEasy = 0.04, ShareNormal = 0.05, ShareHard = 0.06, ShareExtreme = 0.07;
    private const int SummerCapEasy = 8, SummerCapNormal = 10, SummerCapHard = 12, SummerCapExtreme = 14;
    private const int OtherCapEasy = 12, OtherCapNormal = 15, OtherCapHard = 18, OtherCapExtreme = 21;
    private const double UnmoderatedHard = 0.10, UnmoderatedExtreme = 0.30;

    /// <summary>A placed or stored machine counts as this many units when chest blight picks it
    /// (Jeff, 2026-09-15: "so they can't be decimated in one night").</summary>
    public const int BigCraftableUnits = 3;

    /// <summary>Share of live crops (crop blight) or stored units (chest blight) a strike takes.</summary>
    public static double BlightShare(DifficultyStep level) => level switch
    {
        DifficultyStep.Easy => ShareEasy,
        DifficultyStep.Hard => ShareHard,
        DifficultyStep.Extreme => ShareExtreme,
        _ => ShareNormal,
    };

    /// <summary>The most a strike takes; Summer is gentler than Fall and Winter.</summary>
    public static int BlightCap(DifficultyStep level, Season season)
        => season == Season.Summer
            ? level switch
            {
                DifficultyStep.Easy => SummerCapEasy,
                DifficultyStep.Hard => SummerCapHard,
                DifficultyStep.Extreme => SummerCapExtreme,
                _ => SummerCapNormal,
            }
            : level switch
            {
                DifficultyStep.Easy => OtherCapEasy,
                DifficultyStep.Hard => OtherCapHard,
                DifficultyStep.Extreme => OtherCapExtreme,
                _ => OtherCapNormal,
            };

    /// <summary>The chance a reversion or tamper ignores the fairness picker (once per loop each).
    /// Jeff, 2026-09-15: "10% chance for hard, 30% on extreme".</summary>
    public static double UnmoderatedChance(DifficultyStep level) => level switch
    {
        DifficultyStep.Hard => UnmoderatedHard,
        DifficultyStep.Extreme => UnmoderatedExtreme,
        _ => 0.0,
    };

    /// <summary>On Extreme, chest blight can take tools, weapons, rings, boots, hats, stored machines
    /// and machines placed on the farm (spec section 2.5).</summary>
    public static bool StorageReachesEverything(DifficultyStep level) => level == DifficultyStep.Extreme;
}
