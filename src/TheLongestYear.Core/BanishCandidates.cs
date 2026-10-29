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

    /// <summary>Bundle names that are the only candidate at some room position. The engine has to
    /// put such a bundle on every board (a position is never left empty), so a banish of it could
    /// never be honoured and the pick leaves it out. <paramref name="roomPositions"/> is every room's
    /// positions, each the candidate names that position can draw (authored bundles included).</summary>
    public static IReadOnlySet<string> SoleCandidates(
        IReadOnlyDictionary<string, IReadOnlyList<IReadOnlyList<string>>> roomPositions)
    {
        var sole = new HashSet<string>(StringComparer.Ordinal);
        foreach (KeyValuePair<string, IReadOnlyList<IReadOnlyList<string>>> room in roomPositions)
        {
            if (BanishResolver.PassThroughRooms.Contains(room.Key)) continue;
            foreach (IReadOnlyList<string> candidates in room.Value)
            {
                List<string> distinct = candidates.Select(BaseName).Distinct(StringComparer.Ordinal).ToList();
                if (distinct.Count == 1) sole.Add(distinct[0]);
            }
        }
        return sole;
    }

    /// <param name="cannotBanish">Names to leave out (see <see cref="SoleCandidates"/>); null leaves none out.</param>
    public static IReadOnlyList<BanishBundleChoice> Bundles(
        IReadOnlyDictionary<string, string> bundleData, IReadOnlySet<string>? cannotBanish = null)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<BanishBundleChoice>();
        foreach (KeyValuePair<string, string> entry in Ordered(bundleData))
        {
            BundleSpec spec = BundleParsing.ToSpec(entry.Key, entry.Value);
            if (BanishResolver.PassThroughRooms.Contains(spec.Room)) continue;
            string name = BaseName(spec.Name);
            if (string.IsNullOrEmpty(name) || RemixSelector.IsAlwaysPicked(name)) continue;
            if (cannotBanish != null && cannotBanish.Contains(name)) continue;
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
