using System;
using System.Collections.Generic;

namespace TheLongestYear.Core;

/// <summary>
/// Pure computation of the "anything that can feed a CC bundle" item set, used by the Cart Whisperer
/// shrine display. An item qualifies when it is a direct bundle ingredient, OR a seed whose crop is a
/// bundle item, OR an ingredient of a recipe whose product is a bundle item (one craft step). All ids
/// are caller-normalized (qualified, e.g. "(O)24"); the glue gathers the maps from live game data.
/// Category-based bundle slots are handled separately in the glue (by the item's Category).
/// </summary>
public static class BundleRelevance
{
    public static HashSet<string> BuildRelevantItemIds(
        IEnumerable<string> bundleItemIds,
        IReadOnlyDictionary<string, string> seedToCrop,
        IReadOnlyDictionary<string, IReadOnlyCollection<string>> productToIngredients)
    {
        var bundle = new HashSet<string>(bundleItemIds, StringComparer.Ordinal);
        var relevant = new HashSet<string>(bundle, StringComparer.Ordinal);

        // Seeds whose harvested crop is a bundle item.
        foreach (KeyValuePair<string, string> sc in seedToCrop)
            if (bundle.Contains(sc.Value))
                relevant.Add(sc.Key);

        // Ingredients of any recipe whose product is a bundle item.
        foreach (KeyValuePair<string, IReadOnlyCollection<string>> pi in productToIngredients)
            if (bundle.Contains(pi.Key))
                foreach (string ingredient in pi.Value)
                    relevant.Add(ingredient);

        return relevant;
    }
}
