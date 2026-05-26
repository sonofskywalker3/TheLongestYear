using System;
using System.Collections.Generic;

namespace TheLongestYear.Core;

/// <summary>One ingredient requirement: an item id or category ref, a stack, and a min quality.</summary>
public readonly record struct BundleIngredient(string ItemRef, int Stack, int Quality);

/// <summary>A parsed Data/Bundles entry.</summary>
public sealed class ParsedBundle
{
    public string Room { get; }
    public int Index { get; }
    public string Name { get; }
    public IReadOnlyList<BundleIngredient> Ingredients { get; }
    public int NumberOfSlots { get; }

    public ParsedBundle(string room, int index, string name, IReadOnlyList<BundleIngredient> ingredients, int numberOfSlots)
    {
        Room = room;
        Index = index;
        Name = name;
        Ingredients = ingredients;
        NumberOfSlots = numberOfSlots;
    }
}

/// <summary>
/// Pure parsing of the vanilla Data/Bundles format. Key: "Room/index". Value (slash-delimited):
/// name / reward / ingredients(space-separated id-stack-quality triples) / color / numberOfSlots / sprite / displayName.
/// </summary>
public static class BundleParsing
{
    public static ParsedBundle Parse(string key, string value)
    {
        if (key is null) throw new ArgumentNullException(nameof(key));
        if (value is null) throw new ArgumentNullException(nameof(value));

        string[] keyParts = key.Split('/');
        string room = keyParts[0];
        int index = keyParts.Length > 1 && int.TryParse(keyParts[1], out int i) ? i : 0;

        string[] fields = value.Split('/');
        string name = fields.Length > 0 ? fields[0] : "";
        IReadOnlyList<BundleIngredient> ingredients = ParseIngredients(fields.Length > 2 ? fields[2] : "");

        int slots = ingredients.Count;
        if (fields.Length > 4 && int.TryParse(fields[4], out int parsedSlots))
            slots = parsedSlots;

        return new ParsedBundle(room, index, name, ingredients, slots);
    }

    public static IReadOnlyList<BundleIngredient> ParseIngredients(string ingredientField)
    {
        List<BundleIngredient> result = new List<BundleIngredient>();
        if (string.IsNullOrWhiteSpace(ingredientField))
            return result;

        string[] parts = ingredientField.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i + 2 < parts.Length; i += 3)
        {
            int stack = int.TryParse(parts[i + 1], out int s) ? s : 1;
            int quality = int.TryParse(parts[i + 2], out int q) ? q : 0;
            result.Add(new BundleIngredient(parts[i], stack, quality));
        }
        return result;
    }

    /// <summary>A category requirement is a bare negative number (e.g. "-5" = any animal product).</summary>
    public static bool IsCategoryRef(string itemRef)
        => int.TryParse(itemRef, out int n) && n < 0;

    /// <summary>Qualify a bare object id ("24" -> "(O)24"); leave already-qualified ids ("(O)24", "(BC)10") as-is.</summary>
    public static string NormalizeItemId(string itemRef)
    {
        if (string.IsNullOrEmpty(itemRef)) return itemRef;
        return itemRef[0] == '(' ? itemRef : "(O)" + itemRef;
    }
}
