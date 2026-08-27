using System;

namespace TheLongestYear.Core;

/// <summary>One difficulty modifier's setting. Four steps, no Off: <c>Off</c> would have had to
/// mean two different things (coherent for quality asks, degenerate for JP earned), so the ramp
/// starts at Easy. <see cref="Normal"/> is defined as exactly the mod's shipping balance, so a
/// save that never touches the Difficulty section plays identically to a pre-difficulty build.
/// Spec 2026-08-26 difficulty-modifiers.</summary>
public enum DifficultyStep
{
    Easy,
    Normal,
    Hard,
    Extreme,
}

/// <summary>Parsing and display helpers for <see cref="DifficultyStep"/>. Kept tolerant because
/// config.json is hand-editable: an unrecognised value resolves to <see cref="DifficultyStep.Normal"/>
/// rather than throwing, so one typo cannot stop a save from loading.</summary>
public static class DifficultySteps
{
    /// <summary>Every step, in ramp order. Feeds the GMCM dropdown's allowed values.</summary>
    public static readonly string[] AllNames =
    {
        nameof(DifficultyStep.Easy),
        nameof(DifficultyStep.Normal),
        nameof(DifficultyStep.Hard),
        nameof(DifficultyStep.Extreme),
    };

    /// <summary>Case-insensitive parse; unknown or empty input becomes
    /// <see cref="DifficultyStep.Normal"/>.</summary>
    public static DifficultyStep Parse(string? name)
        => Enum.TryParse(name, ignoreCase: true, out DifficultyStep step) && Enum.IsDefined(typeof(DifficultyStep), step)
            ? step
            : DifficultyStep.Normal;

    /// <summary>The i18n key suffix for a step ("easy", "normal", "hard", "extreme").</summary>
    public static string KeySuffix(DifficultyStep step) => step.ToString().ToLowerInvariant();
}
