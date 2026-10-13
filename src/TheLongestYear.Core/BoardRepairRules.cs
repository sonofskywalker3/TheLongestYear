using System;

namespace TheLongestYear.Core;

/// <summary>Which open asks the load-time board repair swaps out. Two kinds: an item the
/// reachability read condemns for this run, and an item on <see cref="ItemPoolBuilder.BuiltInExcludedItemIds"/>,
/// which no one-year loop can ever reach whatever the data says (Ginger Island fish and crops,
/// post-CC content). The built-in list used to be checked only when a pool was built, so a board
/// rolled before an id joined it, or under data that vouched for it, kept the ask for good (Nexus
/// bug report 2026-10-12, iroeny: Weatherman's asked for a Lionfish). It is also the only rule
/// left when the reachability read failed and handed the repair nothing.</summary>
public static class BoardRepairRules
{
    /// <summary>True when the repair must replace this ask. <paramref name="isUnreachable"/> is the
    /// reachability verdict, or null when that read failed (the built-in list still applies).</summary>
    public static bool MustReplace(string itemId, Func<string, bool>? isUnreachable)
    {
        if (string.IsNullOrEmpty(itemId)) return false;
        string id = BundleParsing.NormalizeItemId(itemId);
        return ItemPoolBuilder.BuiltInExcludedItemIds.Contains(id) || (isUnreachable?.Invoke(id) ?? false);
    }

    /// <summary>The log reason for a built-in ban, or null when the id is not on the list.</summary>
    public static string? BannedReason(string itemId)
        => !string.IsNullOrEmpty(itemId) && ItemPoolBuilder.BuiltInExcludedItemIds.Contains(BundleParsing.NormalizeItemId(itemId))
            ? "a one-year loop never reaches it (built-in exclusion)"
            : null;
}
