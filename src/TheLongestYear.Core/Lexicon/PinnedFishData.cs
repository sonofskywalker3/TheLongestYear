using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core.Lexicon;

/// <summary>Data/Fish rows a supported mod's layer pins (<see cref="LayerInputs.FishData"/>).
///
/// A mod can rewrite a fish's Data/Fish row during the year (East Scarp's four nudibranchs are a rod
/// fish in their own season and a crab pot row in the other three, swapped by Content Patcher each
/// season). The answers are kept for one reading of the data, so without a pin they would match the
/// game only in the season they were written in. A pinned fish is read as the layer's row whatever
/// the game holds today, and the context tags the game derives from the live row are left out, since
/// they change with it.</summary>
public static class PinnedFishData
{
    /// <summary>Tag families ItemContextTagManager derives from a fish's Data/Fish row.</summary>
    private static readonly string[] RowTagPrefixes =
    {
        "fish_trap_location_", "fish_motion_", "fish_difficulty_", "fish_favor_weather_",
    };

    /// <summary>The live rows with every pinned fish the game knows read as its pinned row. A pin for
    /// a fish the game does not hold adds nothing. The live dictionary is never changed.</summary>
    public static Dictionary<string, string> Apply(IReadOnlyDictionary<string, string> live, IReadOnlyDictionary<string, string> pins)
    {
        if (live == null) throw new ArgumentNullException(nameof(live));
        var read = new Dictionary<string, string>(live.Count);
        foreach (var (key, row) in live)
            read[key] = pins != null && pins.TryGetValue(key, out string? pinned) ? pinned : row;
        return read;
    }

    /// <summary>An object's context tags without the ones derived from its live Data/Fish row, when its fish is pinned.</summary>
    public static List<string> Tags(string fishKey, IEnumerable<string> tags, IReadOnlyDictionary<string, string> pins)
    {
        List<string> all = tags?.ToList() ?? new List<string>();
        if (pins == null || !pins.ContainsKey(fishKey)) return all;
        return all.Where(tag => !RowTagPrefixes.Any(prefix => tag.StartsWith(prefix, StringComparison.Ordinal))).ToList();
    }
}
