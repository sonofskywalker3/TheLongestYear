using System;
using System.Collections.Generic;

namespace TheLongestYear.Core;

/// <summary>Resolves festival memory line keys (deja-vu phase 2). "Before" and any-time lines are
/// festmem.&lt;memory&gt;.&lt;npc&gt;.&lt;n&gt; with festmem.&lt;memory&gt;.default.&lt;n&gt; as the fallback;
/// post-result lines are festmem.&lt;memory&gt;.&lt;npc&gt;.after.&lt;n&gt;. Pools are discovered from the
/// translation key set (n from 1, contiguous), so adding a line is a JSON edit. A separate prefix from
/// phase 1's dejavu.* so neither family sees the other's lines.</summary>
public static class FestivalMemoryLines
{
    public const string Prefix = "festmem.";
    public const string DefaultPool = "default";
    public const string AfterSegment = "after";
    public const string ItemToken = "item";
    public const string PartnerToken = "partner";
    private const string UnfilledToken = "{{";

    private static ISet<string> AsSet(IReadOnlyCollection<string> available)
        => available as ISet<string> ?? new HashSet<string>(available, StringComparer.Ordinal);

    private static string Stem(string memory, string slug, bool after)
        => after ? $"{Prefix}{memory}.{slug}.{AfterSegment}." : $"{Prefix}{memory}.{slug}.";

    private static List<string> Pool(string memory, string slug, bool after, ISet<string> set)
    {
        var keys = new List<string>();
        string stem = Stem(memory, slug, after);
        for (int n = 1; ; n++)
        {
            string key = stem + n;
            if (!set.Contains(key)) break;
            keys.Add(key);
        }
        return keys;
    }

    /// <summary>The villager's own pool for this memory, else the fallback pool (empty when neither
    /// exists; a post-result line has a fallback only where the lines file gives one).</summary>
    public static IReadOnlyList<string> KeysFor(string memory, string npc, bool after, IReadOnlyCollection<string> available)
    {
        ISet<string> set = AsSet(available);
        List<string> own = Pool(memory, (npc ?? "").ToLowerInvariant(), after, set);
        return own.Count > 0 ? own : Pool(memory, DefaultPool, after, set);
    }

    /// <summary>The villagers (lower-case) with their own post-result lines for this memory.</summary>
    public static IReadOnlyList<string> AfterSpeakers(string memory, IReadOnlyCollection<string> available)
    {
        var slugs = new List<string>();
        string head = Prefix + memory + ".";
        string tail = "." + AfterSegment + ".1";
        foreach (string key in available)
        {
            if (!key.StartsWith(head, StringComparison.Ordinal) || !key.EndsWith(tail, StringComparison.Ordinal)) continue;
            string slug = key.Substring(head.Length, key.Length - head.Length - tail.Length);
            if (slug.Length == 0 || slug.Contains('.') || slug == DefaultPool || slugs.Contains(slug)) continue;
            slugs.Add(slug);
        }
        slugs.Sort(StringComparer.Ordinal);
        return slugs;
    }

    /// <summary>The line to speak, tokens filled, or null when there is no pool or a token the line
    /// needs is missing (a removed mod item, no partner yet). <paramref name="rollIndex"/> maps a pool
    /// size to an index in [0,size).</summary>
    public static string? Pick(string memory, string npc, bool after, IReadOnlyCollection<string> available,
        Func<int, int> rollIndex, string? itemName, string? partnerName)
    {
        IReadOnlyList<string> keys = KeysFor(memory, npc, after, available);
        if (keys.Count == 0) return null;
        int i = Math.Clamp(rollIndex(keys.Count), 0, keys.Count - 1);
        var tokens = new Dictionary<string, string>();
        if (!string.IsNullOrEmpty(itemName)) tokens[ItemToken] = itemName;
        if (!string.IsNullOrEmpty(partnerName)) tokens[PartnerToken] = partnerName;
        string text = Strings.Get(keys[i], tokens);
        return text == null || text.Contains(UnfilledToken) ? null : text;
    }

    /// <summary>Every festmem.* key (the i18n guard executes this).</summary>
    public static IEnumerable<string> AllKeys(IReadOnlyCollection<string> available)
    {
        foreach (string key in available)
            if (key.StartsWith(Prefix, StringComparison.Ordinal))
                yield return key;
    }
}
