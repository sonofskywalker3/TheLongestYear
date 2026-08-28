using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core;

/// <summary>Which fish a re-rolled fish bundle may ask for. Two rules:
/// <list type="bullet">
///   <item><see cref="ByHabitat"/>: a bundle keeps its water. Candidates share at least one
///   spawn location with the bundle's ORIGINAL fish (union empty, e.g. every original unknown
///   to the pool, falls back to the whole pool).</item>
///   <item><see cref="ForNightFishing"/> (Jeff, 2026-08-28): Night Fishing's vanilla ingredients
///   span every water, so the habitat rule let daytime ocean fish like Flounder in. It now
///   asks only for fish that are NOT catchable before 6pm anywhere (every Data/Fish biting
///   window opens at or after 1800), plus the Night Market's fish, which the filler caps at
///   <see cref="NightMarketFishPerBundle"/> per bundle.</item>
/// </list>
/// A Night Market fish is a real fish (Data/Objects category -4, so Seaweed does not count)
/// that spawns in the Submarine and is not already night-only by its own hours. The trio the
/// market is known for (Midnight Squid, Spook Fish, Blobfish) are listed for the Beach with
/// all-day hours in Data/Locations because the game gates them in code, so the Submarine
/// spawn is the only data signal, and it covers modded market fish the same way.</summary>
public static class FishBundleCandidates
{
    public const string NightFishingBundleName = "Night Fishing";
    public const string NightMarketLocation = "Submarine";
    public const int NightMarketFishPerBundle = 1;
    private const int FishCategory = -4;
    private const string ObjectQualifier = "(O)";

    public static bool IsNightFishingBundle(BundleSpec spec)
        => string.Equals(spec.Name, NightFishingBundleName, StringComparison.OrdinalIgnoreCase);

    public static IReadOnlyList<PoolItem> ByHabitat(BundleSpec spec, IReadOnlyList<PoolItem> fishPool)
    {
        var byId = fishPool.ToDictionary(p => p.ItemId, StringComparer.Ordinal);
        var habitat = new HashSet<string>(StringComparer.Ordinal);
        foreach (BundleSlotSpec slot in spec.Slots)
        {
            string normalizedId = BundleParsing.NormalizeItemId(slot.ItemId);
            if (!string.IsNullOrEmpty(normalizedId) && byId.TryGetValue(normalizedId, out var original))
                foreach (string location in original.Locations)
                    habitat.Add(location);
        }
        if (habitat.Count == 0)
            return fishPool;
        return fishPool.Where(p => p.Locations.Any(habitat.Contains)).ToList();
    }

    public static IReadOnlyList<PoolItem> ForNightFishing(
        IReadOnlyList<PoolItem> fishPool, IReadOnlyDictionary<string, RawFishEntry> fishRows)
        => fishPool.Where(p => IsNightOnly(p, fishRows) || IsNightMarketFish(p, fishRows)).ToList();

    public static bool IsNightMarketFish(PoolItem item, IReadOnlyDictionary<string, RawFishEntry> fishRows)
        => item.Category == FishCategory
           && item.Locations.Contains(NightMarketLocation)
           && !IsNightOnly(item, fishRows);

    private static bool IsNightOnly(PoolItem item, IReadOnlyDictionary<string, RawFishEntry> fishRows)
        => fishRows != null
           && fishRows.TryGetValue(Unqualify(item.ItemId), out RawFishEntry? row)
           && row.IsNightOnly();

    private static string Unqualify(string id)
        => id.StartsWith(ObjectQualifier, StringComparison.Ordinal) ? id.Substring(ObjectQualifier.Length) : id;
}
