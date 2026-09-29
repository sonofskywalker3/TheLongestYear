using StardewModdingAPI;
using StardewValley;
using StardewValley.Locations;
using TheLongestYear.Core;
using TheLongestYear.Core.Day28;

namespace TheLongestYear.Loop
{
    /// <summary>Voluntary restart at the Junimo Shrine (spec
    /// docs/superpowers/specs/2026-09-24-voluntary-restart-design.md). The shrine's button asks a
    /// vanilla yes/no question. Yes runs the Fail chain's menus right away, on the same day (hold,
    /// upgrade menu, books), then queues <see cref="Day28Branch.Restart"/> with
    /// <see cref="RunState.RestartMenusDone"/> and ends the day. That night <see cref="OnDayEnding"/>
    /// skips the gate and the shipping and level-up screens; in the morning the Day28CutsceneDriver
    /// skips the scene and calls <see cref="OnCutsceneEnded"/>, which resets at once. Before
    /// 0.18.98 the menus ran in the morning, after the payout, level-ups and the new date (Jeff,
    /// 2026-09-29, from Ben's stream).</summary>
    internal sealed partial class RunController
    {
        /// <summary>True while any part of a rewind or win chain is queued or waiting on a menu.</summary>
        public bool IsRewindChainRunning =>
            _pendingCutscene != Day28Branch.None
            || _shrineOpenPending != null
            || _menuWatch != null
            || _holdReaskPending
            || _restartChainPending
            || _restartChainBeforeNight
            || _restartSleepPending;

        private RestartSituation RestartSituationNow() => new(
            DayOfMonth: Game1.dayOfMonth,
            EventUp: Game1.eventUp || Game1.CurrentEvent != null || Game1.farmEvent != null || Game1.isFestival(),
            // An armed Year One Ending owns the next morning (or this one, until the event plays).
            ResetRunning: IsRewindChainRunning || Game1.newDay || Run.EndingArmed);

        /// <summary>Why the shrine's Restart the year button is hidden right now (None = shown).</summary>
        public RestartBlock VoluntaryRestartBlock() => VoluntaryRestart.BlockedBy(RestartSituationNow());

        /// <summary>True once SMAPI's DayStarted has run (and early-returned) while a day-28 branch
        /// is pending. The Day28CutsceneDriver's Restart branch waits for it, so the chain never
        /// starts before this morning's DayStarted: had it, OnCutsceneEnded would clear the pending
        /// branch first and a late DayStarted would run DoDayStartSeasonAndHub mid-chain. Set in
        /// <see cref="OnDayStarted"/>; cleared when the chain starts and on the next day end.</summary>
        public bool DayStartedWhileBranchPending => _dayStartedWhileBranchPending;

        private bool _dayStartedWhileBranchPending;

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
                // No: back to the shrine view the button was pressed from, but NOT from inside this
                // callback. Answering with Escape, N or the controller's B goes through
                // DialogueBox.receiveKeyPress, which (unlike the click path) never clears
                // Game1.dialogueUp; only the box's outro does, via closeDialogue, and that runs only
                // while the box is still the active menu. Replacing the box here would skip it and
                // leave dialogueUp stuck true (no pause menu, journal, inventory or tools).
                // TickRestartDeclined opens the shrine once the outro has closed the box.
                _restartDeclinedShrinePending = true;
                _monitor.Log("Voluntary restart: the player chose No. Nothing changed; the Junimo Shrine reopens once the question closes.", LogLevel.Info);
            });
            _monitor.Log("Voluntary restart: confirm opened.", LogLevel.Info);
        }

        /// <summary>Set by the question's No answer; drained by <see cref="TickRestartDeclined"/>.</summary>
        private bool _restartDeclinedShrinePending;

        /// <summary>Polled every tick (from <see cref="TickShrineWatchdog"/>). Reopens the Junimo
        /// Shrine after a No, once the question box's outro has closed it: no menu up and
        /// <c>Game1.dialogueUp</c> cleared (closeDialogue resets it and CanMove on every input
        /// path). Same deferral shape as <see cref="DeferShrineThenContinue"/>.</summary>
        private void TickRestartDeclined()
        {
            if (!_restartDeclinedShrinePending) return;
            if (Game1.activeClickableMenu != null || Game1.dialogueUp || Game1.eventUp) return;
            _restartDeclinedShrinePending = false;
            if (TheLongestYear.UI.PlanningShrineService.OpenMenu())
                _monitor.Log("Voluntary restart: back to the Junimo Shrine after No.", LogLevel.Info);
            else
                _monitor.Log("Voluntary restart: the shrine could not reopen after No (no save state attached).", LogLevel.Warn);
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
                $"(run {Run.RunNumber}, {_store.State.JunimoPoints} JP banked). Hold -> upgrade menu -> books now, then the night.",
                LogLevel.Info);
            // The menus run BEFORE the night (Jeff, 2026-09-29): sleeping first showed the shipping
            // payout, level-ups and the next day's date before the bundle question. Not from inside
            // this answer callback, though: the question box is still the active menu (see the No
            // branch). TickVoluntaryRestart starts the chain once the box has closed.
            _restartChainPending = true;
        }

        /// <summary>Yes was answered; the chain starts once the question box has closed.</summary>
        private bool _restartChainPending;

        /// <summary>The rewind chain running now belongs to a voluntary restart, so its end puts
        /// the farmer to sleep (<see cref="FinishRewindChain"/>) instead of resetting mid-day.</summary>
        private bool _restartChainBeforeNight;

        /// <summary>The chain's menus are done; the night starts once the last menu has closed.</summary>
        private bool _restartSleepPending;

        /// <summary>Polled every tick (from <see cref="TickShrineWatchdog"/>): starts the restart's
        /// menus after Yes, and ends the day after them, each once no menu or dialogue is up.</summary>
        private void TickVoluntaryRestart()
        {
            if (!_restartChainPending && !_restartSleepPending) return;
            if (Game1.activeClickableMenu != null || Game1.dialogueUp || Game1.eventUp) return;
            if (_restartChainPending)
            {
                _restartChainPending = false;
                _restartChainBeforeNight = true;
                StartRewindChain();
                return;
            }
            _restartSleepPending = false;
            Run.RestartMenusDone = true;
            _monitor.Log("Voluntary restart: menus done; ending the day. The reset runs as soon as the morning starts.", LogLevel.Info);
            // The Day28CutsceneDriver runs a pending Restart on its next clear tick once DayStarted
            // has run (DayStartedWhileBranchPending), so Game1.newDay must already be true by then or
            // the flag must be set here for the mid-day fallback.
            _pendingCutscene = Day28Branch.Restart;
            _dayStartedWhileBranchPending = false;
            Game1.displayHUD = false;
            EndDayNow();
            // The sleep did not take: no night, so no DayStarted will come to release the driver.
            if (!Game1.newDay)
                _dayStartedWhileBranchPending = true;
        }

        /// <summary>The last step of the rewind chain (after the books). A voluntary restart's
        /// chain runs before the night, so it ends the day; every other chain resets now.</summary>
        private void FinishRewindChain()
        {
            if (_restartChainBeforeNight)
            {
                _restartChainBeforeNight = false;
                _restartSleepPending = true;
                return;
            }
            FinalizeReset("shrine closed");
        }

        /// <summary>The restart night (<see cref="OnDayEnding"/>): the reset wipes the gold, the
        /// skills and the shipped items anyway, so skip the shipping payout screen and the level-up
        /// screens instead of showing them right before the rewind. Kept skills still get their
        /// profession pickers after the reset (ProfessionPickerScheduler).</summary>
        private void QuietRestartNight()
        {
            Farmer player = Game1.player;
            if (player == null) return;
            var bin = Game1.getFarm()?.getShippingBin(player);
            int shipped = bin?.Count ?? 0;
            bin?.Clear();
            int levels = player.newLevels.Count;
            player.newLevels.Clear();
            _monitor.Log(
                $"Voluntary restart night: skipped the shipping screen ({shipped} stack(s) in the bin) and {levels} level-up screen(s).",
                LogLevel.Info);
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

            // doSleep recorded the statue as the sleep spot. Point it at the real bed: the morning's
            // BedFurniture.ApplyWakeUpPosition (called from Game1._newDayAfterFade) reads
            // lastSleepLocation/lastSleepPoint while sleptInTemporaryBed is set, so the player wakes
            // in the farmhouse bed on the live morning, and a quit after tonight's save reloads
            // there too (SaveGame load reads the same fields). mostRecentBed is the bed that save load
            // and the send-home-to-bed events use, so it points at the same bed.
            FarmHouse home = Utility.getHomeOfFarmer(player);
            if (home != null)
            {
                Microsoft.Xna.Framework.Point bed = home.GetPlayerBedSpot();
                player.lastSleepLocation.Value = home.NameOrUniqueName;
                player.lastSleepPoint.Value = bed;
                player.mostRecentBed = Utility.PointToVector2(bed) * 64f;
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
