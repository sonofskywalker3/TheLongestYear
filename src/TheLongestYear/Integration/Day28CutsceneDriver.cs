using System;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Menus;
using TheLongestYear.Core;
using TheLongestYear.Core.Day28;
using TheLongestYear.Loop;
using TheLongestYear.UI;

namespace TheLongestYear.Integration
{
    /// <summary>
    /// Opens the day-28 bedtime Junimo cutscene when <see cref="RunController.PendingCutscene"/> is
    /// set, on a settled frame (see the timing comment inside <see cref="OnUpdateTicked"/>: that
    /// window is load-bearing and untouched by the rewind sequence below). CONTINUE opens the
    /// self-drawn <see cref="Day28CutsceneMenu"/> (not a vanilla Event, see that class for why),
    /// unchanged. FAIL instead opens the rewind sequence (spec 2026-09-11-rewind-cutscene): the
    /// bedroom (<see cref="RewindBedroomScene"/>, beats 1-9) hands off to the Town pan
    /// (<see cref="RewindPanScene"/>, beat 10), which hands off to <see cref="RewindSpringPaint"/> and
    /// the farmhouse morning beat (<see cref="RewindMorningScene"/>, beats 11-12). Every hand-off in
    /// between is this driver's own job (<see cref="OnRewindBedroomComplete"/>,
    /// <see cref="OnRewindPanComplete"/>, <see cref="OpenRewindMorningBeat"/>). Either branch's last
    /// step runs <see cref="RunController.OnCutsceneEnded"/> (FAIL → shop+reset, CONTINUE → next
    /// season). Re-arm is implicit: OnCutsceneEnded clears PendingCutscene, so the next pending
    /// episode (including the mid-day <c>tly_failreset</c> debug path) opens a fresh sequence.
    /// </summary>
    internal sealed class Day28CutsceneDriver
    {
        private readonly IMonitor _monitor;
        private Func<RunController> _runController;
        private Func<SeasonTurnDriver> _turnDriver;
        private bool _opened;
        private IClickableMenu _openedMenu;
        private bool _farmEventDeferLogged;
        // I5: the morning beat (beats 11-12) is waiting for something else's menu to close before it
        // opens. See OpenRewindMorningBeat.
        private bool _pendingMorningBeat;
        private bool _morningDeferLogged;
        // How many times the Town pan has ended abnormally for THIS pending episode. See
        // OnRewindPanAborted: without a cap, a deterministic exception in the pan loops bedroom to
        // pan to throw to re-arm forever.
        private int _panAborts;

        /// <summary>Aborted pans tolerated before the driver stops replaying the scene and just runs
        /// the reset. One retry covers a transient failure; a second is a deterministic one.</summary>
        private const int MaxPanAborts = 2;

        public Day28CutsceneDriver(IMonitor monitor)
        {
            _monitor = monitor;
        }

        /// <summary>Subscribe once (from ModEntry.Entry). The RunController is built later on save
        /// load, so it's resolved through a thunk, the same pattern as the intro driver's launcher.</summary>
        public void Attach(IModHelper helper, Func<RunController> runController, Func<SeasonTurnDriver> turnDriver = null)
        {
            _runController = runController;
            _turnDriver = turnDriver;
            helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
        }

        private void OnUpdateTicked(object sender, UpdateTickedEventArgs e)
        {
            if (!TheLongestYear.Core.RunActivation.IsActive) return; // dormant on non-TLY saves
            RunController rc = _runController?.Invoke();
            if (rc == null) return;
            rc.TickShrineWatchdog();

            if (rc.PendingCutscene == Day28Branch.None)
            {
                _opened = false; // idle / re-arm for the next pending episode
                _farmEventDeferLogged = false;
                _pendingMorningBeat = false;
                _morningDeferLogged = false;
                _panAborts = 0;
                return;
            }

            if (_opened)
            {
                // I5: the pan finished onto somebody else's menu and the morning beat is queued
                // behind it. Poll the same surface the initial open waits on rather than replacing
                // whatever is there. The Spring 1 paint is already holding, so the deferred frames
                // look right; only the Junimo beat is waiting.
                if (_pendingMorningBeat)
                {
                    if (Game1.activeClickableMenu != null || Game1.eventUp || Game1.farmEvent != null) return;
                    OpenRewindMorningBeat();
                    return;
                }
                // Watchdog: our scene is gone but the branch is still pending, so its completion
                // callback never ran, so something replaced activeClickableMenu underneath us (vanilla's
                // showEndOfNightStuff → SaveGameMenu after a FarmEvent is the known case; the owl event
                // pauses on the exact tick that opens the window, Nexus post faldans 2026-08-11). Re-arm
                // so the scene reopens once the surface is clear instead of stranding the loop on a
                // morning that never resets.
                if (_openedMenu != null && !ReferenceEquals(Game1.activeClickableMenu, _openedMenu))
                {
                    _monitor.Log(
                        $"Day-28 cutscene: the {rc.PendingCutscene} scene was replaced by " +
                        $"{Game1.activeClickableMenu?.GetType().Name ?? "nothing"} before it finished; re-arming.",
                        LogLevel.Warn);
                    _opened = false;
                    _openedMenu = null;
                }
                return;                                            // our menu is up (or its continuation)
            }
            if (!Context.IsWorldReady || Game1.currentMinigame != null) return;
            // Open as soon as the night-save / new-day sequence is done, but WHILE the wake-up fade
            // is still dark, so the black cutscene takes over before the farmhouse fades into view
            // (2026-06-03 playtest: "it loads the new day, then blanks to the message"). A menu,
            // unlike a vanilla event, doesn't fight the engine's player placement, so we don't need
            // to wait for the fade to settle.
            //
            // CRUCIAL: also wait out any overnight FarmEvent (e.g. the CC bus-repair WorldChangeEvent
            // that completing the Vault queues for the night). A FarmEvent plays with newDay == false
            // AND eventUp == false but Game1.farmEvent != null (Game1.cs:9340 clears newDay BEFORE
            // 9361 assigns farmEvent), so the old newDay/eventUp-only guard let this cutscene + the JP
            // shrine open DURING the bus scene; the event's end-of-play warp (Game1.cs:4977-4989) then
            // tore the shrine down without firing its exitFunction, silently dropping the reset (#1b).
            // On a FAIL loop the scene is suppressed upstream (RunController.SuppressResetDoomedRoomScenes);
            // on a PASS loop the scene legitimately plays and this defers us cleanly behind it.
            if (Game1.newDay || Game1.eventUp || Game1.farmEvent != null)
            {
                if (Game1.farmEvent != null && !_farmEventDeferLogged)
                {
                    _monitor.Log(
                        $"Day-28 cutscene: deferring the {rc.PendingCutscene} scene until the overnight " +
                        $"FarmEvent ({Game1.farmEvent.GetType().Name}) finishes.", LogLevel.Info);
                    _farmEventDeferLogged = true;
                }
                return;                                            // let the save / new-day / FarmEvent finish first
            }
            // The post-FarmEvent warp is queued (locationRequest) until the fade completes, and
            // showEndOfNightStuff runs from that warp, so opening before it lands gets us clobbered.
            if (Game1.locationRequest != null) return;
            if (Game1.activeClickableMenu != null) return;         // don't stack on another menu

            Day28Branch branch = rc.PendingCutscene;
            if (branch == Day28Branch.Continue)
            {
                // Season Turn Beats (spec 2026-09-07): the Continue morning plays the porch scene in
                // place of the card. Its completion runs the same OnCutsceneEnded. An event, not a
                // menu, so the replaced-menu watchdog above stays off (_openedMenu null).
                SeasonTurnKind? kind = SeasonTurn.ForSeasonStart((TheLongestYear.Core.Season)(int)Game1.season);
                SeasonTurnDriver turn = _turnDriver?.Invoke();
                if (kind != null && turn != null && turn.Start(kind.Value, () => _runController?.Invoke()?.OnCutsceneEnded()))
                {
                    _openedMenu = null;
                    _opened = true;
                    return;
                }
                _monitor.Log("Day-28 cutscene: no season turn scene for this morning; showing the card.", LogLevel.Info);
                _monitor.Log(
                    $"Day-28 cutscene: opening the {branch} Junimo scene (eventUp={Game1.eventUp}, " +
                    $"farmEvent=none, newDay={Game1.newDay}).", LogLevel.Info);
                Action onComplete = () => _runController?.Invoke()?.OnCutsceneEnded();
                Game1.activeClickableMenu = new Day28CutsceneMenu(branch, onComplete);
                _openedMenu = Game1.activeClickableMenu;
                _opened = true;
                return;
            }

            // Fail: the rewind sequence (spec 2026-09-11-rewind-cutscene, task 7) --
            // RewindBedroomScene (beats 1-9) -> RewindPanScene (beat 10) -> RewindSpringPaint + the
            // morning beat (beats 11-12) -> the existing OnCutsceneEnded. The old static
            // Day28CutsceneMenu Fail card is retired; Day28CutsceneMenu is still used above, untouched,
            // for Continue.
            _monitor.Log(
                $"Day-28 cutscene: opening the Fail rewind sequence (eventUp={Game1.eventUp}, " +
                $"farmEvent=none, newDay={Game1.newDay}).", LogLevel.Info);
            // rc.CurrentSeason, not Game1.season: day 28 is a season's last day, so the overnight
            // transition has already rolled the global forward to the NEXT season by the time this
            // driver runs. The bedroom needs the season that failed, both to paint the failed night
            // over the HUD and to hand the pan its starting point (see OnRewindBedroomComplete).
            RewindSkip.Arm();
            Game1.activeClickableMenu = new RewindBedroomScene(rc.CurrentSeason, OnRewindBedroomComplete);
            _openedMenu = Game1.activeClickableMenu;
            _opened = true;
        }

        /// <summary>RewindBedroomScene's completion (beats 1-9 done). Starts the Town pan (beat 10).
        /// The pan drives itself entirely from its own UpdateTicked subscription and never touches
        /// <see cref="Game1.activeClickableMenu"/>, so <see cref="_openedMenu"/> is cleared here the
        /// same way the Continue branch's season-turn beats already clear it above: with no menu to
        /// watch, the generic steal-detector at the top of <see cref="OnUpdateTicked"/> simply stays
        /// inert for the whole pan instead of misreading its own hand-off as a steal.</summary>
        private void OnRewindBedroomComplete()
        {
            _openedMenu = null;
            RunController rc = _runController?.Invoke();
            if (rc == null)
            {
                _monitor.Log("Day-28 rewind: RunController unavailable after the bedroom scene; the pan will not start.", LogLevel.Error);
                return;
            }
            // RewindPanScene.Start needs the season that just failed, not Game1.season: by the time
            // this driver ever runs, the overnight day-transition has already rolled Game1.season to
            // the NEXT season (day 28 is a season's last day), while RunController.CurrentSeason
            // (Run.Season) still holds the failed season until the real reset lands, well after this
            // whole sequence finishes.
            RewindPanScene.Start(rc.CurrentSeason, OnRewindPanComplete, OnRewindPanAborted);
        }

        /// <summary>RewindPanScene's abnormal end (an exception mid-tick, or a quit to the title).
        /// The pan has already restored season, clock, weather, camera and the control flags to what
        /// they were before it started, so the world is back on the failed season's night frame and
        /// the sequence can simply be opened again from beat 1.
        ///
        /// This is the re-arm the pan phase otherwise has no way to get. <see cref="_openedMenu"/> is
        /// deliberately null for the whole pan (see <see cref="OnRewindBedroomComplete"/>), which
        /// switches the generic steal-detector at the top of <see cref="OnUpdateTicked"/> off, so
        /// without this callback an aborted pan left <see cref="_opened"/> true with
        /// <c>PendingCutscene</c> still Fail and nothing watching: no reset, no shrine, no Spring 1,
        /// and the failed season rolling on as though the gate had passed. PendingCutscene is not
        /// persisted either, so a reload would not have recovered it.
        ///
        /// Clearing the two fields is the whole re-arm: the next tick falls through to the normal
        /// open path, which waits out any menu/event/fade of its own accord before reopening
        /// <see cref="RewindBedroomScene"/>. If the abort was a quit to the title, that tick never
        /// comes (the driver returns early on RunActivation), and clearing them is exactly the
        /// per-save reset the next load wants anyway.</summary>
        private void OnRewindPanAborted()
        {
            _panAborts++;
            // THE CAP. Re-arming replays the sequence from beat 1, so a DETERMINISTIC failure in the
            // pan (a missing tilesheet, a bad villager, anything that throws on the same frame every
            // time) is a closed loop: bedroom, pan, throw, re-arm, bedroom, forever, with the player
            // watching the same four lines over and over and the reset never landing. One retry is
            // worth having, since a genuinely transient failure exists; a second identical one is
            // evidence the pan cannot run on this save, and the loop matters more than the scene.
            if (_panAborts >= MaxPanAborts)
            {
                _monitor.Log(
                    $"Day-28 rewind: the Town pan has ended abnormally {_panAborts} times; giving up on " +
                    "the scene and running the end-of-cutscene reset directly so the loop is not stranded.",
                    LogLevel.Error);
                _opened = true;              // nothing left to re-arm for this episode
                _openedMenu = null;
                _pendingMorningBeat = false;
                _morningDeferLogged = false;
                // The bedroom hid the mod's HUD for the sequence and nothing is going to reach the
                // blackout that would normally give it back, so hand it over here.
                RewindBlackout.Release("the pan gave up");
                RewindSkip.Stop();
                _runController?.Invoke()?.OnCutsceneEnded();
                return;
            }

            _monitor.Log(
                "Day-28 rewind: the Town pan ended abnormally and restored the world; re-arming the " +
                "sequence from the bedroom rather than stranding the Fail branch.",
                LogLevel.Warn);
            _opened = false;
            _openedMenu = null;
            _pendingMorningBeat = false;
            _morningDeferLogged = false;
        }

        /// <summary>RewindPanScene's completion (beat 10 done). Two handoffs land here:
        ///
        /// Handoff 2: RewindPanScene.Finish() deliberately leaves <see cref="Game1.freezeControls"/>,
        /// <see cref="Game1.viewportFreeze"/> and <see cref="Game1.currentLocation"/> exactly where the
        /// pan left them (frozen, on Town) on a normal finish -- restoring those is left to whoever
        /// continues the sequence. Un-freezing and switching the camera back happens FIRST, before the
        /// paint, so the camera settles on the farmhouse before the room's look is repainted over it
        /// rather than the other way around. <see cref="Game1.isDebrisWeather"/> is deliberately NOT
        /// restored here: <see cref="RewindSpringPaint.Apply"/> overwrites it unconditionally below
        /// regardless of what the pan left it at, so restoring it first would just be immediately
        /// clobbered.
        ///
        /// Handoff 3: ordering between the pan's own state and the paint. Both touch season, clock and
        /// weather globals, but there is no actual race to arbitrate: RewindPanScene.Tick() writes its
        /// own season/clock/weather for the pan's last frame and THEN calls Finish() (still inside the
        /// same synchronous call), which invokes this method, which calls
        /// <see cref="RewindSpringPaint.Apply"/> last. Apply() unconditionally overwrites
        /// Game1.season/dayOfMonth/timeOfDay and all three weather flags to the definitive Spring 1
        /// values regardless of what the pan left mid-transition -- calling it after everything the pan
        /// itself does, in the same synchronous chain, is what guarantees the paint (not some
        /// in-between rewind frame) is what's actually on screen next frame.</summary>
        private void OnRewindPanComplete()
        {
            Game1.freezeControls = false;
            Game1.viewportFreeze = false;
            // The farmer's own currentLocation never moved during the pan (RewindPanScene's own class
            // comment: "Game1.player.currentLocation stays wherever the bedroom scene left it" -- the
            // farmhouse), so this is simply switching the CAMERA's location back the same bare-
            // reassignment way the pan switched it to Town.
            Game1.currentLocation = Game1.player.currentLocation;

            RewindSpringPaint.Apply();

            OpenRewindMorningBeat();
        }

        /// <summary>Beats 11-12: the farmhouse the morning after, still asleep, the Junimos still
        /// circling but with no light aura this time (the paint owns the room's look now), one line,
        /// then the existing OnCutsceneEnded. <see cref="RewindMorningScene"/> behaves exactly like
        /// <see cref="Day28CutsceneMenu"/> and <see cref="RewindBedroomScene"/> before it: its own
        /// Finish() nulls <see cref="Game1.activeClickableMenu"/> and calls onComplete in the same
        /// synchronous step, so the generic steal-detector above stays correct without special-casing
        /// this phase (mirrors why the Continue branch's Day28CutsceneMenu hand-off doesn't need it
        /// either: PendingCutscene is cleared by OnCutsceneEnded before the driver's next tick).</summary>
        private void OpenRewindMorningBeat()
        {
            // I5: never clobber a menu that opened underneath the pan. The pan runs for thirty
            // seconds with no menu of ours up and player control frozen but the rest of the engine
            // ticking, so a SaveGameMenu, ShippingMenu or LevelUpMenu can legitimately be on screen
            // when it ends. A bare assignment to activeClickableMenu drops that menu WITHOUT running
            // its exitFunction, which is the exact bug class the open-path watchdog above was written
            // for (a torn-down menu's exitFunction is how the reset itself gets continued). Defer
            // instead: the queued check at the top of OnUpdateTicked reopens this the moment the
            // surface is clear.
            if (Game1.activeClickableMenu != null || Game1.eventUp || Game1.farmEvent != null)
            {
                _pendingMorningBeat = true;
                _opened = true;
                _openedMenu = null;   // nothing of ours to watch while we wait
                if (!_morningDeferLogged)
                {
                    _morningDeferLogged = true;
                    _monitor.Log(
                        "Day-28 rewind: the pan ended with " +
                        $"{Game1.activeClickableMenu?.GetType().Name ?? "an event"} on screen; deferring the " +
                        "morning beat until it closes rather than replacing it.",
                        LogLevel.Info);
                }
                return;
            }

            _pendingMorningBeat = false;
            _morningDeferLogged = false;
            Action onComplete = () =>
            {
                RewindSkip.MarkSeen();
                _runController?.Invoke()?.OnCutsceneEnded();
            };
            var scene = new RewindMorningScene(onComplete);
            Game1.activeClickableMenu = scene;
            _openedMenu = scene;
            _opened = true;
        }
    }
}
