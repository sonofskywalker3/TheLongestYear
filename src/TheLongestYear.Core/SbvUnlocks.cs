using System;
using System.Collections.Generic;

namespace TheLongestYear.Core;

/// <summary>
/// Sunberry Village's lasting progress that the mod keeps as "this scene was seen" or in the master
/// player's mod data, and how it rewinds: with the loop, like Seven Deadly Sins' (<see cref="SdsUnlocks"/>).
/// None of these scenes carries friendship in its key or a mail, recipe or quest command, so the seen
/// re-seed carried each into every later loop (1.1.2, Data/Events/EventData.json):
/// <list type="bullet">
/// <item>The Bottom of the Sunberry Mines (20031445, 20031446, 20031447): 20031446 opens a Crystal Cave
/// shortcut straight to the Bottom (MapWarps.json, Entrance to Final) and triggers the Golden Sunberry
/// letter.</item>
/// <item>The Golden Sunberry chain (20031448, then 20031451 to 20031457; 20031449 and 20031450 are heart scenes): its stage is rebuilt from these ids
/// (SaveFixer.cs), and the last one opens the Seed Maker's Golden Sunberry Seeds roll.</item>
/// <item>The Moonberry shop (20031458): its counter in the Village appears once the scene is seen.</item>
/// <item>The Alpaca (20031489): Marnie sells it once the scene is seen (Data/FarmAnimals UnlockCondition).</item>
/// </list>
/// The Golden Sunberry's growth is also kept in the master player's mod data
/// (<see cref="GoldenSunberryModDataPrefix"/>, GoldenSunberryStageToken.cs), which the rewind keeps; left
/// there, the next loop starts at the stage the last one reached while its letters and scenes are gone.
/// </summary>
public static class SbvUnlocks
{
    /// <summary>The Content Patcher pack's id: the one that carries the events and items.</summary>
    public const string ModId = "skellady.SBVCP";

    private const string EventPrefix = "skellady.SBVCP_";

    /// <summary>The Golden Sunberry's growth: Growing, Stage and DaysInStage under this prefix.</summary>
    public const string GoldenSunberryModDataPrefix = "SunberryTeam.SBVSMAPI/GoldenSunberry/";

    /// <summary>The Bottom's scenes, first to last.</summary>
    public static IReadOnlyList<string> BottomScenes { get; } = Ids("20031445", "20031446", "20031447");

    /// <summary>The Golden Sunberry stages, first to last.</summary>
    public static IReadOnlyList<string> GoldenSunberryChain { get; } = Ids(
        "20031448", "20031451", "20031452", "20031453", "20031454", "20031455", "20031456", "20031457");

    public static string MoonberryShopScene { get; } = EventPrefix + "20031458";

    public static string AlpacaScene { get; } = EventPrefix + "20031489";

    /// <summary>Seen-event ids the rewind must not put back.</summary>
    public static IReadOnlySet<string> RewoundEventIds { get; } = BuildRewoundEventIds();

    /// <summary>True for a master-player mod data key the rewind clears.</summary>
    public static bool IsRewoundModDataKey(string? key)
        => key != null && key.StartsWith(GoldenSunberryModDataPrefix, StringComparison.Ordinal);

    private static string[] Ids(params string[] numbers)
        => Array.ConvertAll(numbers, n => EventPrefix + n);

    private static HashSet<string> BuildRewoundEventIds()
    {
        var ids = new HashSet<string>(StringComparer.Ordinal) { MoonberryShopScene, AlpacaScene };
        ids.UnionWith(BottomScenes);
        ids.UnionWith(GoldenSunberryChain);
        return ids;
    }
}
