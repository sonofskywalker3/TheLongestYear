using System;
using System.Collections.Generic;

namespace TheLongestYear.Core;

/// <summary>The live Community Center board, seen through the two things the load-time restore
/// needs. The mod side wraps <c>Game1.netWorldState.Value.BundleData</c> / <c>SetBundleData</c>;
/// tests use a fake.</summary>
public interface ILiveBundleBoard
{
    /// <summary>The live board, <c>"Room/Index" -> value</c>.</summary>
    IReadOnlyDictionary<string, string> Read();

    /// <summary>Upserts these entries into the live board (SetBundleData merges and never removes).</summary>
    void Write(IReadOnlyDictionary<string, string> updates);
}

/// <summary>TLY is the board of record on every load when Tech's Cross-Mod Bundles is loaded (spec
/// 2026-10-08-custom-board-vanilla-only, addendum 3). Tech's SaveLoaded handler writes its own saved
/// raw board over the live one on every load, and TLY's post-reset save never fires the Saving event
/// Tech's mod listens to, so its saved board never learns TLY's board. TLY's load runs after Tech's
/// (low priority) and writes back the board it stored for this loop before it classifies anything.</summary>
public static class TechBoardOfRecord
{
    public const string RestoredInfo =
        "Tech's Cross-Mod Bundles rewrote the board on load; restored this loop's board";

    /// <summary>Whether TLY's SaveLoaded work runs in the low-priority handler (after every other mod's
    /// normal-priority one) instead of the normal one. Only with Tech's mod loaded, so the order is
    /// unchanged for everyone else.</summary>
    public static bool RunLoadLate(bool techLoaded) => techLoaded;

    /// <summary>The board a Normal or Remixed reset stores as its board of record: the final board it
    /// wrote (after the difficulty pass, clamp and reward shuffle, or the held-board restore), copied.
    /// Null without Tech's mod, which keeps the field null as it always was in that mode.</summary>
    public static Dictionary<string, string>? VanillaBoardToStore(
        bool techLoaded, IReadOnlyDictionary<string, string>? finalBoard)
    {
        if (!techLoaded || finalBoard == null || finalBoard.Count == 0)
            return null;
        return ToDictionary(finalBoard);
    }

    /// <summary>The entries to write back: every stored key whose live value is missing or differs,
    /// the display-name field ignored on both sides (<see cref="OwnedFields"/>; the
    /// game recomputes it on every read). Empty when there is nothing to do.</summary>
    public static Dictionary<string, string> Updates(
        bool techLoaded, bool isHost,
        IReadOnlyDictionary<string, string>? stored, IReadOnlyDictionary<string, string>? live)
    {
        var updates = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!techLoaded || !isHost || stored == null || stored.Count == 0)
            return updates;
        foreach (KeyValuePair<string, string> pair in stored)
        {
            if (live != null
                && live.TryGetValue(pair.Key, out string? liveValue)
                && string.Equals(OwnedFields(liveValue), OwnedFields(pair.Value), StringComparison.Ordinal))
                continue;
            updates[pair.Key] = pair.Value;
        }
        return updates;
    }

    /// <summary>Restores the stored board when <see cref="Updates"/> finds a difference: one write, one
    /// <see cref="RestoredInfo"/> line. Returns the number of bundles written back (0 = no-op).</summary>
    public static int RestoreOnLoad(
        bool techLoaded, bool isHost, IReadOnlyDictionary<string, string>? stored,
        ILiveBundleBoard board, Action<string> logInfo)
    {
        if (!techLoaded || !isHost || stored == null || stored.Count == 0)
            return 0;
        Dictionary<string, string> updates = Updates(techLoaded, isHost, stored, board.Read());
        if (updates.Count == 0)
            return 0;
        board.Write(updates);
        logInfo($"{RestoredInfo} ({updates.Count} of {stored.Count} bundles).");
        return updates.Count;
    }

    /// <summary>The fields TLY owns (everything before the display name), padded to the same count:
    /// the game resizes every live value to seven fields on read, while a raw value may have five.</summary>
    public static string OwnedFields(string value)
    {
        string[] fields = (value ?? "").Split('/');
        var owned = new string[EngineManifestCheck.DisplayNameField];
        for (int i = 0; i < owned.Length; i++)
            owned[i] = i < fields.Length ? fields[i] : "";
        return string.Join("/", owned);
    }

    private static Dictionary<string, string> ToDictionary(IReadOnlyDictionary<string, string> source)
    {
        var copy = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (KeyValuePair<string, string> pair in source)
            copy[pair.Key] = pair.Value;
        return copy;
    }
}
