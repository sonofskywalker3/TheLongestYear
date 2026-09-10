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
    private readonly Dictionary<string, List<string>> _seedByHarvest;
    private readonly IReadOnlySet<string> _reachableSpawnIds;
    private readonly Dictionary<string, bool> _memo = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _reasons = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<RawRecipeEntry>> _recipesByOutput;
    private readonly HashSet<string> _inProgress = new(StringComparer.Ordinal);

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

        // A LIST, not a single value: mods define several seeds yielding one harvest, and a
        // scalar would let whichever row enumerated last erase a reachable alternative.
        _seedByHarvest = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (RawCropEntry crop in crops ?? Array.Empty<RawCropEntry>())
        {
            if (crop?.HarvestItemId == null || string.IsNullOrEmpty(crop.SeedItemId)) continue;
            string harvest = Qualify(crop.HarvestItemId);
            if (!_seedByHarvest.TryGetValue(harvest, out List<string>? seeds))
                _seedByHarvest[harvest] = seeds = new List<string>();
            string seed = Qualify(crop.SeedItemId);
            if (!seeds.Contains(seed)) seeds.Add(seed);
        }

        // A LIST for the same reason as the seeds: a reachable vanilla recipe and an
        // unreachable mod recipe can produce the same object, and the last one written must not
        // become the only one considered.
        _recipesByOutput = new Dictionary<string, List<RawRecipeEntry>>(StringComparer.Ordinal);
        foreach (RawRecipeEntry recipe in recipes ?? Array.Empty<RawRecipeEntry>())
        {
            if (recipe?.OutputItemId == null) continue;
            string output = Qualify(recipe.OutputItemId);
            if (!_recipesByOutput.TryGetValue(output, out List<RawRecipeEntry>? list))
                _recipesByOutput[output] = list = new List<RawRecipeEntry>();
            list.Add(recipe);
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

        // A cycle means we are already asking this question further up the stack. Answer
        // "reachable" so a loop can never condemn an item on its own account.
        if (!_inProgress.Add(id)) return false;
        try
        {
            bool verdict = Decide(id, out string reason);
            _memo[id] = verdict;
            if (verdict) _reasons[id] = reason;
            return verdict;
        }
        finally
        {
            _inProgress.Remove(id);
        }
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

        if (_seedByHarvest.TryGetValue(id, out List<string>? seeds) && seeds.Count > 0)
        {
            anySourceKnown = true;
            string? blockedSeed = null;
            foreach (string seed in seeds)
            {
                if (!IsUnreachable(seed)) { blockedSeed = null; break; }   // one good seed is enough
                blockedSeed ??= seed;
            }
            if (blockedSeed == null) return false;
            reason = $"its seed {blockedSeed} is out of reach";
        }

        if (_recipesByOutput.TryGetValue(id, out List<RawRecipeEntry>? recipeList) && recipeList.Count > 0)
        {
            anySourceKnown = true;
            string? recipeReason = null;
            foreach (RawRecipeEntry recipe in recipeList)
            {
                string? blocked = FirstUnreachableIngredient(recipe);
                if (blocked != null)
                    recipeReason ??= $"ingredient {blocked} is out of reach";
                else if (!RecipeLearnable(id, recipe))
                    recipeReason ??= "its recipe cannot be learned anywhere reachable";
                else
                    return false;   // one cookable route is enough
            }
            reason = recipeReason ?? "";
        }

        return anySourceKnown;
    }

    private string? FirstUnreachableIngredient(RawRecipeEntry recipe)
    {
        foreach (string ingredient in recipe.IngredientItemIds ?? Array.Empty<string>())
        {
            if (string.IsNullOrEmpty(ingredient)) continue;
            // A category ref ("any fish") is satisfied by many items; treat it as reachable.
            if (BundleParsing.IsCategoryRef(ingredient)) continue;
            string qualified = Qualify(ingredient);
            if (IsUnreachable(qualified)) return qualified;
        }
        return null;
    }

    /// <summary>Whether the player could ever learn this recipe. The unlock field names a normal
    /// route (a skill level, the TV, friendship, or simply known from the start), OR some shop in a
    /// reachable place teaches it. "none" means no normal route exists, which leaves only the
    /// shops. Nexus posts 2026-09-10: every Fishmonger recipe is "none" and taught only on Ginger
    /// Island, which is what rules out its five all-vanilla dishes.</summary>
    private bool RecipeLearnable(string id, RawRecipeEntry recipe)
    {
        string unlock = (recipe.Unlock ?? "").Trim();
        // Only the LITERAL string "none" is treated as "no normal route". Anything else,
        // including an empty or missing field, counts as learnable. Verified against the live
        // Data/CookingRecipes on 2026-09-10: all 81 vanilla recipes use l, f, s, default or the
        // literal string "null", and NOT ONE uses "none" or an empty field. So this rule cannot
        // touch vanilla cooking, and condemning an unparsed field would be inventing proof.
        if (!unlock.Equals("none", StringComparison.OrdinalIgnoreCase))
            return true;

        if (!_listingsByItem.TryGetValue(id, out List<RawShopListing>? listings)) return false;
        bool anyPlaced = false, allUnreachable = true;
        foreach (RawShopListing listing in listings)
        {
            if (!listing.IsRecipe) continue;
            if (!_shopLocations.TryGetValue(listing.ShopId, out List<string>? places)) continue;
            foreach (string place in places)
            {
                anyPlaced = true;
                if (!_unreachableLocations.Contains(place)) allUnreachable = false;
            }
        }
        // Nothing teaches it anywhere we can place: not learnable. A recipe taught by an
        // unplaced shop keeps the benefit of the doubt.
        return !anyPlaced || !allUnreachable;
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
