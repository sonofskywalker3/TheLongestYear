using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core.Obtainability;

public sealed record QueryResult(IReadOnlyList<string> ItemIds, bool Chance, bool Unresolved, string Note);

/// <summary>Turns an item id or an item query (ItemQueryResolver.cs) into concrete item ids.</summary>
public static class ItemQueries
{
    public const string UnresolvedPrefix = "?";
    private const string RandomItems = "RANDOM_ITEMS";
    private const string FlavoredItem = "FLAVORED_ITEM";
    private const string LostBookOrItem = "LOST_BOOK_OR_ITEM";
    private const string SecretNoteOrItem = "SECRET_NOTE_OR_ITEM";
    private const string RandomSaleFlag = "@isRandomSale";
    private const string RequirePriceFlag = "@requirePrice";
    private const string LostBookId = "(O)102";

    /// <summary>Every query key ItemQueryResolver defines (ItemQueryResolver.cs 29-573).</summary>
    private static readonly HashSet<string> QueryKeys = new(StringComparer.Ordinal)
    {
        "ALL_ITEMS", "DISH_OF_THE_DAY", FlavoredItem, "ITEMS_LOST_ON_DEATH", "ITEMS_SOLD_BY_PLAYER",
        "LOCATION_FISH", LostBookOrItem, "MONSTER_SLAYER_REWARDS", "MOVIE_CONCESSIONS_FOR_GUEST",
        "RANDOM_ARTIFACT_FOR_DIG_SPOT", "RANDOM_BASE_SEASON_ITEM", RandomItems, SecretNoteOrItem,
        "SHOP_TOWN_KEY", "TOOL_UPGRADES", "PET_ADOPTION",
    };

    /// <summary>Preserve type to the object it creates (ObjectDataDefinition.cs 121-368).</summary>
    private static readonly IReadOnlyDictionary<string, string> FlavoredBases = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["AgedRoe"] = "(O)447", ["Honey"] = "(O)340", ["Jelly"] = "(O)344", ["Juice"] = "(O)350",
        ["Pickle"] = "(O)342", ["Roe"] = "(O)812", ["Wine"] = "(O)348", ["Bait"] = "(O)SpecificBait",
        ["DriedFruit"] = "(O)DriedFruit", ["DriedMushroom"] = "(O)DriedMushrooms", ["SmokedFish"] = "(O)SmokedFish",
    };

    /// <summary>A known query key, or any text with arguments: item ids never contain spaces, so a
    /// mod's own query ("MYMOD_ITEM 3") is a query this model does not know, never a fake id.</summary>
    public static bool IsQuery(string itemIdOrQuery)
    {
        string text = itemIdOrQuery.Trim();
        return QueryKeys.Contains(text.Split(' ', 2)[0]) || text.Contains(' ');
    }

    public static string? FlavoredBaseId(string preserveType)
        => FlavoredBases.TryGetValue(preserveType, out string? id) ? id : null;

    public static QueryResult Resolve(string itemIdOrQuery, IReadOnlyDictionary<string, ObjInfo> objects)
    {
        string text = itemIdOrQuery.Trim();
        if (!IsQuery(text))
            return new QueryResult(new[] { BundleParsing.NormalizeItemId(text) }, false, false, "");

        string[] parts = text.Split(' ', 2);
        string key = parts[0];
        string args = parts.Length > 1 ? parts[1] : "";
        switch (key)
        {
            case RandomItems:
                return new QueryResult(ExpandRandomItems(text, objects).ToList(), true, false, "random items");
            case FlavoredItem:
            {
                string type = args.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
                return FlavoredBaseId(type) is string id
                    ? new QueryResult(new[] { id }, false, false, $"flavored {type}")
                    : new QueryResult(Array.Empty<string>(), false, true, $"unknown flavor type {type}");
            }
            case LostBookOrItem:
            {
                QueryResult alt = args.Length > 0 ? Resolve(args, objects) : Empty("");
                return new QueryResult(new[] { LostBookId }.Concat(alt.ItemIds).ToList(), true, alt.Unresolved, "lost book while any are left, then " + args);
            }
            case SecretNoteOrItem:
            {
                QueryResult alt = args.Length > 0 ? Resolve(args, objects) : Empty("");
                return new QueryResult(alt.ItemIds, alt.Chance, alt.Unresolved, "secret note when unlocked, then " + args);
            }
            default:
                return Empty($"unsupported item query {text}", unresolved: true);
        }
    }

    /// <summary>"RANDOM_ITEMS (O) [min max] [@flags]": numeric object ids in the range, with
    /// @isRandomSale dropping ExcludeFromRandomSale items and @requirePrice dropping unpriced ones
    /// (ItemQueryResolver.cs 420-489, 623-641). A ranged query drops non-numeric ids.</summary>
    public static IEnumerable<string> ExpandRandomItems(string query, IReadOnlyDictionary<string, ObjInfo> objects)
    {
        string[] tokens = query.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        string qualifier = tokens.Skip(1).FirstOrDefault(t => t.StartsWith("(", StringComparison.Ordinal)) ?? "(O)";
        int[] numbers = tokens.Skip(1).Select(t => int.TryParse(t, out int n) ? n : (int?)null)
            .Where(n => n != null).Select(n => n!.Value).ToArray();
        bool ranged = numbers.Length >= 2;
        bool randomSale = tokens.Contains(RandomSaleFlag);
        bool requirePrice = tokens.Contains(RequirePriceFlag);

        foreach (ObjInfo obj in objects.Values.OrderBy(o => o.QualifiedId, StringComparer.Ordinal))
        {
            if (!obj.QualifiedId.StartsWith(qualifier, StringComparison.Ordinal)) continue;
            bool numeric = int.TryParse(obj.QualifiedId.Substring(qualifier.Length), out int id);
            if (ranged && (!numeric || id < numbers[0] || id > numbers[1])) continue;
            if (randomSale && obj.ExcludeFromRandomSale) continue;
            if (requirePrice && obj.Price <= 0) continue;
            yield return obj.QualifiedId;
        }
    }

    /// <summary>One source per resolved id, copying <paramref name="template"/>; a random query makes
    /// the sources chance. An unresolved query becomes one <see cref="SourceKind.Other"/> source under
    /// an id starting with <see cref="UnresolvedPrefix"/>, which the builder moves to diagnostics.</summary>
    public static IEnumerable<(string ItemId, ObtainSource Source)> Emit(
        string itemIdOrQuery, IReadOnlyDictionary<string, ObjInfo> objects, ObtainSource template)
    {
        QueryResult result = Resolve(itemIdOrQuery, objects);
        if (result.Unresolved && result.ItemIds.Count == 0)
        {
            yield return (UnresolvedPrefix + itemIdOrQuery, template with
            {
                Kind = SourceKind.Other,
                Conditions = template.Conditions with { Unresolved = true },
                Detail = $"{template.Detail}: {result.Note}",
            });
            yield break;
        }
        foreach (string id in result.ItemIds)
            yield return (id, template with
            {
                Reliability = result.Chance ? Reliability.Chance : template.Reliability,
                Conditions = result.Unresolved ? template.Conditions with { Unresolved = true } : template.Conditions,
                Detail = result.Note.Length == 0 ? template.Detail : $"{template.Detail} ({result.Note})",
            });
    }

    private static QueryResult Empty(string note, bool unresolved = false)
        => new(Array.Empty<string>(), false, unresolved, note);
}
