using System;

namespace TheLongestYear.Core;

/// <summary>Where a save's Community Center board comes from. ONE setting with three choices
/// (Jeff's ruling 2026-08-27), switchable on an existing save and applied at its next reset:
///
///   <c>Engine</c>  — The Longest Year composes its own board every loop (the new-game
///                    "TLY Custom" choice). The default.
///   <c>Normal</c>  — the game's own standard bundle layout, regenerated the same way each reset.
///   <c>Remixed</c> — the game's own remixed layout, likewise.
///
/// Before this there were two settings: an Engine/Vanilla switch here plus a per-save
/// Normal/Remixed value that only the new-game Advanced Options dropdown could set, so a player
/// could never move between the two vanilla layouts. Splitting one player-facing decision across
/// two places is what made that gap invisible.
///
/// <c>Vanilla</c> is retained ONLY as a legacy config value: configs written before this change
/// say "Vanilla" and cannot say which layout the save was on, so it resolves to whatever the save
/// already recorded rather than guessing. See <see cref="VanillaTypeFor"/>.
///
/// Stored as strings so config.json stays human-editable.</summary>
public static class BundleSourceNames
{
    public const string Engine = "Engine";
    public const string Normal = "Normal";
    public const string Remixed = "Remixed";

    /// <summary>Pre-2026-08-27 config value. Never offered in the UI; means "a vanilla board,
    /// layout unspecified", which resolves to the save's existing
    /// <see cref="MetaState.VanillaBundleType"/>.</summary>
    public const string LegacyVanilla = "Vanilla";

    /// <summary>Vanilla's own <c>Game1.BundleType</c> spellings, which is what
    /// <see cref="MetaState.VanillaBundleType"/> stores.</summary>
    public const string VanillaTypeDefault = "Default";
    public const string VanillaTypeRemixed = "Remixed";

    /// <summary>The three choices offered in the UI, in menu order.</summary>
    public static readonly string[] All = { Engine, Normal, Remixed };

    /// <summary>True for any board the GAME generates rather than the engine, legacy value
    /// included.</summary>
    public static bool IsVanilla(string? source) =>
        Is(source, Normal) || Is(source, Remixed) || Is(source, LegacyVanilla);

    /// <summary>The <c>Game1.BundleType</c> this source names, or null when it does not name one
    /// (Engine, or the legacy value). Null means "leave the save's existing layout alone".</summary>
    public static string? VanillaTypeFor(string? source)
    {
        if (Is(source, Normal)) return VanillaTypeDefault;
        if (Is(source, Remixed)) return VanillaTypeRemixed;
        return null;
    }

    /// <summary>The source that matches a save's recorded vanilla layout. Used to show a legacy
    /// "Vanilla" config as a real choice in the menu.</summary>
    public static string ForVanillaType(string? vanillaBundleType) =>
        Is(vanillaBundleType, VanillaTypeRemixed) ? Remixed : Normal;

    /// <summary>Canonical spelling for any accepted input. The legacy value is PRESERVED rather
    /// than folded into one of the two layouts: turning it into Normal would silently move a
    /// remixed save onto the standard board at its next reset.</summary>
    public static string Normalize(string? source)
    {
        if (Is(source, Normal)) return Normal;
        if (Is(source, Remixed)) return Remixed;
        if (Is(source, LegacyVanilla)) return LegacyVanilla;
        return Engine;
    }

    private static bool Is(string? a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
}
