using System.Collections.Generic;
using StardewModdingAPI;
using TheLongestYear.Core;
using TheLongestYear.Integration;

namespace TheLongestYear.Loop
{
    /// <summary>The loop recap (spec 2026-11-10 shrine planning, feature 2 and amendment E): at the
    /// rewind, after the hold question and before the shrine, one page sums up the loop that is
    /// ending. The summary is snapshotted once and shared with the shrine's suggested rows. Never
    /// after a win and never at the year-two wall: only the rewind chain calls these.</summary>
    internal sealed partial class RunController
    {
        /// <summary>The no-hold branch of the rewind chain (a vanilla board): snapshot, then the recap.</summary>
        private void ShowLoopRecap(bool held)
        {
            SnapshotOutgoingLoop(held);
            OpenLoopRecapThenShrine();
        }

        /// <summary>Read the outgoing loop once, before anything resets it. A failure here must never
        /// strand the rewind: it is logged and the chain goes on without a recap.</summary>
        private void SnapshotOutgoingLoop(bool held)
        {
            try
            {
                // The shrine reconciles Vault payments when it opens; the recap comes first now.
                VaultPaymentSync.Reconcile(Run);
                IReadOnlyList<BundleProgress> board = OutgoingLoopReader.ReadBoard(Run);
                RarityThresholds thresholds = _config.RarityThresholds;
                IReadOnlyList<KeepSuggestion> suggestions = KeepSuggestions.For(
                    board, held, _store.State, OutgoingLoopReader.KindOf,
                    id => OutgoingLoopReader.IsRareFish(id, thresholds), RunReachEvaluator.Meets,
                    OutgoingLoopReader.DisplayName);
                LoopSummary summary = LoopSummary.Build(Run, board, held, suggestions);
                _store.OutgoingLoop = summary;
                _monitor.Log(
                    $"Loop {summary.LoopNumber} recap: {summary.Earned.Total()} JP earned{(summary.Earned.SinceUpdate ? " (since the update)" : "")} " +
                    $"[bundles {summary.Earned.Bundles}, rooms {summary.Earned.Rooms}, weekly {summary.Earned.WeeklyThemes}, " +
                    $"shrine goals {summary.Earned.ShrineGoals}, checkpoints {summary.Earned.Checkpoints}]; " +
                    $"bundles {summary.BundlesDone}/{summary.BundlesTotal}, {summary.DonationsStillNeeded} donation(s) still needed; " +
                    $"tampers {summary.Tampers}, blight nights {summary.BlightNights}; held board {held}; " +
                    $"suggested [{string.Join(", ", System.Linq.Enumerable.Select(suggestions, s => $"{s.UpgradeId} ({s.Kind} x{s.Count})"))}].",
                    LogLevel.Info);
            }
            catch (System.Exception ex)
            {
                _store.OutgoingLoop = null;
                _monitor.Log($"Loop recap: the outgoing loop could not be read; the rewind goes on without it.\n{ex}", LogLevel.Error);
            }
        }

        /// <summary>Open the recap; when it closes (or cannot open), queue the shrine the way the
        /// hold choice always has (<see cref="DeferShrineThenContinue"/>).</summary>
        private void OpenLoopRecapThenShrine()
        {
            LoopSummary summary = _store.OutgoingLoop;
            System.Action toShrine = () => DeferShrineThenContinue(ContinueAfterResetSpend, focusRows: true);
            if (summary == null)
            {
                toShrine();
                return;
            }
            WatchRewindMenu(
                "Loop recap",
                $"loop {summary.LoopNumber}",
                () => _launcher?.OpenLoopRecap(summary),
                menu => menu is TheLongestYear.UI.LoopRecapMenu,
                toShrine);
        }
    }
}
