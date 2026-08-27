using System;
using System.Collections.Generic;

namespace TheLongestYear.Core;

/// <summary>Resolves deja-vu line keys: dejavu.&lt;npc&gt;.&lt;tier&gt;.&lt;n&gt; (n from 1, contiguous)
/// with dejavu.default.&lt;tier&gt;.&lt;n&gt; as the pool for any villager without lines. Pools are
/// discovered from the translation key set, so adding a line is a JSON edit.</summary>
public static class DejaVuLines
{
    public const string Prefix = "dejavu.";
    public const string DefaultPool = "default";

    private static List<string> Pool(string slug, int tier, IReadOnlyCollection<string> available)
    {
        var keys = new List<string>();
        var set = available as ISet<string> ?? new HashSet<string>(available, StringComparer.Ordinal);
        for (int n = 1; ; n++)
        {
            string key = $"{Prefix}{slug}.{tier}.{n}";
            if (!set.Contains(key)) break;
            keys.Add(key);
        }
        return keys;
    }

    public static IReadOnlyList<string> KeysFor(string npc, int tier, IReadOnlyCollection<string> available)
    {
        var own = Pool(npc.ToLowerInvariant(), tier, available);
        return own.Count > 0 ? own : Pool(DefaultPool, tier, available);
    }

    /// <summary><paramref name="rollIndex"/> maps a pool size to an index in [0,size).</summary>
    public static string? Pick(string npc, int tier, IReadOnlyCollection<string> available, Func<int, int> rollIndex)
    {
        IReadOnlyList<string> keys = KeysFor(npc, tier, available);
        if (keys.Count == 0) return null;
        int i = Math.Clamp(rollIndex(keys.Count), 0, keys.Count - 1);
        return Strings.Get(keys[i]);
    }

    /// <summary>Every dejavu.* key the family could ask for (the i18n guard executes this).</summary>
    public static IEnumerable<string> AllKeys(IReadOnlyCollection<string> available)
    {
        foreach (string key in available)
            if (key.StartsWith(Prefix, StringComparison.Ordinal))
                yield return key;
    }
}
