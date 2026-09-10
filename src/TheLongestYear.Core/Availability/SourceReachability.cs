using System;
using System.Collections.Generic;

namespace TheLongestYear.Core.Availability;

/// <summary>Whether an item is PROVABLY out of reach this run.
///
/// Sources are alternatives, so an item is only condemned when every known route to it is
/// unreachable, and an item nothing can trace stays allowed (Jeff, 2026-09-10). Over-exclusion
/// silently strips real mod content, which no player could diagnose; under-exclusion merely leaves
/// the status quo.
///
/// Answers are memoised per instance, so build one per generation and throw it away after.</summary>
public sealed class SourceReachability
{
    private readonly IReadOnlySet<string> _unreachableLocations;
    private readonly Dictionary<string, List<RawShopListing>> _listingsByItem;
    private readonly Dictionary<string, List<string>> _shopLocations;
    private readonly IReadOnlySet<string> _reachableSpawnIds;
    private readonly Dictionary<string, bool> _memo = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _reasons = new(StringComparer.Ordinal);

    public SourceReachability(
        IReadOnlySet<string> unreachableLocations,
        IReadOnlyList<RawShopListing> shopListings,
        IReadOnlyList<RawShopPlacement> shopPlacements,
        IReadOnlyList<RawCropEntry> crops,
        IReadOnlyList<RawRecipeEntry> recipes,
        IReadOnlySet<string> reachableSpawnIds)
    {
        _unreachableLocations = unreachableLocations ?? new HashSet<string>(StringComparer.Ordinal);
        _reachableSpawnIds = reachableSpawnIds ?? new HashSet<string>(StringComparer.Ordinal);

        _listingsByItem = new Dictionary<string, List<RawShopListing>>(StringComparer.Ordinal);
        foreach (RawShopListing listing in shopListings ?? Array.Empty<RawShopListing>())
        {
            if (listing == null || string.IsNullOrEmpty(listing.ItemId)) continue;
            string id = Qualify(listing.ItemId);
            if (!_listingsByItem.TryGetValue(id, out List<RawShopListing>? list))
                _listingsByItem[id] = list = new List<RawShopListing>();
            list.Add(listing);
        }

        _shopLocations = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (RawShopPlacement placement in shopPlacements ?? Array.Empty<RawShopPlacement>())
        {
            if (placement == null || string.IsNullOrEmpty(placement.ShopId)) continue;
            if (!_shopLocations.TryGetValue(placement.ShopId, out List<string>? list))
                _shopLocations[placement.ShopId] = list = new List<string>();
            if (!string.IsNullOrEmpty(placement.LocationName)) list.Add(placement.LocationName);
        }
    }

    /// <summary>Why each condemned id was condemned, for the log. Populated as
    /// <see cref="IsUnreachable"/> runs.</summary>
    public IReadOnlyDictionary<string, string> Reasons => _reasons;

    public bool IsUnreachable(string qualifiedItemId)
    {
        if (string.IsNullOrEmpty(qualifiedItemId)) return false;
        string id = Qualify(qualifiedItemId);
        if (_memo.TryGetValue(id, out bool cached)) return cached;

        bool verdict = Decide(id, out string reason);
        _memo[id] = verdict;
        if (verdict) _reasons[id] = reason;
        return verdict;
    }

    private bool Decide(string id, out string reason)
    {
        reason = "";

        // Positive proof beats every condemning rule. An item that spawns somewhere reachable
        // is reachable, whatever else also happens to list it.
        if (_reachableSpawnIds.Contains(id)) return false;

        bool anySourceKnown = false;

        if (BoughtSomewhere(id, out bool shopUnreachable))
        {
            anySourceKnown = true;
            if (!shopUnreachable) return false;   // a reachable shop is enough
            reason = "no reachable shop sells it";
        }

        return anySourceKnown;
    }

    /// <summary>True when some shop SELLS this item (recipe listings excluded).
    /// <paramref name="unreachable"/> is set when every such shop with a known placement sits
    /// somewhere unreachable.</summary>
    private bool BoughtSomewhere(string id, out bool unreachable)
    {
        unreachable = false;
        if (!_listingsByItem.TryGetValue(id, out List<RawShopListing>? listings)) return false;

        bool anySale = false, anyPlaced = false, allUnreachable = true;
        foreach (RawShopListing listing in listings)
        {
            if (listing.IsRecipe) continue;       // teaches the recipe, does not sell the item
            anySale = true;
            if (!_shopLocations.TryGetValue(listing.ShopId, out List<string>? places) || places.Count == 0)
            {
                // A shop nobody could place is an UNKNOWN route, not a closed one. The
                // Traveling Cart, Night Market and festival vendors are opened from game code
                // and have no discoverable placement, and mods open shops from dialogue and
                // events. Skipping these would let one island shop condemn an item the cart
                // sells every spring, so a single unplaced seller keeps the item allowed.
                allUnreachable = false;
                continue;
            }
            foreach (string place in places)
            {
                anyPlaced = true;
                if (!_unreachableLocations.Contains(place)) allUnreachable = false;
            }
        }
        if (!anySale) return false;
        unreachable = anyPlaced && allUnreachable;
        return true;
    }

    private static string Qualify(string id) => BundleParsing.NormalizeItemId(id);
}
