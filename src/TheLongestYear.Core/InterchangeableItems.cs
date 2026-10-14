using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core;

/// <summary>Items a player sees as the same thing, so a donation slot asking for one takes the
/// other too, and both colors add up toward one slot's count.
///
/// Nexus bug report, flutegir, 2026-10-10: a 13-egg Animal slot took only white eggs, and the
/// brown eggs in the same inventory did not count. The game draws the line by exact item id, but
/// both eggs are named "Egg" and come from the same chickens. Jeff's call: white and brown are one
/// item to a player, per size. Duck, void, dinosaur and golden eggs stay their own items, and
/// quality rules are untouched.</summary>
public static class InterchangeableItems
{
    /// <summary>Each group is one item to a player. Qualified ids.</summary>
    private static readonly IReadOnlyList<IReadOnlySet<string>> Groups = new IReadOnlySet<string>[]
    {
        new HashSet<string>(new[] { "(O)176", "(O)180" }, StringComparer.Ordinal), // Egg, Brown Egg
        new HashSet<string>(new[] { "(O)174", "(O)182" }, StringComparer.Ordinal), // Large Egg, Large Brown Egg
    };

    private static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> GroupOf =
        Groups.SelectMany(g => g.Select(id => (id, g))).ToDictionary(p => p.id, p => p.g, StringComparer.Ordinal);

    /// <summary>True when <paramref name="offered"/> may fill a slot asking for
    /// <paramref name="asked"/>: the same item, or another color of the same egg. Bare and
    /// qualified ids both work.</summary>
    public static bool SameForDonation(string? asked, string? offered)
    {
        if (string.IsNullOrEmpty(asked) || string.IsNullOrEmpty(offered))
            return false;
        string a = BundleParsing.NormalizeItemId(asked);
        string o = BundleParsing.NormalizeItemId(offered);
        return string.Equals(a, o, StringComparison.Ordinal)
               || (GroupOf.TryGetValue(a, out IReadOnlySet<string>? group) && group.Contains(o));
    }

    /// <summary>The qualified item and every item that stands in for it. Empty for a missing id.</summary>
    public static IReadOnlyCollection<string> EquivalentsOf(string? itemId)
    {
        if (string.IsNullOrEmpty(itemId))
            return Array.Empty<string>();
        string id = BundleParsing.NormalizeItemId(itemId);
        return GroupOf.TryGetValue(id, out IReadOnlySet<string>? group) ? group.ToArray() : new[] { id };
    }
}
