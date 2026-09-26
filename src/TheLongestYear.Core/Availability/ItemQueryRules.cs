using System;
using System.Collections.Generic;

namespace TheLongestYear.Core.Availability;

/// <summary>How to read one ItemId field from Data/Shops or Data/Locations.</summary>
public enum ShopItemQueryKind
{
    /// <summary>A plain object id, read as-is.</summary>
    PlainId,
    /// <summary>An item query that names a fixed set of objects (every match is on offer every
    /// time), so the game layer resolves it and reads each result as its own line.</summary>
    Resolve,
    /// <summary>Not a dependable object source: another item type, a random pick from a set, or
    /// a query whose results depend on game state. Contributes nothing, exactly as before.</summary>
    Skip,
}

/// <summary>Mods write shop stock and forage as item QUERIES as often as plain ids: Cornucopia
/// sells its seeds with <c>ALL_ITEMS (O)</c> plus a PerItemCondition on context tags. TLY used to
/// read only plain ids, so every item such a line offered looked source-less (Nexus post Thrippa,
/// 2026-09-25). Jeff, 2026-09-26: read whatever mods write, for every modded item.
///
/// Only queries that name a FIXED set are read. <c>RANDOM_ITEMS</c> and anything flagged
/// <c>@isRandomSale</c> are a lottery (the Traveling Cart's one random seed out of hundreds), so
/// they prove nothing; vanilla's own cart stock was already invisible for the same reason.</summary>
public static class ItemQueryRules
{
    private const string ObjectPrefix = "(O)";
    private const string RandomSaleFlag = "@isRandomSale";

    /// <summary>Query keys whose results are the same every time they are read.</summary>
    private static readonly IReadOnlySet<string> FixedSetKeys =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "ALL_ITEMS", "FLAVORED_ITEM" };

    /// <summary>Vanilla's registered query keys (ItemQueryResolver.DefaultResolvers). The game
    /// layer passes the live registry instead, which also holds keys other mods register.</summary>
    public static readonly IReadOnlySet<string> VanillaQueryKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "ALL_ITEMS", "DISH_OF_THE_DAY", "FLAVORED_ITEM", "ITEMS_LOST_ON_DEATH", "ITEMS_SOLD_BY_PLAYER",
        "LOCATION_FISH", "LOST_BOOK_OR_ITEM", "MONSTER_SLAYER_REWARDS", "MOVIE_CONCESSIONS_FOR_GUEST",
        "RANDOM_ARTIFACT_FOR_DIG_SPOT", "RANDOM_BASE_SEASON_ITEM", "RANDOM_ITEMS", "SECRET_NOTE_OR_ITEM",
        "SHOP_TOWN_KEY", "TOOL_UPGRADES", "PET_ADOPTION",
    };

    /// <param name="isQueryKey">Whether a word is a registered item query key; null uses
    /// <see cref="VanillaQueryKeys"/>.</param>
    public static ShopItemQueryKind Classify(string? itemId, Func<string, bool>? isQueryKey = null)
    {
        if (string.IsNullOrWhiteSpace(itemId)) return ShopItemQueryKind.Skip;
        string text = itemId.Trim();
        isQueryKey ??= VanillaQueryKeys.Contains;

        int space = text.IndexOf(' ');
        string key = space < 0 ? text : text.Substring(0, space);
        if (!isQueryKey(key))
        {
            // Game order: not a query key, so the whole string is an item id.
            bool isObject = !text.StartsWith("(", StringComparison.Ordinal)
                            || text.StartsWith(ObjectPrefix, StringComparison.Ordinal);
            return isObject && space < 0 ? ShopItemQueryKind.PlainId : ShopItemQueryKind.Skip;
        }

        if (!FixedSetKeys.Contains(key)) return ShopItemQueryKind.Skip;
        if (text.IndexOf(RandomSaleFlag, StringComparison.OrdinalIgnoreCase) >= 0) return ShopItemQueryKind.Skip;

        // ALL_ITEMS takes an optional type id first; any type but objects offers nothing a bundle
        // can ask for, and resolving it would walk every furniture item for no reason.
        if (key.Equals("ALL_ITEMS", StringComparison.OrdinalIgnoreCase) && space >= 0)
        {
            string firstArg = text.Substring(space + 1).TrimStart().Split(' ')[0];
            if (!firstArg.StartsWith("@", StringComparison.Ordinal) && !firstArg.Equals(ObjectPrefix, StringComparison.Ordinal))
                return ShopItemQueryKind.Skip;
        }
        return ShopItemQueryKind.Resolve;
    }
}
