using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using TheLongestYear.Core;
using TheLongestYear.Core.Ending;
using TheLongestYear.Core.Intro;
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

    /// <summary>OpeningStrings.Replacements maps vanilla string ids to <c>"opening.grandpa-1-m"</c> and the
    /// like; the values are resolved through a Func at the editor, so LiteralKey never sees them at a
    /// Strings.Get call. Catch the family by its distinctive prefix, as EggColorKeyLiteral does.</summary>
    private static readonly Regex OpeningStringsKeyLiteral = new(@"""(?<key>opening\.[a-z0-9\-]+)""", RegexOptions.Compiled);

    /// <summary>EndingEventInjector routes every script line through its local <c>EventText()</c>
    /// sanitiser instead of calling <see cref="Strings.Get"/> directly (a translated '"' or '/' would
    /// break the '/'-joined event script), so <see cref="LiteralKey"/> cannot see those keys at the
    /// call site. They are still literal arguments, just to a different method; match that call the
    /// same way. The name is deliberately distinctive so no unrelated <c>Text(...)</c> call is
    /// mistaken for a key reference.</summary>
    private static readonly Regex EventTextKey = new(@"EventText\(\s*""(?<key>[a-z0-9.\-]+)""", RegexOptions.Compiled);

    /// <summary>OpeningScript.LineKeys builds its keys as <c>Prefix + "robin-1"</c>; the prefix is "event.opening.".
    /// The leading negative lookbehind is required: without it, a bare "Prefix" match also fires inside
    /// SeasonTurn.cs's <c>KeyPrefix + "winter-2"</c> and OpeningStrings.cs's <c>VanillaPrefix + "12026"</c>
    /// (both end in the substring "Prefix"), producing bogus event.opening.* keys.</summary>
    private static readonly Regex OpeningKey = new(@"(?<![A-Za-z])Prefix\s*\+\s*""(?<key>[a-z0-9\-]+)""", RegexOptions.Compiled);

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
        foreach (Match m in OpeningStringsKeyLiteral.Matches(text)) into.Add(m.Groups["key"].Value);
        foreach (Match m in EventTextKey.Matches(text)) into.Add(m.Groups["key"].Value);
        foreach (Match m in OpeningKey.Matches(text)) into.Add("event.opening." + m.Groups["key"].Value);
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
            // Boost name/desc keys live on the catalog entries, not as literals in source
            // (the shrine menu renders them through BoostDefinition), walk them the same way.
            foreach (BoostDefinition boost in BoostCatalog.All)
            {
                _ = Strings.Get(boost.NameKey);
                _ = Strings.Get(boost.DescKey);
            }
            // dejavu.* is resolved from the key set at runtime (DejaVuLines); execute the same walk.
            foreach (string key in DejaVuLines.AllKeys(map.Keys.ToList()))
                _ = Strings.Get(key);
            // reach.* keys are built from the reach metric (ReachText.Describe); walk every catalog requirement.
            foreach (var def in UpgradeCatalog.All)
                _ = ReachText.Describe(def.RunReachRequirement);
            foreach (string metric in new[] { "rod", "backpack", "mastery", "book", "mail", "event", "stardrop_mines", "scythe", "house", "pet", "shortcuts", "bus", "room" })
                _ = Strings.Get("reach." + metric);
            // event.ending.crack.* is built from the tier + npc (EndingLine.MiddleKey); walk every
            // tier for every voice-override npc plus one generic (non-overridden) npc.
            var endingNpcs = EndingLine.VoiceOverrides.Append("Pierre");
            foreach (EndingLineTier tier in Enum.GetValues<EndingLineTier>())
                foreach (string npc in endingNpcs)
                    _ = Strings.Get(EndingLine.MiddleKey(npc, tier));
            _ = Strings.Get(EndingLine.OpenKey);
            _ = Strings.Get(EndingLine.CloseKey);
            // event.ending.scene.* is resolved by event id through EndingLine.SceneTable, never a literal.
            foreach (string sceneKey in EndingLine.SceneTable.Values)
                _ = Strings.Get(sceneKey);
            // bundle-slot.* keys are looked up by item id (FlavorlessBundleSlots) and only ever
            // reach Strings.Get through a variable, so walk the rule's own key set.
            foreach (string key in FlavorlessBundleSlots.AllLabelKeys)
                _ = Strings.Get(key);
            // event.turn.* keys: the Summer closer picks its key with a ternary inside the
            // KeyPrefix concatenation (KeyPrefix + (rewound ? "summer-3-again" : "summer-3")),
            // so the literal-scanning regex above can never see either half. Walk the exhaustive
            // key list instead.
            foreach (string key in SeasonTurn.AllLineKeys)
                _ = Strings.Get(key);
            // mail.joja.come-N / mail.joja.decide-N are built as $"mail.joja.come-{i}" /
            // $"mail.joja.decide-{i}" in JojaLetterService.OnAssetRequested, over the fixed
            // counts JojaOffer.ComeLetters/DecisionLetters; walk the same ranges.
            for (int i = 1; i <= TheLongestYear.Core.Joja.JojaOffer.ComeLetters; i++)
                _ = Strings.Get($"mail.joja.come-{i}");
            for (int i = 1; i <= TheLongestYear.Core.Joja.JojaOffer.DecisionLetters; i++)
                _ = Strings.Get($"mail.joja.decide-{i}");
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
        foreach (BoostDefinition boost in BoostCatalog.All)
        {
            Assert.NotEqual(boost.NameKey, Strings.Get(boost.NameKey));
            Assert.NotEqual(boost.DescKey, Strings.Get(boost.DescKey));
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

    /// <summary>Matches a literal-key <c>Strings.Get(...)</c> call site (incl. the ternary-key
    /// form) whose second argument is an inline <c>Dictionary&lt;string, string&gt;</c> token
    /// literal, capturing the flat <c>["name"] = value</c> body between the dictionary's braces.
    /// None of the token dictionaries in this codebase nest braces, so a single non-brace
    /// character class correctly bounds the body without a full brace-balance walk.</summary>
    private static readonly Regex TokenCallSite = new(
        @"Strings\.Get\(\s*(?:[^,()""]*?\?\s*)?""(?<key1>[a-z0-9.\-]+)""(?:\s*:\s*""(?<key2>[a-z0-9.\-]+)"")?\s*,\s*new Dictionary<string,\s*string>\s*(?:\([^()]*\))?\s*\{(?<body>[^{}]*)\}",
        RegexOptions.Compiled);

    private static readonly Regex TokenDictEntry = new(@"\[""(?<tok>[^""]+)""\]\s*=", RegexOptions.Compiled);

    /// <summary>Prefix families whose token round-trip is proven elsewhere: <c>UpgradeDefinition</c>
    /// resolves <c>upgrade.*</c>/<c>upgrade-tpl.*</c>/<c>tier.*</c>/<c>tool.*</c>/<c>skill.*</c> keys
    /// through a lazily-built token dictionary keyed by variables, not string literals, so this
    /// literal-scanning regex can never see those call sites — they're never referenced by a bare
    /// <c>Strings.Get("literal-key", ...)</c> anywhere. <see cref="EveryCatalogKey_ExistsInDefaultJson"/>
    /// already asserts no "{{" survives in resolved catalog output, which is strictly stronger proof
    /// for that family than a per-key token match here would be.</summary>
    private static readonly string[] ExcludedTokenFamilies =
        { "upgrade.", "upgrade-tpl.", "tier.", "tool.", "skill.", "reach.", "event.ending.crack.tier" };
    // reach.*: ReachText builds the key from the metric (ReachTextTests covers it).
    // event.ending.crack.tier*: the {{scene}} token is supplied at a call site whose key is a
    // variable (the ending injector builds it via EndingLine.MiddleKey), so the literal-scanning
    // token check above can never see that call site.

    /// <summary>
    /// For every default.json value containing a <c>{{token}}</c> placeholder (outside the
    /// excluded families above), asserts at least one <c>Strings.Get</c> call site in source
    /// supplies every token the value actually uses. A call site may supply extra unused tokens
    /// (warn-level, not asserted) — only a MISSING token fails. Catches the class of bug where a
    /// key's English text gains/renames a token but a call site's dictionary isn't updated to match,
    /// which would otherwise only surface in-game as a literal unresolved "{{token}}" on screen.
    /// </summary>
    [Fact]
    public void EveryTokenedKey_HasCallSiteSupplyingAllTokens()
    {
        var tokenPattern = new Regex(@"\{\{(?<n>[a-z][a-z0-9]*)\}\}");

        var required = new Dictionary<string, HashSet<string>>();
        foreach (var kv in _fixture.Map)
        {
            if (ExcludedTokenFamilies.Any(p => kv.Key.StartsWith(p, StringComparison.Ordinal))) continue;
            var toks = tokenPattern.Matches(kv.Value).Select(m => m.Groups["n"].Value).ToHashSet();
            if (toks.Count > 0) required[kv.Key] = toks;
        }

        // suppliedSets[key] = one token-name set per call site found for that key in source.
        var suppliedSets = new Dictionary<string, List<HashSet<string>>>();
        foreach (string file in AllSourceFiles())
        {
            string text = File.ReadAllText(file);
            foreach (Match m in TokenCallSite.Matches(text))
            {
                var supplied = TokenDictEntry.Matches(m.Groups["body"].Value)
                    .Select(t => t.Groups["tok"].Value).ToHashSet();
                foreach (Group g in new[] { m.Groups["key1"], m.Groups["key2"] })
                {
                    if (!g.Success) continue;
                    if (!suppliedSets.TryGetValue(g.Value, out var list))
                        suppliedSets[g.Value] = list = new List<HashSet<string>>();
                    list.Add(supplied);
                }
            }
        }

        var problems = new List<string>();
        foreach (var (key, needed) in required)
        {
            if (!suppliedSets.TryGetValue(key, out var sites))
            {
                problems.Add($"{key}: no call site found supplying tokens {{{string.Join(",", needed)}}}");
                continue;
            }
            if (sites.Any(s => needed.IsSubsetOf(s))) continue;

            HashSet<string> closest = sites.OrderByDescending(s => s.Intersect(needed).Count()).First();
            problems.Add($"{key}: missing token(s) {{{string.Join(",", needed.Except(closest))}}} " +
                         $"(closest call site supplied {{{string.Join(",", closest)}}})");
        }
        Assert.True(problems.Count == 0, "Token round-trip guard failures:\n" + string.Join("\n", problems));
    }

    /// <summary>The opening's letter keys must keep vanilla's own {0}/{1} (name/farm) placeholders so
    /// the substituted text still reads correctly. This checks only the mod's own side of the swap
    /// (our default.json values against vanilla's placeholder shape); vanilla's own string is not on
    /// the test path here, since OpeningStrings.Apply is what actually writes into vanilla's data at
    /// runtime, and that is covered separately by OpeningStringsTests.</summary>
    [Fact]
    public void The_letter_keeps_vanillas_name_and_farm_tokens()
    {
        foreach (string key in new[] { "opening.letter-m", "opening.letter-f" })
            Assert.True(OpeningStrings.PlaceholdersMatch("Dear {0}, {1} Farm", _fixture.Map[key]), key);
    }

    /// <summary>Event-script safety: an event is one string whose commands are joined with '/', and a
    /// <c>speak</c> / <c>message</c> payload is wrapped in double quotes. An <c>event.</c> value
    /// containing either character would split the script into bogus commands or unbalance the quotes
    /// and break the ending outright. <c>EndingEventInjector.Text</c> sanitises both at runtime for
    /// translations we do not control; this guard keeps our own English source clean.</summary>
    [Fact]
    public void NoEventKeyValue_ContainsAScriptBreakingCharacter()
    {
        var problems = _fixture.Map
            .Where(kv => kv.Key.StartsWith("event.", StringComparison.Ordinal))
            .Where(kv => kv.Value.Contains('"') || kv.Value.Contains('/'))
            .Select(kv => $"{kv.Key}: {kv.Value}")
            .ToList();

        Assert.True(problems.Count == 0,
            "event.* values must contain no '\"' (unbalances the speak/message quotes) and no '/' " +
            "(splits the '/'-joined event script):\n" + string.Join("\n", problems));
    }
}
