namespace TheLongestYear.Core;

/// <summary>Verifies that a regenerated engine bundle set matches the live BundleData
/// EXACTLY (key AND value) for every generated key. Key-existence alone is not enough:
/// the engine's write-key space is deliberately invariant across generations, so a live
/// board holding an older loop's bundles (meta saved ahead of a skipped/crashed world
/// save) still has every key — only the VALUES betray the mismatch. On any difference
/// the caller must fall back to the legacy read-and-classify path, which serves live
/// truth by construction.</summary>
public static class EngineManifestCheck
{
    /// <summary>True when every generated entry exists in <paramref name="live"/> with a
    /// byte-identical value.</summary>
    public static bool Matches(
        System.Collections.Generic.IReadOnlyDictionary<string, string> generated,
        System.Collections.Generic.IReadOnlyDictionary<string, string> live)
    {
        foreach (var pair in generated)
        {
            if (!live.TryGetValue(pair.Key, out string? liveValue)) return false;
            if (!string.Equals(liveValue, pair.Value, System.StringComparison.Ordinal)) return false;
        }
        return true;
    }
}
