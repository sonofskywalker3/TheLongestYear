using StardewModdingAPI;
using StardewValley;
using StardewValley.Locations;
using TheLongestYear.Core;
using TheLongestYear.Core.Day28;

namespace TheLongestYear.Loop
{
    /// <summary>Voluntary restart at the Junimo Shrine (spec
    /// docs/superpowers/specs/2026-09-24-voluntary-restart-design.md). The shrine's button asks a
    /// vanilla yes/no question; Yes queues <see cref="Day28Branch.Restart"/> and ends the day at
    /// once. That night <see cref="OnDayEnding"/> skips the gate; in the morning the
    /// Day28CutsceneDriver skips the scene and calls <see cref="OnCutsceneEnded"/>, which runs the
    /// Fail chain (hold, upgrade menu, books, reset).</summary>
    internal sealed partial class RunController
    {
        /// <summary>True while any part of a rewind or win chain is queued or waiting on a menu.</summary>
        public bool IsRewindChainRunning =>
            _pendingCutscene != Day28Branch.None
            || _shrineOpenPending != null
            || _menuWatch != null
            || _holdReaskPending;

        private RestartSituation RestartSituationNow() => new(
            DayOfMonth: Game1.dayOfMonth,
            EventUp: Game1.eventUp || Game1.CurrentEvent != null || Game1.farmEvent != null || Game1.isFestival(),
            ResetRunning: IsRewindChainRunning || Game1.newDay);

        /// <summary>Why the shrine's Restart the year button is hidden right now (None = shown).</summary>
        public RestartBlock VoluntaryRestartBlock() => VoluntaryRestart.BlockedBy(RestartSituationNow());

        public bool IsVoluntaryRestartOffered() => VoluntaryRestartBlock() == RestartBlock.None;

        /// <summary>The button's action (and <c>tly_restart</c>): ask the vanilla yes/no question.
        /// No changes nothing and returns the player to the Junimo Shrine view, opened the same way
        /// the statue opens it (<see cref="TheLongestYear.UI.PlanningShrineService.OpenMenu"/>).</summary>
        public void AskVoluntaryRestart()
        {
            RestartBlock block = VoluntaryRestartBlock();
            if (block != RestartBlock.None)
            {
                _monitor.Log($"Voluntary restart not offered right now ({block}).", LogLevel.Info);
                return;
            }
            GameLocation loc = Game1.currentLocation ?? Game1.player?.currentLocation;
            if (loc == null)
            {
                _monitor.Log("Voluntary restart: no current location to host the question; nothing changed.", LogLevel.Warn);
                return;
            }
            loc.createQuestionDialogue(Strings.Get("dialog.restart.prompt"), loc.createYesNoResponses(), (Farmer who, string key) =>
            {
                if (key == "Yes")
                {
                    BeginVoluntaryRestart();
                    return;
                }
                // No: back to the shrine view the button was pressed from. Opening a menu from a
                // question answer is what vanilla does too: the DialogueBox only closes itself while
                // it is still the active menu, so the shrine replaces it cleanly.
                if (TheLongestYear.UI.PlanningShrineService.OpenMenu())
                    _monitor.Log("Voluntary restart: the player chose No. Nothing changed; back to the Junimo Shrine.", LogLevel.Info);
                else
                    _monitor.Log("Voluntary restart: the player chose No. Nothing changed; the shrine could not reopen (no save state attached).", LogLevel.Warn);
            });
            _monitor.Log("Voluntary restart: confirm opened.", LogLevel.Info);
        }

        /// <summary>Yes: queue the Restart branch and end the day. Runs inside the question's
        /// answer callback, the same place vanilla's bed question runs its own sleep.</summary>
        private void BeginVoluntaryRestart()
        {
            // Re-check: the answer arrives after the question opened. The question box itself is a
            // menu, not an event, so the snapshot is still honest.
            RestartBlock block = VoluntaryRestartBlock();
            if (block != RestartBlock.None)
            {
                _monitor.Log($"Voluntary restart: confirmed, but no longer allowed ({block}); nothing changed.", LogLevel.Warn);
                return;
            }
            _monitor.Log(
                $"Voluntary restart confirmed on {Game1.season} {Game1.dayOfMonth} at {Game1.timeOfDay} " +
                $"(run {Run.RunNumber}, {_store.State.JunimoPoints} JP banked). Ending the day now.",
                LogLevel.Info);
            // Same call, same tick: the Day28CutsceneDriver runs a pending Restart on its next clear
            // tick with no "the day has ended" guard, so Game1.newDay must already be true by then.
            _pendingCutscene = Day28Branch.Restart;
            EndDayNow();
        }

        /// <summary>Put the host to sleep where they stand, with vanilla's own <c>debug sleep</c>
        /// recipe (DebugCommands.Sleep): <c>Game1.NewDay</c> only fades to black, and so only ever
        /// reaches newDayAfterFade, when <c>isInBed</c> is true, and Farmer.Update re-derives
        /// <c>isInBed</c> every tick unless <c>sleptInTemporaryBed</c> is set. "Sleep_Yes" is the
        /// bed question's answer: startSleep, then doSleep, then NewDay. No pass-out penalty: that
        /// lives only in Farmer.performPassoutWarp. SaveGame.Save clears sleptInTemporaryBed.</summary>
        private void EndDayNow()
        {
            Farmer player = Game1.player;
            GameLocation here = Game1.currentLocation ?? player.currentLocation;
            player.isInBed.Value = true;
            player.sleptInTemporaryBed.Value = true;
            here.answerDialogueAction("Sleep_Yes", null);

            // doSleep recorded the statue as the sleep spot. Point it at the real bed so a quit
            // after tonight's save reloads the player in the farmhouse (SaveGame load reads it).
            FarmHouse home = Utility.getHomeOfFarmer(player);
            if (home != null)
            {
                player.lastSleepLocation.Value = home.NameOrUniqueName;
                player.lastSleepPoint.Value = home.GetPlayerBedSpot();
            }

            if (Game1.newDay)
                _monitor.Log("Voluntary restart: the day is ending.", LogLevel.Info);
            else
                _monitor.Log(
                    "Voluntary restart: the game did not start a new day. The chain will run right away, mid-day, " +
                    "once the question box closes.",
                    LogLevel.Warn);
        }
    }
}
