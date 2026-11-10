using System;
using System.Collections.Generic;

namespace TheLongestYear.Core;

/// <summary>
/// Pinned keeps (the shrine's "sorted on top of each tab"): a pin belongs to a whole chain, stored
/// as the chain's root id in <see cref="MetaState.PinnedUpgrades"/>, so it follows the chain from
/// tier to tier and still marks the owned leaf once the chain is fully kept. Pure, so both shrine
/// menus share the same rules.
/// </summary>
public static class PinnedKeeps
{
    /// <summary>Longest prerequisite walk before giving up (guards a malformed cycle).</summary>
    private const int MaxChainLength = 64;

    /// <summary>The first tier of <paramref name="id"/>'s chain: follow prerequisites back while
    /// they name a catalog entry. A keep with no prerequisite is its own root.</summary>
    public static string ChainRoot(string id)
    {
        string current = id;
        for (int step = 0; step < MaxChainLength; step++)
        {
            string? prerequisite = UpgradeCatalog.TryGet(current)?.PrerequisiteId;
            if (prerequisite == null || UpgradeCatalog.TryGet(prerequisite) == null)
                return current;
            current = prerequisite;
        }
        return current;
    }

    public static bool IsPinned(MetaState state, string? id)
        => id != null && state.PinnedUpgrades.Contains(ChainRoot(id));

    /// <summary>Pins <paramref name="id"/>'s chain, or unpins it when already pinned. Returns
    /// true when the chain is pinned afterwards.</summary>
    public static bool Toggle(MetaState state, string id)
    {
        string root = ChainRoot(id);
        if (state.PinnedUpgrades.Remove(root))
            return false;
        state.PinnedUpgrades.Add(root);
        return true;
    }

    /// <summary><paramref name="rows"/> with the pinned ones first, each group in its existing
    /// order. <paramref name="idOf"/> gives a row's keep id, or null for a row that is not a keep
    /// (it is never pinned).</summary>
    public static List<T> PinnedFirst<T>(IEnumerable<T> rows, Func<T, string?> idOf, MetaState state)
    {
        var pinned = new List<T>();
        var rest = new List<T>();
        foreach (T row in rows)
            (IsPinned(state, idOf(row)) ? pinned : rest).Add(row);
        pinned.AddRange(rest);
        return pinned;
    }
}
