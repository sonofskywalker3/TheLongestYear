using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core.Sabotage;

/// <summary>Rewrites one ingredient triple ("id stack quality") in a vanilla BundleData value,
/// leaving every other field byte-for-byte as it was. The write-side counterpart of
/// <see cref="BundleParsing.ParseIngredients"/> for a single slot; the rest of the value
/// (name, reward, colour, slot count, display name) passes through untouched so the manifest
/// check still recognises the board after the stored copy is updated the same way.</summary>
public static class BundleDataTamper
{
    private const int IngredientField = 2;

    /// <summary>The value with slot <paramref name="ingredientIndex"/> replaced. Null when the
    /// value has no ingredient field or the index is out of range.</summary>
    public static string? Rewrite(string value, int ingredientIndex, string newItemRef, int stack, int quality)
    {
        if (value is null) throw new ArgumentNullException(nameof(value));
        if (string.IsNullOrWhiteSpace(newItemRef)) throw new ArgumentException("Item ref must be non-empty.", nameof(newItemRef));
        string[] fields = value.Split('/');
        if (fields.Length <= IngredientField) return null;
        List<string> parts = fields[IngredientField]
            .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
            .ToList();
        int triples = parts.Count / 3;
        if (ingredientIndex < 0 || ingredientIndex >= triples) return null;
        int at = ingredientIndex * 3;
        parts[at] = newItemRef;
        parts[at + 1] = stack.ToString();
        parts[at + 2] = quality.ToString();
        fields[IngredientField] = string.Join(" ", parts);
        return string.Join("/", fields);
    }

    /// <summary>The board with one entry rewritten; the input is not modified. Null when the key
    /// is missing or the slot is out of range.</summary>
    public static Dictionary<string, string>? Apply(
        IReadOnlyDictionary<string, string> board, string key, int ingredientIndex, string newItemRef, int stack, int quality)
    {
        if (board is null) throw new ArgumentNullException(nameof(board));
        if (key is null) throw new ArgumentNullException(nameof(key));
        if (!board.TryGetValue(key, out string? value)) return null;
        string? rewritten = Rewrite(value, ingredientIndex, newItemRef, stack, quality);
        if (rewritten is null) return null;
        var copy = new Dictionary<string, string>(board.Count, StringComparer.Ordinal);
        foreach (KeyValuePair<string, string> kv in board) copy[kv.Key] = kv.Value;
        copy[key] = rewritten;
        return copy;
    }

    /// <summary>The board with every one of <paramref name="ingredientIndices"/> in one entry
    /// rewritten to the same item, stack and quality (a target asked in two slots of its bundle,
    /// Jeff 2026-10-07); the input is not modified. All or nothing: null when the key is missing,
    /// no slot is given, or any slot is out of range.</summary>
    public static Dictionary<string, string>? ApplyAll(
        IReadOnlyDictionary<string, string> board, string key, IReadOnlyList<int> ingredientIndices, string newItemRef, int stack, int quality)
    {
        if (board is null) throw new ArgumentNullException(nameof(board));
        if (key is null) throw new ArgumentNullException(nameof(key));
        if (ingredientIndices is null || ingredientIndices.Count == 0) return null;
        if (!board.TryGetValue(key, out string? value)) return null;
        string? rewritten = value;
        foreach (int index in ingredientIndices)
        {
            rewritten = Rewrite(rewritten, index, newItemRef, stack, quality);
            if (rewritten is null) return null;
        }
        var copy = new Dictionary<string, string>(board.Count, StringComparer.Ordinal);
        foreach (KeyValuePair<string, string> kv in board) copy[kv.Key] = kv.Value;
        copy[key] = rewritten;
        return copy;
    }

    /// <summary>The BundleData key ("Room/index") whose parsed index matches, or null.</summary>
    public static string? KeyForIndex(IReadOnlyDictionary<string, string> board, int bundleIndex)
    {
        if (board is null) throw new ArgumentNullException(nameof(board));
        foreach (KeyValuePair<string, string> kv in board)
            if (BundleParsing.Parse(kv.Key, kv.Value).Index == bundleIndex)
                return kv.Key;
        return null;
    }
    private const int NameField = 0;
    private const int DisplayNameField = 6;

    /// <summary>How many slot <paramref name="ingredientIndex"/> asks for, or 0 when the value has
    /// no such slot.</summary>
    public static int StackAt(string value, int ingredientIndex)
    {
        if (value is null) return 0;
        string[] fields = value.Split('/');
        if (fields.Length <= IngredientField) return 0;
        string[] parts = fields[IngredientField].Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        int at = ingredientIndex * 3 + 1;
        if (ingredientIndex < 0 || at >= parts.Length) return 0;
        return int.TryParse(parts[at], out int stack) ? stack : 0;
    }

    /// <summary>The bundle's name as the bundle menu shows it: the display name field when the
    /// value has one, else the internal name (vanilla's <c>Bundle</c> reads field 6 as its label).</summary>
    public static string LabelOf(string value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        string[] fields = value.Split('/');
        if (fields.Length > DisplayNameField && !string.IsNullOrWhiteSpace(fields[DisplayNameField])) return fields[DisplayNameField];
        return fields[NameField];
    }
}
