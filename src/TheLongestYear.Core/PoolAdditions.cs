using System.Collections.Generic;

namespace TheLongestYear.Core;

public sealed record PoolAddition(string ItemId, IReadOnlyList<Season> Seasons, IReadOnlyList<string> Locations, int Weight);

/// <summary>Items the game data does not put in a pool but Jeff wants on the board at the jelly
/// rate (weight 1): the three mine fish MineShaft.getFish hard-codes by area, the five legendaries
/// (fish_legendary, CatchLimit 1; the rewind clears the catch), and the year-2 crops (a Boost or a
/// permanent buy is their route). Spec 2026-08-28-obtainable-board, section 3.</summary>
public static class PoolAdditions
{
    private static readonly string[] Mine = { "UndergroundMine" };
    private static readonly Season[] Any = System.Array.Empty<Season>();

    public static readonly IReadOnlyList<PoolAddition> Fish = new[]
    {
        new PoolAddition("(O)158", Any, Mine, 1),                              // Stonefish, floors 1 to 39
        new PoolAddition("(O)161", Any, Mine, 1),                              // Ice Pip, floors 40 to 79
        new PoolAddition("(O)162", Any, Mine, 1),                              // Lava Eel, floors 80 to 119
        new PoolAddition("(O)163", new[] { Season.Spring }, new[] { "Mountain" }, 1),   // Legend, rain, Fishing 10
        new PoolAddition("(O)159", new[] { Season.Summer }, new[] { "Beach" }, 1),      // Crimsonfish, Fishing 5
        new PoolAddition("(O)160", new[] { Season.Fall }, new[] { "Town" }, 1),         // Angler, Fishing 3
        new PoolAddition("(O)775", new[] { Season.Winter }, new[] { "Forest" }, 1),     // Glacierfish, Fishing 6
        new PoolAddition("(O)682", Any, new[] { "Sewer" }, 1),                          // Mutant Carp
    };

    public static readonly IReadOnlySet<string> YearTwoCropIds =
        new HashSet<string>(System.StringComparer.Ordinal) { "(O)248", "(O)266", "(O)274" };

    public static readonly IReadOnlySet<string> VetExceptions =
        new HashSet<string>(System.StringComparer.Ordinal) { "(O)158", "(O)161", "(O)162", "(O)163", "(O)159", "(O)160", "(O)775", "(O)682" };
}
