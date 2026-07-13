using System;
using System.Collections.Generic;

namespace TheLongestYear.Core;

/// <summary>
/// Translation lookup facade. Core has no SMAPI reference, so ModEntry injects a provider
/// backed by ITranslationHelper at startup; tests inject a dictionary loaded from the real
/// i18n/default.json (see I18nFixture). Uninitialized, Get returns the key itself — loud
/// in-game ("menu.hub.title" on screen), never a crash.
/// </summary>
public static class Strings
{
    private static Func<string, IReadOnlyDictionary<string, string>?, string>? _provider;

    public static void Init(Func<string, IReadOnlyDictionary<string, string>?, string> provider)
        => _provider = provider ?? throw new ArgumentNullException(nameof(provider));

    /// <summary>Test hook — clears the provider so tests can assert uninitialized behavior.</summary>
    public static void Reset() => _provider = null;

    public static string Get(string key)
        => _provider == null ? key : _provider(key, null);

    public static string Get(string key, IReadOnlyDictionary<string, string> tokens)
        => _provider == null ? key : _provider(key, tokens);
}
