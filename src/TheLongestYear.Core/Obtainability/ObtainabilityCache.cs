using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;

namespace TheLongestYear.Core.Obtainability;

/// <summary>Keeps the last <see cref="ObtainabilityBuild"/> for as long as the game data it was built
/// from is unchanged. The build takes most of a second and runs on every save load, including the
/// deferred load right after a loop reset, where it added about 0.8 s to the post-shrine pause (log,
/// 2026-09-16) while reading the inputs took about 20 ms. The key is a hash of the whole input set,
/// so a content edit (a mod's shop change, a year-2 seed unlock) still rebuilds.</summary>
public sealed class ObtainabilityCache
{
    private string? _key;
    private ObtainabilityBuild? _build;

    /// <param name="extraDirect">Passed to <see cref="ObtainabilityBuilder.Build"/>; part of the key.</param>
    public ObtainabilityBuild Get(
        ObtainabilityInputs inputs, out bool reused,
        IReadOnlyDictionary<string, IReadOnlyList<ObtainSource>>? extraDirect = null)
    {
        if (inputs is null) throw new ArgumentNullException(nameof(inputs));
        string key = Fingerprint(inputs) + (extraDirect is { Count: > 0 } ? ExtraFingerprint(extraDirect) : "");
        if (_build != null && key == _key)
        {
            reused = true;
            return _build;
        }

        _build = ObtainabilityBuilder.Build(inputs, extraDirect: extraDirect);
        _key = key;
        reused = false;
        return _build;
    }

    private static string ExtraFingerprint(IReadOnlyDictionary<string, IReadOnlyList<ObtainSource>> extra)
    {
        var sorted = new SortedDictionary<string, IReadOnlyList<ObtainSource>>(
            extra.ToDictionary(p => p.Key, p => p.Value), StringComparer.Ordinal);
        byte[] json = JsonSerializer.SerializeToUtf8Bytes(sorted, Lexicon.LexiconJson.Options);
        using var sha = SHA256.Create();
        return "+" + Convert.ToHexString(sha.ComputeHash(json));
    }

    private static string Fingerprint(ObtainabilityInputs inputs)
    {
        byte[] json = JsonSerializer.SerializeToUtf8Bytes(inputs);
        using var sha = SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(json));
    }
}
