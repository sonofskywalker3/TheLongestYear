using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace TheLongestYear.Core;

/// <summary>One bundle the banish picker offers: its base name (the banish key) and the name the
/// board shows.</summary>
public readonly record struct BanishBundleChoice(string Name, string DisplayName);

/// <summary>What the banish picker lists, read from THIS loop's board: its bundles on one tab and
/// the items they ask for on the other. Left out: the pass-through rooms (the Vault's gold and the
/// Missing Bundle are copied from the game untouched), the seasonal crops bundles (every board keeps
/// a crops bundle each season, Jeff 2026-09-07), money and category slots ("any fish" is not one item).</summary>
public static class BanishCandidates
{
    /// <summary>The " II", " III" suffix the engine adds when two picks share a name.</summary>
    private static readonly Regex UniquifySuffix = new(@" [IVXL]+$", RegexOptions.Compiled);

    /// <summary>A board name with the engine's duplicate suffix taken off, so a banish of "Spring
    /// Foraging" also covers a "Spring Foraging II".</summary>
    public static string BaseName(string name)
        => string.IsNullOrEmpty(name) ? name : UniquifySuffix.Replace(name, "");

    public static IReadOnlyList<BanishBundleChoice> Bundles(IReadOnlyDictionary<string, string> bundleData)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<BanishBundleChoice>();
        foreach (KeyValuePair<string, string> entry in Ordered(bundleData))
        {
            BundleSpec spec = BundleParsing.ToSpec(entry.Key, entry.Value);
            if (BanishResolver.PassThroughRooms.Contains(spec.Room)) continue;
            string name = BaseName(spec.Name);
            if (string.IsNullOrEmpty(name) || RemixSelector.IsAlwaysPicked(name)) continue;
            if (!seen.Add(name)) continue;
            result.Add(new BanishBundleChoice(name, BaseName(spec.DisplayName)));
        }
        return result;
    }

    /// <summary>Every distinct item the board asks for, qualified, in board order.</summary>
    public static IReadOnlyList<string> Items(IReadOnlyDictionary<string, string> bundleData)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<string>();
        foreach (KeyValuePair<string, string> entry in Ordered(bundleData))
        {
            BundleSpec spec = BundleParsing.ToSpec(entry.Key, entry.Value);
            if (BanishResolver.PassThroughRooms.Contains(spec.Room)) continue;
            foreach (BundleSlotSpec slot in spec.Slots)
            {
                string? id = BanishResolver.SlotItemId(slot.ItemId);
                if (id != null && seen.Add(id)) result.Add(id);
            }
        }
        return result;
    }

    /// <summary>Board order: by room, then by bundle index, so the list reads like the Community Center.</summary>
    private static IEnumerable<KeyValuePair<string, string>> Ordered(IReadOnlyDictionary<string, string> bundleData)
        => bundleData
            .OrderBy(kv => kv.Key.Split('/')[0], StringComparer.Ordinal)
            .ThenBy(kv => kv.Key.Split('/').Length > 1 && int.TryParse(kv.Key.Split('/')[1], out int i) ? i : 0);
}
