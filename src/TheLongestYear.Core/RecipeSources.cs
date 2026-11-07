using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace TheLongestYear.Core;

/// <summary>One source tab of the recipe book's picker: the recipes one source added, A to Z.</summary>
/// <param name="Key">The source: <see cref="RecipeSources.Vanilla"/>, a supported mod's UniqueID,
/// <see cref="RecipeSources.OtherMods"/> or <see cref="RecipeSources.All"/>.</param>
public sealed record RecipeSourceTab(string Key, string Label, IReadOnlyList<string> RecipeIds);

/// <summary>
/// Sorts the recipe book's picker by where each recipe comes from, so the base game's recipes are not
/// buried under a mod's hundreds (Celjhyn, Nexus posts, 2026-11-07, with Cornucopia). A recipe is the
/// base game's when the unmodded game defines its key (<see cref="VanillaRecipeKeys"/>); otherwise it
/// belongs to the supported mod whose layer adds the item it makes, and any other is another mod's.
/// Every supported mod gets its own tab without more work: its layer already lists its items.
/// </summary>
public static class RecipeSources
{
    public const string Vanilla = "";
    public const string OtherMods = "*other";
    public const string All = "*all";

    /// <summary>Which supported mod adds each item: every item id in a single-mod layer's answers that
    /// the unmodded game does not define, mapped to that mod. A base-game item a layer only changes the
    /// answer for is not the mod's. The first layer listed wins an item two layers both add.</summary>
    public static IReadOnlyDictionary<string, string> ItemOwners(
        IEnumerable<(string ModId, IEnumerable<string> ItemIds)> layers, IReadOnlySet<string> vanillaItemIds)
    {
        var owners = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach ((string modId, IEnumerable<string> itemIds) in layers)
            foreach (string id in itemIds)
                if (!vanillaItemIds.Contains(id))
                    owners.TryAdd(id, modId);
        return owners;
    }

    /// <summary>The source of one recipe: <see cref="Vanilla"/>, the UniqueID of the supported mod
    /// that adds the item it makes, or <see cref="OtherMods"/>.</summary>
    public static string SourceOf(string recipeKey, string? outputQualifiedId,
        IReadOnlySet<string> vanillaRecipeKeys, IReadOnlyDictionary<string, string> itemOwners)
    {
        if (vanillaRecipeKeys.Contains(recipeKey)) return Vanilla;
        return outputQualifiedId != null && itemOwners.TryGetValue(outputQualifiedId, out string? mod) ? mod : OtherMods;
    }

    /// <summary>The picker's tabs: the base game first, then each supported mod by label, then other
    /// mods, then All; every tab A to Z by name, and a source with no recipe gets no tab. With one
    /// source only, that one tab (the picker shows no tab row then).</summary>
    /// <param name="labelOf">The label for a source key (the mod's name, or the base game's,
    /// other mods' or all's own string).</param>
    public static IReadOnlyList<RecipeSourceTab> Tabs(
        IEnumerable<(string Id, string Name, string Source)> recipes, Func<string, string> labelOf)
    {
        var sorted = recipes
            .OrderBy(r => r.Name, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(r => r.Id, StringComparer.Ordinal)
            .ToList();
        var tabs = sorted
            .GroupBy(r => r.Source, StringComparer.Ordinal)
            .Select(g => new RecipeSourceTab(g.Key, labelOf(g.Key), g.Select(r => r.Id).ToList()))
            .OrderBy(t => Rank(t.Key))
            .ThenBy(t => t.Label, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        if (tabs.Count > 1)
            tabs.Add(new RecipeSourceTab(All, labelOf(All), sorted.Select(r => r.Id).ToList()));
        return tabs;
    }

    /// <summary>The tab to open on: the one last chosen while it still has recipes, else the base
    /// game's, else the first.</summary>
    public static int StartTab(IReadOnlyList<RecipeSourceTab> tabs, string? lastKey)
    {
        for (int i = 0; i < tabs.Count; i++)
            if (tabs[i].Key == lastKey) return i;
        for (int i = 0; i < tabs.Count; i++)
            if (tabs[i].Key == Vanilla) return i;
        return 0;
    }

    private static readonly Regex PackTag = new(@"^\s*[\[\(][^\]\)]*[\]\)]\s*|\s*([\[\(]\s*CP\s*[\]\)]|[-_.\s]CP)\s*$", RegexOptions.IgnoreCase);

    /// <summary>A mod's name as a tab label: without a pack tag ("[CP] ", "-CP"), and with the dots or
    /// underscores of a name written without spaces read as spaces ("Seven.Deadly.Sins-CP" is
    /// "Seven Deadly Sins").</summary>
    public static string TabName(string modName)
    {
        string name = PackTag.Replace(modName, "").Trim();
        if (!name.Contains(' ')) name = name.Replace('.', ' ').Replace('_', ' ');
        return name.Length == 0 ? modName : name;
    }

    private static int Rank(string key) => key switch
    {
        Vanilla => 0,
        OtherMods => 2,
        _ => 1,
    };
}
