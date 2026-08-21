using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

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

    /// <summary>Order-independent fingerprint of a board (keys + values). Used to notice a
    /// bundle mod rewriting BundleData mid-day (Challenging CC Bundles swaps values on
    /// DayStarted) so Vanilla mode can re-classify.</summary>
    public static string Fingerprint(IReadOnlyDictionary<string, string> bundleData)
    {
        if (bundleData == null) throw new ArgumentNullException(nameof(bundleData));
        var sb = new StringBuilder();
        foreach (string key in bundleData.Keys.OrderBy(k => k, StringComparer.Ordinal))
            sb.Append(key).Append('=').Append(bundleData[key]).Append('\n');
        return sb.ToString();
    }

    /// <summary>True when every live entry exists in <paramref name="reference"/> with an
    /// identical value — i.e. the live board IS the reference (Standard) set. Any differing
    /// value ⇒ not the reference (a Remixed roll, or an engine board). Extra reference keys the
    /// live board lacks are ignored.</summary>
    public static bool MatchesReference(
        IReadOnlyDictionary<string, string> live, IReadOnlyDictionary<string, string> reference)
    {
        if (live == null) throw new ArgumentNullException(nameof(live));
        if (reference == null) throw new ArgumentNullException(nameof(reference));
        if (live.Count == 0) return false;
        foreach (KeyValuePair<string, string> kvp in live)
        {
            if (!reference.TryGetValue(kvp.Key, out string? refValue)) return false;
            if (!string.Equals(refValue, kvp.Value, StringComparison.Ordinal)) return false;
        }
        return true;
    }
}
