using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core;

/// <summary>Which fish a re-rolled fish bundle may ask for. Three rules:
/// <list type="bullet">
///   <item><see cref="ByHabitat"/>: a bundle keeps its water. Its habitat is every location
///   that holds a MAJORITY of the bundle's ORIGINAL fish, and candidates spawn in at least one
///   habitat location (Nexus posts 2026-10-03, Nerlana: "my lake fish bundle asks for catfish and
///   woodskip"). It used to take every location ANY original spawned in: Carp also bites in the
///   Secret Woods pond and the Sewer, so the Woods joined Lake Fish's water and let Woodskip and
///   Catfish in. A bundle whose originals share no majority water (Quality Fish: one lake, one
///   river, one ocean fish) keeps that old any-shared-location pool, and no known original at all
///   falls back to the whole pool.</item>
///   <item><see cref="ForSpecialty"/> (same report: "specialty fish asked for herring"): Specialty
///   Fish's originals sit at the Beach, the mines, the desert and the Woods, so the habitat rule
///   counted every Beach fish. It now asks only for hard-to-reach fish: a legendary, a fish
///   whose every spawn location is a gated place (<see cref="HardToReachLocations"/>), or an
///   open-water fish that is hard AND bites only briefly (<see cref="IsHardShortWindowFish"/>,
///   Jeff, 2026-10-05). An ordinary open-water fish never qualifies.</item>
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
    public const string SpecialtyFishBundleName = "Specialty Fish";
    public const string NightMarketLocation = "Submarine";
    public const int NightMarketFishPerBundle = 1;
    private const int FishCategory = -4;
    private const string ObjectQualifier = "(O)";

    /// <summary>A location is part of a bundle's habitat when it holds MORE than this share of
    /// the bundle's original fish (more than half).</summary>
    public const double HabitatMajorityShare = 0.5;

    /// <summary>Data/Locations keys that are gated or special places, not ordinary open water:
    /// the Secret Woods pond (behind the steel axe), the Calico Desert (bus repair), the mines
    /// and Skull Cavern, the Sewer (Rusty Key), the Night Market's submarine and beach, the
    /// Desert Festival and the volcano Caldera. Every location not listed counts as ordinary
    /// water, so a modded map is ordinary until it is added here.</summary>
    public static readonly IReadOnlySet<string> HardToReachLocations = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "Woods", "Desert", "DesertFestival", "UndergroundMine", "Mine", "SkullCave", "Sewer",
        NightMarketLocation, "BeachNightMarket", "Caldera",
    };

    /// <summary>Farm-type maps (Farm_Forest, Farm_Beach, ...) fish only on that one farm layout,
    /// so they say nothing about whether a fish is hard to reach: Woodskip also bites on the
    /// Forest Farm and is still the Secret Woods fish.</summary>
    private const string FarmMapPrefix = "Farm_";

    public static bool IsNightFishingBundle(BundleSpec spec)
        => string.Equals(spec.Name, NightFishingBundleName, StringComparison.OrdinalIgnoreCase);

    public static IReadOnlyList<PoolItem> ByHabitat(BundleSpec spec, IReadOnlyList<PoolItem> fishPool)
    {
        var byId = fishPool.ToDictionary(p => p.ItemId, StringComparer.Ordinal);
        var originalsAt = new Dictionary<string, int>(StringComparer.Ordinal);
        int knownOriginals = 0;
        foreach (BundleSlotSpec slot in spec.Slots)
        {
            string normalizedId = BundleParsing.NormalizeItemId(slot.ItemId);
            if (string.IsNullOrEmpty(normalizedId) || !byId.TryGetValue(normalizedId, out var original))
                continue;
            knownOriginals++;
            foreach (string location in original.Locations.Distinct(StringComparer.Ordinal))
                originalsAt[location] = originalsAt.TryGetValue(location, out int n) ? n + 1 : 1;
        }
        if (originalsAt.Count == 0)
            return fishPool;
        var habitat = new HashSet<string>(
            originalsAt.Where(kv => kv.Value > knownOriginals * HabitatMajorityShare).Select(kv => kv.Key),
            StringComparer.Ordinal);
        if (habitat.Count == 0)
            habitat.UnionWith(originalsAt.Keys);
        return fishPool.Where(p => p.Locations.Any(habitat.Contains)).ToList();
    }

    public static bool IsSpecialtyFishBundle(BundleSpec spec)
        => string.Equals(spec.Name, SpecialtyFishBundleName, StringComparison.OrdinalIgnoreCase);

    public static IReadOnlyList<PoolItem> ForSpecialty(
        IReadOnlyList<PoolItem> fishPool, IReadOnlyDictionary<string, RawFishEntry>? fishRows)
        => fishPool.Where(p => IsHardToReach(p)
                               || (fishRows != null
                                   && fishRows.TryGetValue(Unqualify(p.ItemId), out RawFishEntry? row)
                                   && IsHardShortWindowFish(row)))
            .ToList();

    /// <summary>Jeff's ruling, 2026-10-05: an open-water fish still counts as a specialty when it
    /// is both hard to land and bites only briefly. Against vanilla data that admits Pufferfish
    /// (80, noon to 4pm), Octopus (95, 6am to 1pm) and Super Cucumber (80, 6pm to 2am); Squid
    /// (75) stays out.</summary>
    public const int SpecialtyMinDifficulty = 80;
    public const double SpecialtyMaxDailyWindowHours = 8.0;

    public static bool IsHardShortWindowFish(RawFishEntry row)
        => !row.IsTrap
           && row.Difficulty >= SpecialtyMinDifficulty
           && row.DailyWindowHours() <= SpecialtyMaxDailyWindowHours;

    /// <summary>A legendary, or a fish with at least one spawn location outside the farm maps and
    /// every such location in <see cref="HardToReachLocations"/>.</summary>
    public static bool IsHardToReach(PoolItem item)
    {
        if (LegendaryFishRules.IsLegendary(item.ItemId))
            return true;
        var places = item.Locations
            .Where(l => !l.StartsWith(FarmMapPrefix, StringComparison.OrdinalIgnoreCase))
            .ToList();
        return places.Count > 0 && places.All(HardToReachLocations.Contains);
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
