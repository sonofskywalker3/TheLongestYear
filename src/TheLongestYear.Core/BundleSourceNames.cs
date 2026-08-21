using System;

namespace TheLongestYear.Core;

/// <summary>Where a save's Community Center board comes from (spec 2026-08-21 BundleSource).
/// <c>Engine</c> — The Longest Year writes its own board every loop (default). <c>Vanilla</c> —
/// keep the game's own Standard/Remixed board (or another bundle mod's) and regenerate it the
/// same way on every reset. Stored as strings so config.json / GMCM stay human-editable.</summary>
public static class BundleSourceNames
{
    public const string Engine = "Engine";
    public const string Vanilla = "Vanilla";

    public static readonly string[] All = { Engine, Vanilla };

    public static bool IsVanilla(string? source) =>
        string.Equals(source, Vanilla, StringComparison.OrdinalIgnoreCase);

    /// <summary>Canonical spelling for any accepted input; unknown/null → <see cref="Engine"/>.</summary>
    public static string Normalize(string? source) => IsVanilla(source) ? Vanilla : Engine;
}
