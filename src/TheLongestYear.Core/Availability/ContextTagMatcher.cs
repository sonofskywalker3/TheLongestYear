using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core.Availability;

/// <summary>Mirrors the tags the game synthesises at runtime (ItemContextTagManager): the
/// category_* family maps to Data/Objects Category, item_* to the sanitised internal name, id_o_*
/// to the bare id. Anything else is looked up in the object's own ContextTags list.</summary>
public static class ContextTagMatcher
{
    private const string ItemTagPrefix = "item_";
    private const string ObjectIdTagPrefix = "id_o_";

    private static readonly IReadOnlyDictionary<string, int> CategoryTags =
        new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["category_gem"] = -2, ["category_fish"] = -4, ["category_egg"] = -5,
            ["category_milk"] = -6, ["category_cooking"] = -7, ["category_minerals"] = -12,
            ["category_metal_resources"] = -15, ["category_animal_product"] = -18,
            ["category_artisan_goods"] = -26, ["category_syrup"] = -27,
            ["category_monster_loot"] = -28, ["category_seeds"] = -74,
            ["category_vegetable"] = -75, ["category_fruit"] = -79,
            ["category_flower"] = -80, ["category_greens"] = -81,
            // Plural forms the game's Data/Machines rules actually use (a Preserves Jar asks for
            // category_fruits, not category_fruit); the singular ones stay for older data.
            ["category_gems"] = -2, ["category_fishes"] = -4, ["category_eggs"] = -5,
            ["category_minerals_"] = -12, ["category_animal_products"] = -18,
            ["category_artisan_good"] = -26, ["category_syrups"] = -27,
            ["category_seed"] = -74, ["category_vegetables"] = -75, ["category_fruits"] = -79,
            ["category_flowers"] = -80, ["category_green"] = -81, ["category_monster_loots"] = -28,
        };

    public static bool Matches(string bareId, RawObjectEntry obj, string tag)
    {
        if (obj == null || string.IsNullOrEmpty(tag)) return false;
        if (CategoryTags.TryGetValue(tag, out int category)) return obj.Category == category;
        if (tag.StartsWith(ObjectIdTagPrefix, StringComparison.Ordinal))
            return string.Equals(tag.Substring(ObjectIdTagPrefix.Length), bareId, StringComparison.OrdinalIgnoreCase);
        if (tag.StartsWith(ItemTagPrefix, StringComparison.Ordinal))
            return tag == ItemTag(obj.Name);
        return obj.ContextTags != null && obj.ContextTags.Contains(tag);
    }

    public static string ItemTag(string name)
        => ItemTagPrefix + (name ?? "").ToLowerInvariant().Replace(' ', '_').Replace("'", "");

    /// <summary>Qualified ids of every object matching ALL the tags, in ordinal order.</summary>
    public static IReadOnlyList<string> IdsMatchingAll(
        IReadOnlyDictionary<string, RawObjectEntry> objects, IReadOnlyList<string> tags)
        => objects
            .Where(kv => tags.All(t => Matches(kv.Key, kv.Value, t)))
            .Select(kv => BundleParsing.NormalizeItemId(kv.Key))
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToList();
}
