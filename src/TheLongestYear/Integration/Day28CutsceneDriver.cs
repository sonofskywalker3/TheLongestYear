using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Netcode;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Characters;
using StardewValley.Menus;
using TheLongestYear.Core;
using TheLongestYear.Core.Day28;
using TheLongestYear.Loop;
using TheLongestYear.UI;

namespace TheLongestYear.Integration
{
    /// <summary>
    /// Opens the day-28 bedtime Junimo cutscene when <see cref="RunController.PendingCutscene"/> is
    /// set, on a settled frame (see the timing comment inside <see cref="OnUpdateTicked"/> — that
    /// window is load-bearing and untouched by the rewind sequence below). CONTINUE opens the
    /// self-drawn <see cref="Day28CutsceneMenu"/> (not a vanilla Event — see that class for why),
    /// unchanged. FAIL instead opens the rewind sequence (spec 2026-09-11-rewind-cutscene): the
    /// bedroom (<see cref="RewindBedroomScene"/>, beats 1-9) hands off to the Town pan
    /// (<see cref="RewindPanScene"/>, beat 10), which hands off to <see cref="RewindSpringPaint"/> and
    /// the farmhouse morning beat (<see cref="RewindMorningScene"/>, beats 11-12) — every hand-off in
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

        public Day28CutsceneDriver(IMonitor monitor)
        {
            _monitor = monitor;
        }

        /// <summary>Subscribe once (from ModEntry.Entry). The RunController is built later on save
        /// load, so it's resolved through a thunk — same pattern as the intro driver's launcher.</summary>
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
                return;
            }

            if (_opened)
            {
                // Watchdog: our scene is gone but the branch is still pending, so its completion
                // callback never ran — something replaced activeClickableMenu underneath us (vanilla's
                // showEndOfNightStuff → SaveGameMenu after a FarmEvent is the known case; the owl event
                // pauses on the exact tick that opens the window, Nexus post faldans 2026-08-11). Re-arm
                // so the scene reopens once the surface is clear instead of stranding the loop on a
                // morning that never resets.
                if (_openedMenu != null && !ReferenceEquals(Game1.activeClickableMenu, _openedMenu))
                {
                    _monitor.Log(
                        $"Day-28 cutscene: the {rc.PendingCutscene} scene was replaced by " +
                        $"{Game1.activeClickableMenu?.GetType().Name ?? "nothing"} before it finished — re-arming.",
                        LogLevel.Warn);
                    _opened = false;
                    _openedMenu = null;
                }
                return;                                            // our menu is up (or its continuation)
            }
            if (!Context.IsWorldReady || Game1.currentMinigame != null) return;
            // Open as soon as the night-save / new-day sequence is done, but WHILE the wake-up fade
            // is still dark — so the black cutscene takes over before the farmhouse fades into view
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
            // showEndOfNightStuff runs from that warp — opening before it lands gets us clobbered.
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
            Game1.activeClickableMenu = new RewindBedroomScene(OnRewindBedroomComplete);
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
            RewindPanScene.Start(rc.CurrentSeason, OnRewindPanComplete);
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
            Action onComplete = () => _runController?.Invoke()?.OnCutsceneEnded();
            var scene = new RewindMorningScene(onComplete);
            Game1.activeClickableMenu = scene;
            _openedMenu = scene;
            _opened = true;
        }

        /// <summary>Beats 11-12 of the rewind cutscene (design 2026-09-11): the morning after the pan
        /// and the paint. Respawns the same four Junimos RewindBedroomScene tore down before the pan
        /// (its own Finish() always runs TeardownWorldState), now around the sleeping farmer in the
        /// farmhouse with no light source -- "no light aura" is the point of this beat, the room's
        /// actual look now belongs to RewindSpringPaint, not this scene. One line plays through
        /// <see cref="EndingSpeechBox"/> the same way RewindBedroomScene forwards to its own box: the
        /// box is a plain object, not the active menu, so this scene keeps ticking (driving the
        /// Junimos' idle animation, same reason as RewindBedroomScene -- Game1.shouldTimePass is false
        /// for the whole run of any non-BobberBar activeClickableMenu) behind it, and notices the box's
        /// own <c>Game1.exitActiveMenu()</c> call on its last page the same tick it happens. Not
        /// skippable, matching every other beat in this sequence.</summary>
        private sealed class RewindMorningScene : IClickableMenu
        {
            private static readonly Point[] JunimoOffsets =
            {
                new Point(-1, -1), new Point(1, -1), new Point(-1, 1), new Point(1, 1),
            };
            private const string JunimoNamePrefix = "TlyRewindMorningJunimo";
            private const string JunimoDisplayName = "Junimo";

            // Same idle-bob constants RewindBedroomScene uses for the same reason (see its own
            // AnimateJunimos remarks): vanilla's own standing-still Junimo animation, driven by hand
            // since Game1.shouldTimePass is false for this menu's whole run.
            private const int JunimoIdleFrame = 8;
            private const int JunimoIdleFrameCount = 4;
            private const float JunimoIdleFrameMs = 100f;

            private static readonly FieldInfo JunimoColourField = typeof(Junimo).GetField(
                "color", BindingFlags.Instance | BindingFlags.NonPublic);

            private readonly Action _onComplete;
            private readonly List<Junimo> _junimos = new List<Junimo>();
            private EndingSpeechBox _activeBox;
            private bool _completed;

            public RewindMorningScene(Action onComplete)
                : base(0, 0, Game1.uiViewport.Width, Game1.uiViewport.Height, showUpperRightCloseButton: false)
            {
                _onComplete = onComplete;

                Texture2D portrait;
                try { portrait = Game1.content.Load<Texture2D>("Portraits/Junimo0"); }
                catch (Exception) { portrait = null; }

                SpawnJunimos();

                string playerName = Game1.player?.Name ?? string.Empty;
                string line = Strings.Get("cutscene.rewind.morning").Replace("@", playerName);
                _activeBox = new EndingSpeechBox(portrait, new List<string> { line });
            }

            private void SpawnJunimos()
            {
                GameLocation loc = Game1.currentLocation;
                if (loc == null || Game1.player == null) return;
                Point playerTile = Game1.player.TilePoint;

                for (int i = 0; i < JunimoOffsets.Length; i++)
                {
                    Vector2 worldPos = new Vector2(playerTile.X + JunimoOffsets[i].X, playerTile.Y + JunimoOffsets[i].Y) * 64f;
                    var junimo = new Junimo(worldPos, -1, temporary: true)
                    {
                        Name = JunimoNamePrefix + i,
                        displayName = JunimoDisplayName,
                        EventActor = true,
                        currentLocation = loc,
                    };
                    junimo.stayPut.Value = true;
                    if (JunimoColourField?.GetValue(junimo) is NetColor net)
                        net.Value = JunimoPalette.Get(i);
                    loc.characters.Add(junimo);
                    _junimos.Add(junimo);
                }
                // Deliberately no LightSource here, unlike RewindBedroomScene's beats 2-9: this beat's
                // whole point is the aura is gone.
            }

            private void TeardownWorldState()
            {
                GameLocation loc = Game1.currentLocation;
                foreach (Junimo j in _junimos)
                    loc?.characters.Remove(j);
                _junimos.Clear();
            }

            private void AnimateJunimos(GameTime time)
            {
                foreach (Junimo j in _junimos)
                    j.Sprite?.Animate(time, JunimoIdleFrame, JunimoIdleFrameCount, JunimoIdleFrameMs);
            }

            /// <summary>Same trick RewindBedroomScene.ForwardToBox uses: the box isn't the active menu
            /// (we are), so its own Game1.exitActiveMenu() call on the last page just clears the static
            /// field; noticing that happen in the same call and putting ourselves back is what lets this
            /// scene tell "the line finished" apart from "something else stole the frame" without a
            /// callback on EndingSpeechBox itself.</summary>
            private void ForwardToBox(Action<EndingSpeechBox> invoke)
            {
                if (_activeBox == null) return;
                bool wasActive = ReferenceEquals(Game1.activeClickableMenu, this);
                invoke(_activeBox);
                if (!wasActive || ReferenceEquals(Game1.activeClickableMenu, this)) return;

                Game1.activeClickableMenu = this;
                _activeBox = null;
                Finish();
            }

            public override void update(GameTime time)
            {
                base.update(time);
                AnimateJunimos(time);
                if (_completed) return;
                _activeBox?.update(time);
            }

            private void Finish()
            {
                if (_completed) return;
                _completed = true;
                TeardownWorldState();
                if (ReferenceEquals(Game1.activeClickableMenu, this))
                    Game1.activeClickableMenu = null;
                _onComplete?.Invoke();
            }

            public override void receiveLeftClick(int x, int y, bool playSound = true)
                => ForwardToBox(b => b.receiveLeftClick(x, y, playSound));

            public override void receiveRightClick(int x, int y, bool playSound = true)
                => ForwardToBox(b => b.receiveRightClick(x, y, playSound));

            public override void receiveKeyPress(Keys key)
                => ForwardToBox(b => b.receiveKeyPress(key));

            public override void receiveGamePadButton(Buttons b)
                => ForwardToBox(box => box.receiveGamePadButton(b));

            // Forced scene, matching every other beat in this sequence: never satisfy the engine's
            // close paths. Forwarding a cancel press to the open box (above) only advances its page.
            public override bool readyToClose() => false;

            public override void gameWindowSizeChanged(Rectangle oldBounds, Rectangle newBounds)
            {
                base.gameWindowSizeChanged(oldBounds, newBounds);
                width = Game1.uiViewport.Width;
                height = Game1.uiViewport.Height;
            }

            public override void draw(SpriteBatch b)
            {
                _activeBox?.draw(b);
            }
        }
    }
}
