using System;
using System.Collections.Generic;

namespace TheLongestYear.Core;

/// <summary>Deterministically picks one bundle per room position from that position's
/// candidate pool (pool shape mirrors Data/RandomBundles: element i = variants for the
/// room's i-th bundle). Picks are re-indexed 0..n-1 so the written BundleData has
/// sequential per-room indices regardless of which variants won.</summary>
public static class RemixSelector
{
    public static IReadOnlyList<BundleSpec> PickForRoom(
        IReadOnlyList<IReadOnlyList<BundleSpec>> slotPools, int seed, string room)
    {
        var rng = new Random(seed ^ (StableRoomSalt(room) * 7919));
        var picks = new List<BundleSpec>(slotPools.Count);
        for (int position = 0; position < slotPools.Count; position++)
        {
            var candidates = slotPools[position];
            var chosen = candidates[rng.Next(candidates.Count)];
            picks.Add(chosen with { Index = position });
        }
        return picks;
    }

    /// <summary>Deterministic, culture/runtime-stable salt for a room name (string.GetHashCode
    /// is randomized per process in .NET — never use it for persisted determinism).</summary>
    private static int StableRoomSalt(string room)
    {
        int hash = 17;
        foreach (char c in room) hash = unchecked(hash * 31 + c);
        return hash;
    }
}
