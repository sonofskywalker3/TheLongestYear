using System.Collections.Generic;

namespace TheLongestYear.Core;

/// <summary>The night_event wildcard twist (spec section 8): one of the game's own random night
/// events, forced. The roll picks where to start in the list; the glue tries each event in this
/// order and plays the first one whose own setup does not cancel.</summary>
public static class WildcardNightEvents
{
    private const int NightSalt = 0x4E17;

    public const string Fairy = "fairy";
    public const string Witch = "witch";
    public const string Meteorite = "meteorite";
    public const string Owl = "owl";
    public const string Capsule = "capsule";

    public static readonly IReadOnlyList<string> All = new[] { Fairy, Witch, Meteorite, Owl, Capsule };

    /// <summary>Every event once, starting at the seeded pick and wrapping around.</summary>
    public static IReadOnlyList<string> TryOrder(int seed, int weekOfYear)
    {
        int start = RollSeed.Rng(seed, weekOfYear, NightSalt).Next(All.Count);
        var order = new List<string>(All.Count);
        for (int i = 0; i < All.Count; i++)
            order.Add(All[(start + i) % All.Count]);
        return order;
    }
}
