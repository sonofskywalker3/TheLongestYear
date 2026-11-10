using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core;

/// <summary>The outgoing loop, snapshotted once at the rewind before the reset (spec 2026-11-10
/// shrine planning, amendments A, B, E). Immutable: the recap page and the shop's suggested rows
/// read the same snapshot.</summary>
public sealed class LoopSummary
{
    public int LoopNumber { get; }
    public JpLedger Earned { get; }
    public int BundlesDone { get; }
    public int BundlesTotal { get; }
    public int DonationsStillNeeded { get; }
    public int Tampers { get; }
    public int BlightNights { get; }

    /// <summary>The player kept this board for the next loop, so the reasons read it as the coming board.</summary>
    public bool HeldBoard { get; }

    public IReadOnlyList<KeepSuggestion> Suggestions { get; }

    public LoopSummary(int loopNumber, JpLedger earned, int bundlesDone, int bundlesTotal, int donationsStillNeeded,
        int tampers, int blightNights, bool heldBoard, IReadOnlyList<KeepSuggestion> suggestions)
    {
        LoopNumber = loopNumber;
        Earned = (earned ?? throw new ArgumentNullException(nameof(earned))).Copy();
        BundlesDone = bundlesDone;
        BundlesTotal = bundlesTotal;
        DonationsStillNeeded = donationsStillNeeded;
        Tampers = tampers;
        BlightNights = blightNights;
        HeldBoard = heldBoard;
        Suggestions = (suggestions ?? Array.Empty<KeepSuggestion>()).ToList();
    }

    /// <summary>The suggestion for this keep id, or null.</summary>
    public KeepSuggestion? SuggestionFor(string? upgradeId)
        => upgradeId == null ? null : Suggestions.FirstOrDefault(s => s.UpgradeId == upgradeId);

    /// <summary>Position of the keep in the suggestions (0 = best), or -1.</summary>
    public int RankOf(string? upgradeId)
    {
        for (int i = 0; i < Suggestions.Count; i++)
            if (Suggestions[i].UpgradeId == upgradeId) return i;
        return -1;
    }

    /// <summary>The summary of <paramref name="run"/> ending on <paramref name="board"/>.</summary>
    public static LoopSummary Build(RunState run, IReadOnlyList<BundleProgress> board, bool heldBoard,
        IReadOnlyList<KeepSuggestion> suggestions)
    {
        if (run == null) throw new ArgumentNullException(nameof(run));
        if (board == null) throw new ArgumentNullException(nameof(board));
        return new LoopSummary(
            run.RunNumber,
            run.JpEarnedSnapshot(),
            board.Count(b => b.Done),
            board.Count,
            board.Sum(b => b.StillNeeded),
            run.Tampers?.Count ?? 0,
            run.LoopBlightNights,
            heldBoard,
            suggestions);
    }
}
