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

    private static Func<string, string>? _itemNames;

    /// <summary>Item display-name provider for the "item:" catalog token (glue wires
    /// ItemRegistry). Uninitialised, <see cref="ItemName"/> echoes the qualified id, loud and
    /// never a crash, the same contract as <see cref="Get(string)"/>.</summary>
    public static void InitItemNames(Func<string, string> provider)
        => _itemNames = provider ?? throw new ArgumentNullException(nameof(provider));

    /// <summary>Test hook, clears the item-name provider.</summary>
    public static void ResetItemNames() => _itemNames = null;

    public static string ItemName(string qualifiedId)
        => _itemNames == null ? qualifiedId : _itemNames(qualifiedId);
}
