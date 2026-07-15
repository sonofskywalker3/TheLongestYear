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
        var pickedNames = new HashSet<string>(StringComparer.Ordinal);
        for (int position = 0; position < slotPools.Count; position++)
        {
            var candidates = slotPools[position];
            IReadOnlyList<BundleSpec> pool = WithoutAlreadyPickedNames(candidates, pickedNames);
            var chosen = pool[rng.Next(pool.Count)];
            pickedNames.Add(chosen.Name);
            picks.Add(chosen with { Index = position });
        }
        return picks;
    }

    /// <summary>Mirrors vanilla's own duplicate-avoidance for RandomBundles
    /// (<c>BundleGenerator.Generate</c>, StardewValley/BundleGenerator.cs): vanilla draws every
    /// room position's bundle from ONE shared, mutable <c>Bundles</c> list and calls
    /// <c>list2.Remove(bundleData)</c> on every pick, so a candidate consumed by one position can
    /// never be offered to a later position in the same room (the same guarantee also falls out
    /// of <c>BundleSets</c> being chosen as a single atomic whole via <c>random.ChooseFrom</c>
    /// rather than mixed per-position). <see cref="VanillaBundlePool"/> builds independent
    /// per-position candidate lists rather than one shared consumable list (a documented,
    /// intentional widening -- see its class doc), so a same-named candidate can legitimately
    /// appear in more than one position's pool. We mirror vanilla's OUTCOME instead of its exact
    /// data structure: exclude any candidate whose Name was already picked earlier in this room,
    /// unless that would leave the position with zero candidates (in which case fall back to the
    /// full pool -- BundleEngine's global name-uniquifier is the last-resort backstop for that
    /// case). This is deterministic (filter, then a single rng.Next over the filtered list) but
    /// is NOT distribution-identical to the un-deduplicated picker: once an earlier position in
    /// the room consumes a name, every remaining position's odds shift across its own
    /// candidates.</summary>
    private static IReadOnlyList<BundleSpec> WithoutAlreadyPickedNames(
        IReadOnlyList<BundleSpec> candidates, HashSet<string> pickedNames)
    {
        List<BundleSpec>? unpicked = null;
        foreach (BundleSpec candidate in candidates)
        {
            if (pickedNames.Contains(candidate.Name))
                continue;
            (unpicked ??= new List<BundleSpec>()).Add(candidate);
        }
        return unpicked is { Count: > 0 } ? unpicked : candidates;
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
