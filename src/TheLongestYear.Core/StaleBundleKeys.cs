using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core;

/// <summary>Which live bundle keys a TLY Custom board no longer has (spec
/// 2026-10-09-bundle-count-dial). <c>SetBundleData</c> never removes a key, and the game re-adds
/// <c>Data/Bundles</c>' default keys on every load, so a board with fewer bundles than before
/// would keep its dropped bags on the room page and the room could never complete.
///
/// Only rooms the board writes are looked at: another mod's room is never touched.</summary>
public static class StaleBundleKeys
{
    public sealed record Result(IReadOnlyList<string> Keys, IReadOnlyList<int> Indices);

    /// <summary>Live keys in a room <paramref name="board"/> writes that the board does not contain,
    /// and their indices (whose completion entries are cleared, so the write rebuilds them at the
    /// right size; an index the board still uses under another key is listed too).</summary>
    public static Result Find(IReadOnlyDictionary<string, string> board, IEnumerable<string> liveKeys)
    {
        if (board == null) throw new ArgumentNullException(nameof(board));
        var rooms = new HashSet<string>(
            board.Keys.Select(k => TryParse(k, out string room, out _) ? room : null).Where(r => r != null)!,
            StringComparer.Ordinal);

        var keys = new List<string>();
        var indices = new SortedSet<int>();
        foreach (string key in liveKeys ?? Array.Empty<string>())
        {
            if (board.ContainsKey(key) || !TryParse(key, out string room, out int index) || !rooms.Contains(room))
                continue;
            keys.Add(key);
            indices.Add(index);
        }
        keys.Sort(StringComparer.Ordinal);
        return new Result(keys, indices.ToList());
    }

    /// <summary>Bundle index to room name, for rebuilding the Community Center's own lookup after a
    /// write. A malformed key is skipped; a duplicated index keeps its first room.</summary>
    public static IReadOnlyDictionary<int, string> IndexToRoom(IEnumerable<string> keys)
    {
        var map = new Dictionary<int, string>();
        foreach (string key in keys ?? Array.Empty<string>())
            if (TryParse(key, out string room, out int index) && !map.ContainsKey(index))
                map[index] = room;
        return map;
    }

    private static bool TryParse(string key, out string room, out int index)
    {
        room = "";
        index = 0;
        if (string.IsNullOrEmpty(key))
            return false;
        int slash = key.IndexOf('/');
        if (slash <= 0 || !int.TryParse(key.Substring(slash + 1), out index))
            return false;
        room = key.Substring(0, slash);
        return true;
    }
}
