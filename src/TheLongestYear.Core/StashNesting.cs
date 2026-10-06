using System;
using System.Collections.Generic;

namespace TheLongestYear.Core;

/// <summary>
/// The stash's nesting rule (spec 2026-10-01, Jeff ruling option C): a container may go into the
/// Junimo Stash only if everything inside it, at any depth, is cosmetic. Cosmetic is decided by the
/// item's type prefix alone, so the rule is the same for vanilla and mod items: hats, shirts, pants,
/// furniture, wallpaper and flooring. Everything else (boots, rings, trinkets, objects, big
/// craftables, tools, weapons, mannequins) may still go in as a top-level item, one slot each.
///
/// "Inside" means <see cref="StashItemRecord.Contents"/> and <see cref="StashItemRecord.HeldObject"/>.
/// A tool's <see cref="StashItemRecord.Attachments"/> (rod bait and tackle) and a Combined Ring's
/// <see cref="StashItemRecord.InnerRings"/> are part of the item, not held by it, and are never walked.
/// </summary>
public static class StashNesting
{
    /// <summary>Item-id prefix of The Longest Year's own items (books, planning shrine). They are
    /// re-granted every loop, so a nested copy would duplicate them.</summary>
    public const string ModItemPrefix = "sonofskywalker3.TheLongestYear_";

    // ItemRegistry type prefixes: hat, shirt, pants, furniture, wallpaper, flooring.
    private static readonly string[] CosmeticTypePrefixes = { "(H)", "(S)", "(P)", "(F)", "(WP)", "(FL)" };

    public static bool IsCosmetic(string qualifiedItemId)
    {
        if (string.IsNullOrEmpty(qualifiedItemId))
            return false;
        foreach (string prefix in CosmeticTypePrefixes)
        {
            if (!qualifiedItemId.StartsWith(prefix, StringComparison.Ordinal))
                continue;
            return !qualifiedItemId.AsSpan(prefix.Length).StartsWith(ModItemPrefix, StringComparison.Ordinal);
        }
        return false;
    }

    public static IReadOnlyList<string> NonCosmeticNested(StashItemRecord record)
    {
        var found = new List<string>();
        foreach (StashItemRecord child in Children(record))
            Collect(child, found);
        return found;
    }

    public static bool CanStash(StashItemRecord record) => NonCosmeticNested(record).Count == 0;

    public static StashItemRecord Trim(StashItemRecord record, List<StashItemRecord> ejected)
    {
        List<StashItemRecord>? contents = null;
        if (record.Contents != null)
        {
            contents = new List<StashItemRecord>(record.Contents.Count);
            foreach (StashItemRecord child in record.Contents)
            {
                if (child == null) continue;
                StashItemRecord trimmedChild = Trim(child, ejected);
                if (IsCosmetic(child.ItemId))
                    contents.Add(trimmedChild);
                else
                    ejected.Add(trimmedChild);
            }
        }

        StashItemRecord? held = null;
        if (record.HeldObject != null)
        {
            StashItemRecord trimmedHeld = Trim(record.HeldObject, ejected);
            if (IsCosmetic(record.HeldObject.ItemId))
                held = trimmedHeld;
            else
                ejected.Add(trimmedHeld);
        }

        return record with { Contents = contents, HeldObject = held };
    }

    private static void Collect(StashItemRecord record, List<string> found)
    {
        if (!IsCosmetic(record.ItemId))
            found.Add(record.ItemId);
        foreach (StashItemRecord child in Children(record))
            Collect(child, found);
    }

    private static IEnumerable<StashItemRecord> Children(StashItemRecord record)
    {
        if (record.Contents != null)
            foreach (StashItemRecord child in record.Contents)
                if (child != null)
                    yield return child;
        if (record.HeldObject != null)
            yield return record.HeldObject;
    }
}
