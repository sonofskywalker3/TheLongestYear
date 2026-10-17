using System;
using System.Collections.Generic;

namespace TheLongestYear.Core;

/// <summary>The highest upgrade level per tool kind out of every tool the player owns, wherever
/// it sits: the backpack, the tool Clint is upgrading, or a chest. The glue layer walks the
/// world and reports one (slug, level) sighting per tool instance; this merges them. A leftover
/// basic tool in the bag or a chest can never mask the upgraded one, so a "Keep &lt;tool&gt;"
/// offer and the cap on a kept tier both read the player's real best tool.</summary>
public static class ToolTierPeaks
{
    /// <summary>The fishing rod's "no rod owned" level, distinct from a Bamboo Pole (0).</summary>
    public const int NoRod = -1;

    /// <summary>Slug -> highest level seen. A slug with no sighting is absent.</summary>
    public static IReadOnlyDictionary<string, int> Highest(IEnumerable<(string Slug, int Level)> sightings)
    {
        var peaks = new Dictionary<string, int>(StringComparer.Ordinal);
        if (sightings == null) return peaks;
        foreach ((string slug, int level) in sightings)
        {
            if (string.IsNullOrEmpty(slug)) continue;
            if (!peaks.TryGetValue(slug, out int known) || level > known)
                peaks[slug] = level;
        }
        return peaks;
    }

    /// <summary>The peak for a slug, or <paramref name="missing"/> when no tool of that kind was seen.</summary>
    public static int Level(IReadOnlyDictionary<string, int> peaks, string slug, int missing = 0)
        => peaks != null && slug != null && peaks.TryGetValue(slug, out int level) ? level : missing;
}
