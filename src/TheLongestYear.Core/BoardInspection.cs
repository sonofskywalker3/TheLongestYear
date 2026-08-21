using System;
using System.Collections.Generic;

namespace TheLongestYear.Core;

/// <summary>Read-only questions about a live Community Center board (raw BundleData).
/// Pure — parses through <see cref="BundleParsing"/> so it sees exactly what the game
/// sees.</summary>
public static class BoardInspection
{
    private const string ObjectIdPrefix = "(O)";

    /// <summary>True when any bundle on the board has a concrete (non-category) ingredient
    /// slot whose qualified id is not an Object — e.g. Gil's Trophies' weapon/hat slots.
    /// Used to keep the weapon/hat donation patches live for the rest of a loop whose board
    /// was composed with them, even after <c>EnableNonObjectDonations</c> is turned off
    /// (spec 2026-08-21: the flag governs the NEXT board; an in-flight board keeps working).</summary>
    public static bool HasNonObjectIngredients(IReadOnlyDictionary<string, string> bundleData)
    {
        if (bundleData == null) throw new ArgumentNullException(nameof(bundleData));

        foreach (KeyValuePair<string, string> kvp in bundleData)
        {
            ParsedBundle bundle = BundleParsing.Parse(kvp.Key, kvp.Value);
            foreach (BundleIngredient ing in bundle.Ingredients)
            {
                if (BundleParsing.IsCategoryRef(ing.ItemRef)) continue;
                string id = BundleParsing.NormalizeItemId(ing.ItemRef);
                if (!id.StartsWith(ObjectIdPrefix, StringComparison.Ordinal))
                    return true;
            }
        }
        return false;
    }
}
