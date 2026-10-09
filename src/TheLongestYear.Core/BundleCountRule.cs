using System;

namespace TheLongestYear.Core;

/// <summary>The bundle-count dial, resolved: how many bundles a themed room of a TLY Custom board
/// holds, given the room's standard count (spec 2026-10-09-bundle-count-dial, Jeff's rulings).
/// Stamped on <see cref="DifficultyProfile.BundleCount"/> as resolved numbers rather than the
/// step, like every other dial, so retuning a step later never reshapes a board in flight.
///
/// Easy: one or two fewer, floor 2. Normal: the standard count, cap 6. Hard: one or two more,
/// cap 8. Extreme: always 9, the room page's 9 fixed bag spots.</summary>
public sealed record BundleCountRule
{
    /// <summary>The fewest bundles the dial will leave in a room that had at least this many.</summary>
    public const int RoomFloor = 2;

    public const int NormalCap = 6;
    public const int HardCap = 8;

    /// <summary>The room page draws bags at 9 fixed spots (JunimoNoteMenu.getBundleLocationFromNumber).</summary>
    public const int ExtremeCount = 9;

    /// <summary>Lowest change to the standard count (inclusive).</summary>
    public int DeltaMin { get; init; }

    /// <summary>Highest change to the standard count (inclusive).</summary>
    public int DeltaMax { get; init; }

    public int Floor { get; init; } = RoomFloor;

    public int Cap { get; init; } = NormalCap;

    /// <summary>When set, every room holds exactly this many, whatever its standard count.</summary>
    public int? Exact { get; init; }

    public static BundleCountRule For(DifficultyStep step) => step switch
    {
        DifficultyStep.Easy => new BundleCountRule { DeltaMin = -2, DeltaMax = -1, Cap = NormalCap },
        DifficultyStep.Hard => new BundleCountRule { DeltaMin = 1, DeltaMax = 2, Cap = HardCap },
        DifficultyStep.Extreme => new BundleCountRule { Exact = ExtremeCount, Cap = ExtremeCount },
        _ => new BundleCountRule { Cap = NormalCap },
    };

    /// <summary>The count for a room with <paramref name="standard"/> positions. Consumes one
    /// <paramref name="rng"/> draw only when the change is a range (Easy, Hard).</summary>
    public int Target(int standard, Random rng)
    {
        if (Exact is int exact)
            return exact;
        int delta = DeltaMin == DeltaMax ? DeltaMin : rng.Next(DeltaMin, DeltaMax + 1);
        int target = Math.Min(standard + delta, Cap);
        return Math.Max(target, Math.Min(Floor, standard));
    }
}
