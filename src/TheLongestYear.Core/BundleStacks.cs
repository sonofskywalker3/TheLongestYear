using System.Collections.Generic;

namespace TheLongestYear.Core;

/// <summary>How many of each item a bundle asks for, keyed by qualified id. A doubled id keeps its
/// largest stack (Construction lists Wood twice at 99; 99 is the "what fits any slot" reading).</summary>
public static class BundleStacks
{
    public static Dictionary<string, int> Of(IReadOnlyList<BundleIngredient> ingredients)
    {
        var stacks = new Dictionary<string, int>(System.StringComparer.Ordinal);
        foreach (BundleIngredient ing in ingredients)
        {
            if (BundleParsing.IsCategoryRef(ing.ItemRef)) continue;
            string id = BundleParsing.NormalizeItemId(ing.ItemRef);
            int stack = ing.Stack > 0 ? ing.Stack : 1;
            if (!stacks.TryGetValue(id, out int existing) || stack > existing)
                stacks[id] = stack;
        }
        return stacks;
    }

    /// <summary>The stacks the board asks for right now in bundle <paramref name="bundleIndex"/>, or
    /// null when the board has no such bundle. Reads the board as it is, so a stack the theme week
    /// discount lowered or put back shows its current value (Reddit report 2026-10-07).</summary>
    public static IReadOnlyDictionary<string, int>? ForBundle(IReadOnlyDictionary<string, string>? board, int bundleIndex)
    {
        if (board == null) return null;
        foreach (KeyValuePair<string, string> entry in board)
        {
            ParsedBundle parsed = BundleParsing.Parse(entry.Key, entry.Value);
            if (parsed.Index == bundleIndex)
                return Of(parsed.Ingredients);
        }
        return null;
    }
}
