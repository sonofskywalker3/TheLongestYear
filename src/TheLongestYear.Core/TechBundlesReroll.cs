using System;

namespace TheLongestYear.Core;

/// <summary>Tech's Cross-Mod Bundles, seen through the one thing TLY asks of it: roll a fresh board.
/// The mod-side implementation reaches it by reflection; tests use a fake.</summary>
public interface ITechBundlesRerollTarget
{
    /// <summary>True when Tech's Cross-Mod Bundles is installed and loaded.</summary>
    bool IsLoaded { get; }

    /// <summary>Rolls a fresh Tech board and writes it to the world. Throws when the mod's code is
    /// not where TLY expects it, or when the mod itself fails.</summary>
    void Reroll();
}

/// <summary>What <see cref="TechBundlesReroll.Run"/> did.</summary>
public enum TechRerollOutcome
{
    Skipped,
    Rerolled,
    Failed,
}

/// <summary>Normal and Remixed roll a fresh Tech's Cross-Mod Bundles board each loop (spec
/// 2026-10-08-custom-board-vanilla-only, addendum 2). Tech's mod generates its board only when a
/// save is created and serves that same board as Data/Bundles from then on, so a Normal reset
/// would get it back unchanged while vanilla's remix overwrites parts of it. On Normal or Remixed
/// the reset asks Tech's mod for a new board instead, right after the game rebuilds the world and
/// before TLY's own passes run over it.</summary>
public static class TechBundlesReroll
{
    public const string ModId = "TechnicalityCreations.CrossModBundles";

    public const string FailureWarning =
        "Tech's Cross-Mod Bundles changed; this loop uses the game's own board.";

    public const string RerolledInfo =
        "Reset: Tech's Cross-Mod Bundles is installed and this save is on Normal or Remixed, so it rolled a fresh board for this loop.";

    /// <summary>Only a Normal or Remixed reset that builds a new board, with Tech's mod loaded. A
    /// held board (Fail-night keep) is restored as it was, and TLY Custom never uses Tech's board.</summary>
    public static bool ShouldReroll(string? boardSource, bool restoringHeldBoard, bool techLoaded)
    {
        if (!techLoaded || restoringHeldBoard)
            return false;
        string normalized = BundleSourceNames.Normalize(boardSource);
        return string.Equals(normalized, BundleSourceNames.Normal, StringComparison.Ordinal)
               || string.Equals(normalized, BundleSourceNames.Remixed, StringComparison.Ordinal);
    }

    /// <summary>Rerolls when <see cref="ShouldReroll"/> says so. Never throws: a failure logs
    /// <see cref="FailureWarning"/> once and the reset carries on with the game's own board.</summary>
    public static TechRerollOutcome Run(
        ITechBundlesRerollTarget? target, string? boardSource, bool restoringHeldBoard,
        Action<string> logInfo, Action<string> logWarn)
    {
        if (target == null)
            return TechRerollOutcome.Skipped;
        bool loaded;
        try
        {
            loaded = target.IsLoaded;
        }
        catch (Exception ex)
        {
            logWarn($"{FailureWarning} ({ex.GetType().Name}: {ex.Message})");
            return TechRerollOutcome.Failed;
        }
        if (!ShouldReroll(boardSource, restoringHeldBoard, loaded))
            return TechRerollOutcome.Skipped;

        try
        {
            target.Reroll();
        }
        catch (Exception ex)
        {
            // Any failure inside another mod's code, or a renamed type/method, must not stop a reset.
            Exception inner = ex.InnerException ?? ex;
            logWarn($"{FailureWarning} ({inner.GetType().Name}: {inner.Message})");
            return TechRerollOutcome.Failed;
        }
        logInfo(RerolledInfo);
        return TechRerollOutcome.Rerolled;
    }
}
