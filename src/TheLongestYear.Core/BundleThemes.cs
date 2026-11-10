using System;
using System.Collections.Generic;

namespace TheLongestYear.Core;

/// <summary>Bundles that ask for the same kind of thing. A board fields at most one bundle per
/// theme where an alternative exists (Jeff, 2026-11-10: a Chef's and a Home Cook's Feast on one
/// board is two cooking bundles). Names are the bundles' stable Data/Bundles names: vanilla's own
/// (Chef's, Artisan, Geologist's) and the authored ones (<see cref="AuthoredBundles"/>). The
/// seasonal Crops and Foraging bundles are deliberately absent: they never exclude each other.
/// <see cref="RemixSelector"/> and <see cref="RoomBundleCountPlanner"/> read this; rooms are
/// walked in a fixed order, so the board-wide set of used themes is deterministic.</summary>
public static class BundleThemes
{
    public const string Cooking = "Cooking";
    public const string ArtisanGoods = "Artisan goods";
    public const string Minerals = "Minerals";

    private static readonly IReadOnlyDictionary<string, string> ThemeByName =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Chef's"] = Cooking,
            ["Home Cook's Feast"] = Cooking,
            ["Artisan"] = ArtisanGoods,
            ["Preserver's"] = ArtisanGoods,
            ["Geologist's"] = Minerals,
            ["Mineral"] = Minerals,
            ["Rockhound's"] = Minerals,
        };

    /// <summary>The bundle's theme, or null for a bundle in no theme group.</summary>
    public static string? ThemeOf(string? bundleName)
        => bundleName != null && ThemeByName.TryGetValue(bundleName, out string? theme) ? theme : null;

    /// <summary>True when the bundle's theme is already in <paramref name="usedThemes"/>.</summary>
    public static bool IsTaken(string? bundleName, IReadOnlySet<string>? usedThemes)
        => usedThemes is { Count: > 0 } && ThemeOf(bundleName) is string theme && usedThemes.Contains(theme);

    /// <summary>Adds the bundle's theme (if any) to <paramref name="usedThemes"/>.</summary>
    public static void MarkUsed(ISet<string> usedThemes, string? bundleName)
    {
        if (ThemeOf(bundleName) is string theme)
            usedThemes.Add(theme);
    }

    /// <summary>The candidates whose theme is still free; all of them when every candidate's
    /// theme is taken (the same fallback as the name dedup). Returns the input list itself when
    /// nothing is filtered out, so a board without a theme clash rolls exactly as before.</summary>
    public static IReadOnlyList<BundleSpec> WithoutTakenThemes(
        IReadOnlyList<BundleSpec> candidates, IReadOnlySet<string>? usedThemes)
    {
        if (usedThemes is not { Count: > 0 })
            return candidates;
        List<BundleSpec>? free = null;
        bool anyTaken = false;
        foreach (BundleSpec candidate in candidates)
        {
            if (IsTaken(candidate.Name, usedThemes))
            {
                anyTaken = true;
                continue;
            }
            (free ??= new List<BundleSpec>()).Add(candidate);
        }
        if (!anyTaken)
            return candidates;
        return free is { Count: > 0 } ? free : candidates;
    }
}
