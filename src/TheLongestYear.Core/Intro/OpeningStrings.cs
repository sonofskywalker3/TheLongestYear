using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace TheLongestYear.Core.Intro;

/// <summary>Grandpa's deathbed speech and the letter, replaced word for word in
/// Strings/StringsFromCSFiles while the vanilla pictures play (spec 2026-09-16-expanded-opening-design.md,
/// section 2.2). Keys are the vanilla string ids GrandpaStory.cs reads (lines 84-91 the speech queue,
/// 312 the letter); values are the mod's i18n keys. The two "-m" / "-f" pairs are the male and
/// female variants vanilla itself has.</summary>
public static class OpeningStrings
{
    private const string VanillaPrefix = "GrandpaStory.cs.";
    private static readonly Regex Placeholder = new(@"\{\d+\}", RegexOptions.Compiled);

    public static readonly IReadOnlyDictionary<string, string> Replacements = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [VanillaPrefix + "12026"] = "opening.grandpa-1-m",
        [VanillaPrefix + "12028"] = "opening.grandpa-1-f",
        [VanillaPrefix + "12029"] = "opening.grandpa-2",
        [VanillaPrefix + "12030"] = "opening.grandpa-3",
        [VanillaPrefix + "12031"] = "opening.grandpa-4",
        [VanillaPrefix + "12034"] = "opening.grandpa-5",
        [VanillaPrefix + "12035"] = "opening.grandpa-6",
        [VanillaPrefix + "12036"] = "opening.grandpa-7-m",
        [VanillaPrefix + "12038"] = "opening.grandpa-7-f",
        [VanillaPrefix + "12040"] = "opening.grandpa-8",
        [VanillaPrefix + "12051"] = "opening.letter-m",
        [VanillaPrefix + "12055"] = "opening.letter-f",
    };

    /// <summary>Overwrites each mapped vanilla key with the mod's text when that text is non-empty
    /// (an empty i18n value leaves vanilla's line in place rather than blanking the scene).</summary>
    public static int Apply(IDictionary<string, string> data, Func<string, string> text)
    {
        int replaced = 0;
        foreach ((string vanillaKey, string ourKey) in Replacements)
        {
            string value = text(ourKey);
            if (string.IsNullOrEmpty(value)) continue;
            data[vanillaKey] = value;
            replaced++;
        }
        return replaced;
    }

    /// <summary>True when both strings use the same set of {n} tokens (the letter needs {0} and {1}).</summary>
    public static bool PlaceholdersMatch(string vanilla, string ours)
    {
        var a = Placeholder.Matches(vanilla).Select(m => m.Value).ToHashSet(StringComparer.Ordinal);
        var b = Placeholder.Matches(ours).Select(m => m.Value).ToHashSet(StringComparer.Ordinal);
        return a.SetEquals(b);
    }
}
