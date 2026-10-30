using System;
using System.Collections.Generic;

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

    /// <summary>The gold star, the ask every Quality Crops slot carries.</summary>
    public const int QualityGold = 2;

    /// <summary>The log reason for an ask the crop-bundle rule swaps out.</summary>
    public const string NotACropReason = "not a crop (left out of the crop pool)";

    /// <summary>True when a bundle is a named crop bundle: Quality Crops, or a season's Crops
    /// bundle (the classifier's name fast-paths). A bundle claimed as crops only by majority is
    /// left out on purpose: its odd slot may be a real non-crop ask.</summary>
    public static bool IsNamedCropBundle(DomainMatch match)
        => match.Domain == PoolDomain.QualityCrops
           || (match.Domain == PoolDomain.SeasonalCrops && match.Season != null);

    /// <summary>True when an open ask in a named crop bundle is not in the crop pool, so the
    /// repair must replace it. Boards rolled before the crop pool kept only produce asked for
    /// Fiber (Fiber Seeds harvest it) or a wild seed's placeholder forage item as a crop (player
    /// report: Fiber in Quality Crops). Fiber is NOT banned outright: Construction and Forest
    /// bundles ask for it by right.</summary>
    public static bool MustReplaceInCropBundle(DomainMatch? match, string itemId, IReadOnlySet<string>? cropPoolIds)
    {
        if (string.IsNullOrEmpty(itemId) || match == null || cropPoolIds == null) return false;
        if (!IsNamedCropBundle(match)) return false;
        return !cropPoolIds.Contains(BundleParsing.NormalizeItemId(itemId));
    }

    /// <summary>The quality a replacement ask takes. In Quality Crops it is gold whenever the new
    /// item can carry a star, so a repaired slot never becomes a plain ask in a gold-crop bundle;
    /// elsewhere it is <paramref name="keptQuality"/>, the old slot's quality as the repair kept it.</summary>
    public static int ReplacementQuality(DomainMatch? match, bool canCarryQuality, int keptQuality)
        => match != null && match.Domain == PoolDomain.QualityCrops && canCarryQuality
            ? QualityGold
            : keptQuality;

    /// <summary>The log reason for a built-in ban, or null when the id is not on the list.</summary>
    public static string? BannedReason(string itemId)
        => !string.IsNullOrEmpty(itemId) && ItemPoolBuilder.BuiltInExcludedItemIds.Contains(BundleParsing.NormalizeItemId(itemId))
            ? "a one-year loop never reaches it (built-in exclusion)"
            : null;
}
