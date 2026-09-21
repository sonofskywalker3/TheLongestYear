using System;
using System.Collections.Generic;

namespace TheLongestYear.Core;

/// <summary>The three bundle asks whose vanilla name tells the player nothing.
///
/// Dried Fruit, Dried Mushrooms and Smoked Fish are flavored goods: the thing that was dried or
/// smoked lives in a separate field on the item, and the display name is built by dropping that
/// item's name into a format string. A Community Center bundle cannot fill that field in, because
/// vanilla parses a bundle's ingredients as (id, stack, quality) triples and nothing else
/// (Bundle.cs:133) — only Mr. Raccoon's bundles, which are built in code, ever set a flavor.
///
/// So the slot has no flavor, the format string gets nothing to format, and the player is left
/// with these objects' bare Name from Data/Objects, which is the word "Dried" or "Smoked" on its
/// own. That is Nexus bug 1137151.
///
/// The slot still ACCEPTS any flavor: with no preserved id to match, vanilla's check is
/// <c>ItemRegistry.HasItemId(item, ingredient.id)</c> (Bundle.cs:231), and every dried fruit
/// shares the id "DriedFruit". The ask is genuinely "any dried fruit" and is right to be, so the
/// fix is to say so rather than to narrow the slot or drop these goods from the pools.
///
/// Core-only: this maps an item id to an i18n key. The mod side looks the key up and writes it
/// over the menu's hover text.</summary>
public static class FlavorlessBundleSlots
{
    private static readonly IReadOnlyDictionary<string, string> LabelKeys =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["(O)DriedFruit"] = "bundle-slot.any-dried-fruit",
            ["(O)DriedMushrooms"] = "bundle-slot.any-dried-mushrooms",
            ["(O)SmokedFish"] = "bundle-slot.any-smoked-fish",
        };

    /// <summary>Every key this rule can return. The keys are dictionary values, not literals at a
    /// call site, so the i18n orphan guard walks this to prove they are all reachable.</summary>
    public static IEnumerable<string> AllLabelKeys => LabelKeys.Values;

    /// <summary>The i18n key naming what this slot really wants, or null for every other item.
    /// Takes a qualified or unqualified id: the menu hands out unqualified ItemIds and the pools
    /// hold qualified ones.</summary>
    public static string? LabelKeyFor(string? itemId)
    {
        if (string.IsNullOrEmpty(itemId)) return null;
        return LabelKeys.TryGetValue(BundleParsing.NormalizeItemId(itemId), out string? key) ? key : null;
    }
}
