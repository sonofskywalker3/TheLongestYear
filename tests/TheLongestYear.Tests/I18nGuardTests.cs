using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>
/// Guards that keep every player-visible string flowing through <see cref="Strings"/> with a
/// live key in <c>i18n/default.json</c>: no literal key referenced in source is missing, no
/// dynamically-composed catalog key fails to resolve, no key in default.json is unreachable
/// dead weight, and no <c>{{token}}</c> placeholder is malformed.
/// </summary>
[Collection("i18n")]
public class I18nGuardTests
{
    private readonly I18nFixture _fixture;
    public I18nGuardTests(I18nFixture fixture) => _fixture = fixture;

    private static string SrcRoot =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src"));

    /// <summary>Matches <c>Strings.Get("key", ...)</c> and the ternary-selected-key form
    /// <c>Strings.Get(cond ? "key-a" : "key-b", ...)</c> (e.g. JunimoStashCapPatch's
    /// singular/plural HUD message) in one pass. The ternary-condition prefix and the
    /// "key2" alternate are both optional, so a plain single-literal call still matches via
    /// key1 alone — this is a superset of the naive "quote right after the paren" pattern.</summary>
    private static readonly Regex LiteralKey = new(
        @"Strings\.Get\(\s*(?:[^,()""]*?\?\s*)?""(?<key1>[a-z0-9.\-]+)""(?:\s*:\s*""(?<key2>[a-z0-9.\-]+)"")?",
        RegexOptions.Compiled);

    private static readonly Regex I18nToken = new(@"""i18n:(?<key>[a-z0-9.\-]+)""", RegexOptions.Compiled);

    /// <summary>WeeklyThemeQuestService.AmbiguousEggColors stores i18n key NAMES as dictionary
    /// values ("egg-color.white"/"egg-color.brown"), resolved later via a local variable at the
    /// Strings.Get call site — so <see cref="LiteralKey"/> can't see them there. They're still
    /// literal text in source, just not the direct call argument; catch them by the family's
    /// distinctive prefix instead of whitelisting the family wholesale.</summary>
    private static readonly Regex EggColorKeyLiteral = new(@"""(?<key>egg-color\.[a-z0-9\-]+)""", RegexOptions.Compiled);

    private static IEnumerable<string> AllSourceFiles()
        => Directory.EnumerateFiles(SrcRoot, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                     && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"));

    private static void AddLiteralMatches(string text, HashSet<string> into)
    {
        foreach (Match m in LiteralKey.Matches(text))
        {
            into.Add(m.Groups["key1"].Value);
            if (m.Groups["key2"].Success) into.Add(m.Groups["key2"].Value);
        }
        foreach (Match m in I18nToken.Matches(text)) into.Add(m.Groups["key"].Value);
        foreach (Match m in EggColorKeyLiteral.Matches(text)) into.Add(m.Groups["key"].Value);
    }

    /// <summary>
    /// Every i18n key actually reachable from source: literal <c>Strings.Get()</c> arguments
    /// (incl. ternary-selected pairs and the egg-color dict-value family) found by scanning
    /// file text, UNIONED with every key touched by genuinely EXECUTING the dynamically
    /// composed families — <c>theme.*</c> / <c>upgrade-category.*</c> (built via
    /// <c>ToLowerInvariant()</c> string interpolation in <see cref="ThemeDisplay"/>),
    /// <c>modifier.*</c> (interpolated in <see cref="ThemeModifiers.DisplayNameFor"/>), and
    /// <c>upgrade.*</c> / <c>upgrade-tpl.*</c> / <c>tier.*</c> / <c>tool.*</c> / <c>skill.*</c>
    /// (built by <see cref="UpgradeCatalog"/> / <see cref="UpgradeCatalogGenerators"/> and
    /// resolved through <see cref="UpgradeDefinition"/>'s lazy "i18n:"-token indirection).
    /// None of those families can be found by any text regex — they don't exist as string
    /// literals anywhere. Recording every key the real code asks for is strictly stronger
    /// proof of reachability than whitelisting the prefix: it fails the moment a real code
    /// path stops asking for a key default.json still defines (a genuine orphan), whereas a
    /// prefix whitelist would keep passing forever regardless of what the code actually does.
    /// </summary>
    private HashSet<string> ReferencedKeys()
    {
        var keys = new HashSet<string>();
        foreach (string file in AllSourceFiles())
            AddLiteralMatches(File.ReadAllText(file), keys);

        var recorded = new HashSet<string>();
        try
        {
            IReadOnlyDictionary<string, string> map = _fixture.Map;
            Strings.Init((key, tokens) =>
            {
                recorded.Add(key);
                if (!map.TryGetValue(key, out string? value)) return key;
                if (tokens != null)
                    foreach (var kv in tokens)
                        value = value.Replace("{{" + kv.Key + "}}", kv.Value, StringComparison.Ordinal);
                return value;
            });

            foreach (Theme t in Enum.GetValues<Theme>())
            {
                _ = ThemeDisplay.Name(t);
                var (bonus, liability) = ThemeModifiers.For(t);
                _ = ThemeModifiers.DisplayNameFor(bonus);
                _ = ThemeModifiers.DisplayNameFor(liability);
            }
            foreach (UpgradeCategory c in Enum.GetValues<UpgradeCategory>())
                _ = ThemeDisplay.CategoryName(c);
            foreach (var def in UpgradeCatalog.All)
            {
                _ = def.DisplayName;
                _ = def.Description;
            }
        }
        finally
        {
            // Restore the real provider unconditionally — this test must never leave the
            // Strings facade pointed at the recording delegate for classes that run after it.
            I18nFixture.InstallGlobalProvider();
        }

        keys.UnionWith(recorded);
        return keys;
    }

    [Fact]
    public void EveryLiteralKeyInSource_ExistsInDefaultJson()
    {
        var missing = new List<string>();
        foreach (string file in AllSourceFiles())
        {
            var found = new HashSet<string>();
            AddLiteralMatches(File.ReadAllText(file), found);
            foreach (string key in found)
                if (!_fixture.Map.ContainsKey(key))
                    missing.Add($"{key} ({Path.GetFileName(file)})");
        }
        Assert.True(missing.Count == 0, "Keys referenced but missing from default.json:\n" + string.Join("\n", missing.Distinct()));
    }

    [Fact]
    public void EveryCatalogKey_ExistsInDefaultJson()
    {
        // Lazy resolution returns the key itself when missing — detect that.
        foreach (var def in UpgradeCatalog.All)
        {
            Assert.False(def.DisplayName.StartsWith("upgrade"), $"unresolved name for {def.Id}: {def.DisplayName}");
            Assert.False(def.Description.StartsWith("upgrade"), $"unresolved desc for {def.Id}: {def.Description}");
        }
        foreach (Theme t in Enum.GetValues<Theme>())
        {
            _ = ThemeDisplay.Name(t);
            var (b, l) = ThemeModifiers.For(t);
            Assert.False(ThemeModifiers.DisplayNameFor(b).StartsWith("modifier."));
            Assert.False(ThemeModifiers.DisplayNameFor(l).StartsWith("modifier."));
        }
    }

    [Fact]
    public void NoOrphanKeys_InDefaultJson()
    {
        var referenced = ReferencedKeys();
        var orphans = _fixture.Map.Keys.Where(k => !referenced.Contains(k)).ToList();
        Assert.True(orphans.Count == 0, "Orphan keys in default.json:\n" + string.Join("\n", orphans));
    }

    [Fact]
    public void EveryTokenInValues_LooksSane()
    {
        // {{token}} names must be lowercase word chars — catches typos like {{ count }} or {{Count}}.
        var bad = new List<string>();
        var token = new Regex(@"\{\{(?<n>[^}]*)\}\}");
        foreach (var kv in _fixture.Map)
            foreach (Match m in token.Matches(kv.Value))
                if (!Regex.IsMatch(m.Groups["n"].Value, @"^[a-z][a-z0-9]*$"))
                    bad.Add($"{kv.Key}: '{{{{{m.Groups["n"].Value}}}}}'");
        Assert.True(bad.Count == 0, string.Join("\n", bad));
    }
}
