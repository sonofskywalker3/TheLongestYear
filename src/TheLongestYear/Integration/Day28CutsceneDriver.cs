using System;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using TheLongestYear.Core.Day28;
using TheLongestYear.Loop;
using TheLongestYear.UI;

namespace TheLongestYear.Integration
{
    /// <summary>
    /// Opens the day-28 bedtime Junimo cutscene when <see cref="RunController.PendingCutscene"/> is
    /// set. The cutscene is a self-drawn <see cref="Day28CutsceneMenu"/> (not a vanilla Event — see
    /// that class for why), so the driver just opens it on a settled frame and the menu's completion
    /// callback runs <see cref="RunController.OnCutsceneEnded"/> (FAIL → shop+reset, CONTINUE → next
    /// season). Re-arm is implicit: OnCutsceneEnded clears PendingCutscene, so the next pending
    /// episode (including the mid-day <c>tly_failreset</c> debug path) opens a fresh menu.
    /// </summary>
    internal sealed class Day28CutsceneDriver
    {
        private readonly IMonitor _monitor;
        private Func<RunController> _runController;
        private bool _opened;

        public Day28CutsceneDriver(IMonitor monitor)
        {
            _monitor = monitor;
        }

        /// <summary>Subscribe once (from ModEntry.Entry). The RunController is built later on save
        /// load, so it's resolved through a thunk — same pattern as the intro driver's launcher.</summary>
        public void Attach(IModHelper helper, Func<RunController> runController)
        {
            _runController = runController;
            helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
        }

        private void OnUpdateTicked(object sender, UpdateTickedEventArgs e)
        {
            RunController rc = _runController?.Invoke();
            if (rc == null) return;

            if (rc.PendingCutscene == Day28Branch.None)
            {
                _opened = false; // idle / re-arm for the next pending episode
                return;
            }

            if (_opened) return;                                   // our menu is up (or its continuation)
            if (!Context.IsWorldReady || Game1.currentMinigame != null) return;
            // Open as soon as the night-save / new-day sequence is done, but WHILE the wake-up fade
            // is still dark — so the black cutscene takes over before the farmhouse fades into view
            // (2026-06-03 playtest: "it loads the new day, then blanks to the message"). A menu,
            // unlike a vanilla event, doesn't fight the engine's player placement, so we don't need
            // to wait for the fade to settle.
            if (Game1.newDay || Game1.eventUp) return;             // let the save / new-day finish first
            if (Game1.activeClickableMenu != null) return;         // don't stack on another menu

            Day28Branch branch = rc.PendingCutscene;
            _monitor.Log($"Day-28 cutscene: opening the {branch} Junimo scene.", LogLevel.Info);
            Action onComplete = () => _runController?.Invoke()?.OnCutsceneEnded();
            Game1.activeClickableMenu = branch == Day28Branch.Win
                ? new VictoryMenu(rc.CurrentRunNumber, onComplete)
                : new Day28CutsceneMenu(branch, onComplete);
            _opened = true;
        }
    }
}
