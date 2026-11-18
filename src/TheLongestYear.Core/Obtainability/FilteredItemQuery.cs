using System;
using System.Collections.Generic;

namespace TheLongestYear.Core.Obtainability;

/// <summary>A data line that gives "some object" narrowed by a PerItemCondition: <c>RANDOM_ITEMS (O)</c> or
/// <c>ALL_ITEMS (O)</c> with a condition the game checks on each item (a context tag, a category). The game
/// lists such a line only through its own resolver, so the reader resolves it there and hands the model the
/// ids that pass. Read without the condition, the bare query is every object in the game: Sunberry Village's
/// artifact spots (ITEM_CONTEXT_TAG Target sbv_artifact) and garbage cans (ITEM_CATEGORY Target -20) gave
/// every item a week-1 luck route, Midnight Carp and Golden Sunberry included (2026-11-18).</summary>
public static class FilteredItemQuery
{
    private const string RandomItemsQuery = "RANDOM_ITEMS";
    private const string AllItemsQuery = "ALL_ITEMS";
    private const string ObjectQualifier = "(O)";

    /// <summary>True when the query names a set of objects the game lists in full once the condition is
    /// applied. Catalogue lines of other item types (furniture, wallpaper) are left as they were.</summary>
    public static bool IsFilteredSet(string? query, string? perItemCondition)
    {
        if (string.IsNullOrWhiteSpace(perItemCondition) || string.IsNullOrWhiteSpace(query)) return false;
        string[] tokens = query.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return tokens.Length >= 2 && (tokens[0] == RandomItemsQuery || tokens[0] == AllItemsQuery) && tokens[1] == ObjectQualifier;
    }

    /// <summary>What one spawn entry gives: itself, or for a filtered set the ids the game's resolver lists
    /// with the condition, each a random pick. A set the resolver could not list (null) gives nothing: falling
    /// back to the bare query would hand out every object again.</summary>
    /// <param name="resolve">The game's resolver: (query, condition) to qualified ids, or null on failure.</param>
    public static IEnumerable<(string Id, bool IsRandom)> Expand(string entry, bool isRandom, string? perItemCondition,
        Func<string, string, IReadOnlyList<string>?> resolve)
    {
        if (resolve == null) throw new ArgumentNullException(nameof(resolve));
        if (!IsFilteredSet(entry, perItemCondition))
        {
            yield return (entry, isRandom);
            yield break;
        }
        foreach (string id in resolve(entry, perItemCondition!) ?? (IReadOnlyList<string>)Array.Empty<string>())
            yield return (id, true);
    }
}
