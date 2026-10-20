using System;
using System.Collections.Generic;

namespace TheLongestYear.Core;

/// <summary>The night_event wildcard twist (spec section 8): one of the game's own random night
/// events, forced. The roll picks the first event to try; the glue tries each event in the order
/// below and plays the first one whose own setup does not cancel.</summary>
public static class WildcardNightEvents
{
    private const int NightSalt = 0x4E17;

    public const string Fairy = "fairy";
    public const string Witch = "witch";
    public const string Meteorite = "meteorite";
    public const string Owl = "owl";
    public const string Capsule = "capsule";

    public static readonly IReadOnlyList<string> All = new[] { Fairy, Witch, Meteorite, Owl, Capsule };

    /// <summary>Every event once, in a seeded random order. The first pick is the same roll as
    /// before 0.19.125; each fallback after it is a random event not tried yet, drawn from the same
    /// seeded generator, so a save and week always get the same order. Until 0.19.125 a cancelled
    /// event stepped to its neighbour in <see cref="All"/>, and on a young farm (fairy, witch and
    /// meteorite often cancelling) the owl collected their share.</summary>
    public static IReadOnlyList<string> TryOrder(int seed, int weekOfYear)
    {
        Random rng = RollSeed.Rng(seed, weekOfYear, NightSalt);
        var untried = new List<string>(All);
        var order = new List<string>(All.Count);
        while (untried.Count > 0)
        {
            int pick = rng.Next(untried.Count);
            order.Add(untried[pick]);
            untried.RemoveAt(pick);
        }
        return order;
    }
}
