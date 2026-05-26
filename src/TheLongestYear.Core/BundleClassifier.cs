using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace TheLongestYear.Core;

/// <summary>
/// Classifies a parsed vanilla bundle into one of the three <see cref="BundleKind"/>s and
/// returns a populated <see cref="BundleRequirement"/>. Pure (no Game1/SMAPI deps) so it can
/// be unit-tested with synthetic <see cref="ParsedBundle"/>s.
///
/// Decision order (the first match wins):
///   1. Name matches "(Spring|Summer|Fall|Winter) (Foraging|Crops)" → <see cref="BundleKind.Seasonal"/>.
///   2. Name has an entry in <paramref name="bundleQuotas"/> → <see cref="BundleKind.Percentage"/>.
///   3. X == Y (every slot must be filled) → <see cref="BundleKind.PerItem"/>.
///   4. Otherwise → returns null (caller should skip + warn).
///
/// Ingredients are normalized via <see cref="BundleParsing.NormalizeItemId"/> and de-duplicated
/// to match the donation-ledger model (one set entry per qualified id).
/// </summary>
public static class BundleClassifier
{
    private static readonly Regex SeasonalNamePattern = new Regex(
        @"^(?<season>Spring|Summer|Fall|Winter)\s+(?<kind>Foraging|Crops)$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>Classify one bundle. Returns null if the bundle name doesn't match any rule
    /// (i.e. an SVE-added or otherwise unknown bundle the caller should log and skip).</summary>
    /// <param name="parsed">Parsed vanilla bundle data (Data/Bundles entry).</param>
    /// <param name="theme">Bundle's room theme (from <see cref="RoomThemeMap"/>).</param>
    /// <param name="itemSeasonPins">Per-item season pins for KIND 2 bundles (merged
    /// defaults + user). Keyed by qualified item id.</param>
    /// <param name="bundleQuotas">Per-bundle cumulative quotas for KIND 3 bundles (merged
    /// defaults + user). Keyed by bundle name.</param>
    public static BundleRequirement? Classify(
        ParsedBundle parsed, Theme theme,
        IReadOnlyDictionary<string, Season> itemSeasonPins,
        IReadOnlyDictionary<string, int[]> bundleQuotas)
    {
        if (parsed == null) throw new ArgumentNullException(nameof(parsed));
        if (itemSeasonPins == null) throw new ArgumentNullException(nameof(itemSeasonPins));
        if (bundleQuotas == null) throw new ArgumentNullException(nameof(bundleQuotas));

        string name = parsed.Name ?? "";

        // Skip category-only bundles — no concrete ids to track.
        List<string> ingredients = CollectQualifiedIngredients(parsed);
        if (ingredients.Count == 0)
            return null;

        // KIND 1: Seasonal — bundle name like "Spring Foraging" / "Fall Crops".
        Match seasonalMatch = SeasonalNamePattern.Match(name);
        if (seasonalMatch.Success)
        {
            Season season = ParseSeason(seasonalMatch.Groups["season"].Value);
            return BundleRequirement.CreateSeasonal(name, theme, ingredients, season);
        }

        // KIND 3: Percentage — has a named quota override.
        if (bundleQuotas.TryGetValue(name, out int[]? quota) && quota != null)
        {
            // numberOfSlots = X (the parsed bundle's slot count), ingredients = Y (deduped list).
            // CreatePercentage validates X < Y and Y entries within [0..X].
            return BundleRequirement.CreatePercentage(
                name, theme, ingredients,
                numberOfSlots: parsed.NumberOfSlots,
                cumulativeRequiredBySeason: quota);
        }

        // KIND 2: PerItem — every slot must be filled (X == Y after dedup).
        if (parsed.NumberOfSlots == ingredients.Count)
        {
            // Pull pins for THIS bundle's ingredients only. Unpinned items don't gate any
            // season but still count toward IsFullyComplete.
            Dictionary<string, Season> pins = new();
            foreach (string id in ingredients)
                if (itemSeasonPins.TryGetValue(id, out Season s))
                    pins[id] = s;
            return BundleRequirement.CreatePerItem(name, theme, ingredients, pins);
        }

        // X < Y with no named quota — caller logs and skips.
        return null;
    }

    /// <summary>Distinct, qualified-id ingredient list (drops category refs).</summary>
    private static List<string> CollectQualifiedIngredients(ParsedBundle parsed)
    {
        List<string> result = new();
        HashSet<string> seen = new();
        foreach (BundleIngredient ing in parsed.Ingredients)
        {
            if (BundleParsing.IsCategoryRef(ing.ItemRef))
                continue;
            string id = BundleParsing.NormalizeItemId(ing.ItemRef);
            if (seen.Add(id))
                result.Add(id);
        }
        return result;
    }

    private static Season ParseSeason(string text) => text.ToLowerInvariant() switch
    {
        "spring" => Season.Spring,
        "summer" => Season.Summer,
        "fall"   => Season.Fall,
        "winter" => Season.Winter,
        _ => throw new ArgumentException($"Unknown season name '{text}'.", nameof(text))
    };
}
