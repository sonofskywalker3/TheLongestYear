using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core.Availability;

/// <summary>Turns the item id or item query strings the game data uses (artifact spots, machine
/// outputs) into concrete qualified object ids. Only the query shapes the effort rules need are
/// understood; anything else expands to nothing rather than guessing.</summary>
public static class ItemQueryIds
{
    private const string RandomItemsQuery = "RANDOM_ITEMS";
    private const string FlavoredItemQuery = "FLAVORED_ITEM";
    private const string ObjectQualifier = "(O)";
    private const int MaxRangeSize = 500;

    private static readonly IReadOnlyDictionary<string, string> FlavoredBaseIds =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Wine"] = "(O)348", ["Juice"] = "(O)350", ["Jelly"] = "(O)344", ["Pickles"] = "(O)342",
            ["Roe"] = "(O)812", ["AgedRoe"] = "(O)447", ["Honey"] = "(O)340",
            ["DriedFruit"] = "(O)DriedFruit", ["DriedMushrooms"] = "(O)DriedMushrooms",
            ["SmokedFish"] = "(O)SmokedFish", ["Bait"] = "(O)SpecificBait",
        };

    public static IReadOnlyList<string> Expand(string? itemIdOrQuery)
    {
        string text = (itemIdOrQuery ?? "").Trim();
        if (text.Length == 0) return Array.Empty<string>();

        string[] tokens = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (tokens[0] == RandomItemsQuery)
        {
            if (tokens.Length >= 4 && tokens[1] == ObjectQualifier
                && int.TryParse(tokens[2], out int low) && int.TryParse(tokens[3], out int high)
                && high >= low && high - low < MaxRangeSize)
                return Enumerable.Range(low, high - low + 1).Select(n => $"{ObjectQualifier}{n}").ToList();
            return Array.Empty<string>();
        }
        if (tokens[0] == FlavoredItemQuery)
            return tokens.Length >= 2 && FlavoredBaseIds.TryGetValue(tokens[1], out string? id)
                ? new[] { id }
                : Array.Empty<string>();
        if (tokens.Length > 1) return Array.Empty<string>();
        if (text.StartsWith("(", StringComparison.Ordinal))
            return text.StartsWith(ObjectQualifier, StringComparison.Ordinal) ? new[] { text } : Array.Empty<string>();
        return new[] { BundleParsing.NormalizeItemId(text) };
    }
}
