using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>Loads the REAL i18n/default.json into Strings so tests assert on real English
/// text and double as a missing-key detector (a missing key comes back as the raw key and
/// fails the text assertion).</summary>
public sealed class I18nFixture
{
    public IReadOnlyDictionary<string, string> Map { get; }

    public I18nFixture()
    {
        Map = Load();
        var map = Map;
        Strings.Init((key, tokens) =>
        {
            if (!map.TryGetValue(key, out string? value))
                return key;
            if (tokens != null)
                foreach (var kv in tokens)
                    value = value.Replace("{{" + kv.Key + "}}", kv.Value, StringComparison.Ordinal);
            return value;
        });
    }

    public static string DefaultJsonPath =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
            "..", "..", "..", "..", "..", "src", "TheLongestYear", "i18n", "default.json"));

    private static IReadOnlyDictionary<string, string> Load()
    {
        // SMAPI allows // comments in i18n JSON; System.Text.Json needs them skipped.
        var options = new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };
        using var doc = JsonDocument.Parse(File.ReadAllText(DefaultJsonPath), options);
        var map = new Dictionary<string, string>();
        foreach (var prop in doc.RootElement.EnumerateObject())
            map[prop.Name] = prop.Value.GetString() ?? "";
        return map;
    }
}

[CollectionDefinition("i18n")]
public class I18nCollection : ICollectionFixture<I18nFixture> { }
