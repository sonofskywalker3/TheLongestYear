using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarmonyLib;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Menus;
using StardewValley.Network;
using StardewValley.Quests;
using TheLongestYear.Core;
using TheLongestYear.Core.Availability;
using TheLongestYear.Donations;
using TheLongestYear.Integration;
using TheLongestYear.Loop;
using TheLongestYear.UI;

namespace TheLongestYear
{
    public sealed partial class ModEntry
    {
        /// <summary>Reset only if the loaded save's farmer name matches the argument. Used by the
        /// debug-command-file bridge to queue a "reset on next load of save X" without affecting
        /// other saves (e.g. write 'tly_resetif puffpuff' before exit, then loading puffpuff
        /// resets it but loading any other save is a no-op).</summary>
        private void ResetIfNameMatches(string command, string[] args)
        {
            if (!Context.IsWorldReady)
            {
                this.Monitor.Log("Load a save first.", LogLevel.Warn);
                return;
            }

            if (args.Length < 1)
            {
                this.Monitor.Log("Usage: tly_resetif <farmerName>", LogLevel.Warn);
                return;
            }

            string target = args[0];
            string current = Game1.player?.Name ?? "";
            if (!string.Equals(current, target, StringComparison.OrdinalIgnoreCase))
            {
                this.Monitor.Log(
                    $"tly_resetif: current save is '{current}', not '{target}'. Skipping reset.",
                    LogLevel.Info);
                return;
            }

            this.Monitor.Log($"tly_resetif: name matches '{target}', resetting.", LogLevel.Info);
            FullResetAndPresentOffer();
        }

        private void ForceReset(string command, string[] args)
        {
            if (!Context.IsWorldReady)
            {
                this.Monitor.Log("Load a save first.", LogLevel.Warn);
                return;
            }

            int? pin = null;
            if (args.Length > 0)
            {
                if (!int.TryParse(args[0], out int seedLoop) || seedLoop < 0)
                {
                    this.Monitor.Log($"tly_reset: '{args[0]}' is not a seed loop. Usage: tly_reset [seedLoop]", LogLevel.Warn);
                    return;
                }
                pin = seedLoop;
            }

            // The pin mutates MetaState, so it must not run when the reset itself cannot: a missing
            // run controller used to leave the save carrying a pinned seed loop for a reset that
            // never happened.
            if (_runController == null)
            {
                this.Monitor.Log("Reset unavailable: no run controller (load a save first).", LogLevel.Warn);
                return;
            }

            if (pin.HasValue) PinSeedLoopForNextReset(pin.Value);

            FullResetAndPresentOffer();
        }

        /// <summary>Debug: force the NEXT reset onto a chosen bundle seed loop, so two runs can
        /// be played on the same board (same number <c>tly_genbundles</c> takes).
        ///
        /// The pin has to survive <see cref="BundleHold.ConsumeChoiceAtReset"/>, which otherwise
        /// snaps BundleSeedLoop back to the post-bump CompletedResets for any reset that skipped
        /// the Fail-night hold question, which is exactly the console path.  Stamping
        /// HoldChoiceMadeForReset makes that call a no-op, so the pin stands.
        /// ConsecutiveHolds is zeroed too: this is a debug pin, not a paid hold.</summary>
        private void PinSeedLoopForNextReset(int seedLoop)
        {
            TheLongestYear.Core.MetaState state = _meta.State;
            state.BundleSeedLoop = seedLoop;
            state.ConsecutiveHolds = 0;
            state.HoldChoiceMadeForReset = true;
            this.Monitor.Log(
                $"tly_reset: pinned bundle seed loop {seedLoop} for this reset (consecutive holds zeroed).",
                LogLevel.Info);
            if (TheLongestYear.Core.BundleSourceNames.IsVanilla(state.BundleSource))
                this.Monitor.Log(
                    "tly_reset: this save runs a vanilla board, which regenerates through loadForNewGame and never reads the seed loop, so the pin will not change the board.",
                    LogLevel.Warn);
        }

        /// <summary>Debug: simulate a day-28 gate-miss reset (shrine-spend → reset → persist),
        /// the natural loop-boundary path. See <see cref="RunController.DebugForceFailReset"/>.</summary>
        private void CmdFailReset(string command, string[] args)
        {
            if (!Context.IsWorldReady)
            {
                this.Monitor.Log("Load a save first.", LogLevel.Warn);
                return;
            }

            _runController?.DebugForceFailReset();
        }

        /// <summary>Debug: the shrine's Restart the year button without the mouse.</summary>
        private void CmdRestart(string command, string[] args)
        {
            if (!Context.IsWorldReady) { this.Monitor.Log("Load a save first.", LogLevel.Warn); return; }
            _runController?.AskVoluntaryRestart();
        }

        private void CmdForceWin(string command, string[] args)
        {
            if (!Context.IsWorldReady)
            {
                this.Monitor.Log("Load a save first.", LogLevel.Warn);
                return;
            }

            _runController?.DebugForceWin();
        }

        /// <summary>Full reset: rebuild the world (PerformReset), wipe RunState (BeginNewRun),
        /// and fire the Spring 1 hub. Used by both <see cref="ForceReset"/> and
        /// <see cref="ResetIfNameMatches"/>.
        ///
        /// 2026-05-26 round-2 bug: log showed a deferred SaveLoaded event firing AFTER this
        /// method returned, which called <c>_meta.Load()</c> and overwrote our in-memory
        /// BeginNewRun with the stale on-disk state ("the reset didn't remove the foraging
        /// items I had donated"). Fix: commit the cleared state to disk immediately after
        /// BeginNewRun so the subsequent SaveLoaded's Load reads the post-reset state.</summary>
        private void FullResetAndPresentOffer()
        {
            // Tech-debt consolidation (2026-06-10): the debug reset is now a thin alias for THE
            // shared finalizer (RunController.FinalizeReset) instead of a hand-copied subset. That
            // makes tly_reset a faithful stand-in for the real fail-day-28 reset (it previously
            // skipped ActiveEffectsProvider.Clear — leaking the old theme's effects — and
            // ForceFullSave, and presented the offer via PresentOffer(1) instead of the real
            // day-start flow). Cross-cutting reset fixes now land once, in FinalizeReset.
            if (_runController == null)
            {
                this.Monitor.Log("Reset unavailable: no run controller (load a save first).", LogLevel.Warn);
                return;
            }
            // A planning hub still up when tly_reset fires survives the in-place reset and then
            // blocks the new run's week-1 offer for good: the hub only opens over a clear
            // activeClickableMenu, and a stale hub never closes itself (readyToClose waits for a
            // pick). Drop it here; the reset re-presents week 1 from the real day-start flow.
            if (Game1.activeClickableMenu is TheLongestYear.UI.WeeklyHubMenu)
            {
                this.Monitor.Log("tly_reset: closing the open planning hub so the new run's week-1 offer can open.", LogLevel.Info);
                Game1.exitActiveMenu();
            }
            _runController.FinalizeReset("debug tly_reset");
        }

        private void LeakTest(string command, string[] args)
        {
            if (!Context.IsWorldReady)
            {
                this.Monitor.Log("Load a save first.", LogLevel.Warn);
                return;
            }

            _reset.PerformReset();
            _reset.ProfessionPicker.DrainOnDayStart();
            var first = WorldStateProbe.Capture();

            _reset.PerformReset();
            _reset.ProfessionPicker.DrainOnDayStart();
            var second = WorldStateProbe.Capture();

            this.Monitor.Log(
                $"Leak test object counts (informational, non-deterministic world-gen): {first.PlacedObjectCount} vs {second.PlacedObjectCount}.",
                LogLevel.Info);

            var diff = first.Diff(second);
            if (diff.Count == 0)
            {
                this.Monitor.Log("Leak test PASSED: two consecutive resets produced an identical baseline.", LogLevel.Info);
            }
            else
            {
                this.Monitor.Log($"Leak test FAILED: {diff.Count} field(s) leaked between runs:", LogLevel.Error);
                foreach (string d in diff)
                    this.Monitor.Log($"  - {d}", LogLevel.Error);
            }
        }
    }
}
