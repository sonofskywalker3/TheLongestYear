using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Characters;
using TheLongestYear.Core;
using TheLongestYear.Core.Ending;
using TheLongestYear.Core.Rewind;
using CoreSeason = TheLongestYear.Core.Season;

namespace TheLongestYear.Integration
{
    /// <summary>Beat 10 of the rewind cutscene (design 2026-09-11, "the pan, beat 10"): a camera pan
    /// across Town from Clint's shop door out to the west-edge road toward the Bus Stop, while the
    /// year visibly runs backward underneath it.
    ///
    /// THIRTY SECONDS, FIXED, no matter how many seasons are being unwound: twenty-five of camera
    /// travel, three held on the villager turning around, two fading out as he walks away. (Retimed
    /// 2026-09-11: the first playtest ran the whole thing in about four seconds, and the turn-back
    /// was never on screen.) Dials:
    ///
    /// - Camera: PanStart to PanEnd over the TRAVEL leg only, eased the same smoothstep
    ///   <see cref="EndingEventCommands.PanToName"/> (tlyPanTo) uses, then still for the last five
    ///   seconds. That command's own centring math (ClampedCentre/SetCentre) is reused here through
    ///   reflection rather than reimplemented.
    /// - Seasons: <see cref="RewindSchedule.SwapFractions"/>/<see cref="RewindSchedule.SeasonsToUnwind"/>
    ///   drive <c>GameLocation.updateSeasonalTileSheets()</c> swaps only. NEVER <c>seasonUpdate()</c>,
    ///   which would mutate terrain, crops and features instead of just repainting them.
    /// - Date: <see cref="RewindSchedule.DateAt"/>, unwinding once and steadily across the whole
    ///   thirty seconds from the failed date back to Spring 1. This is what the HUD shows.
    /// - Light: <see cref="RewindSchedule.CycleClockAt"/>, a dusk-to-dawn loop about every two
    ///   seconds, so roughly fifteen cycles across the scene. <c>Game1.UpdateGameClock</c> already
    ///   recomputes <c>outdoorLight</c> from <c>Game1.timeOfDay</c> on its own each frame (it runs
    ///   unconditionally while no menu or minigame is up), so driving the clock is the whole effect;
    ///   this class does no tinting of its own. The light and the date are TWO INDEPENDENT DIALS and
    ///   are deliberately not synchronised: Jeff, 2026-09-11, was explicit that the date must not
    ///   line up with the visual sunset.
    /// - Weather: <c>Game1.isDebrisWeather</c> for the whole pan (the wind), with a harder gust
    ///   (re-seeded debris via <c>Game1.populateDebrisWeatherArray()</c>) on every season swap to hide
    ///   the tilesheet cut, which is a dispose-and-reload and cannot cross-fade.
    ///
    /// The villager beat borrows the ending's own cast selector, <see cref="EndingSpeaker.Pick"/>, with
    /// the same eligibility rule <see cref="EndingEventDriver"/> uses (present, not a child, not the
    /// spouse) so the villager who almost remembers the player at the good ending is the one who
    /// forgets them here. Pick can return null (no bonded villager yet); the pan still runs its full
    /// length on wind, clock and camera alone.
    ///
    /// The farmer never moves. <see cref="Game1.currentLocation"/> switches to Town for the pan the
    /// same way <c>WorldResetService</c> places the player without a warp (a bare reassignment; that
    /// property has no setter side effects), while <c>Game1.player.currentLocation</c> stays wherever
    /// the bedroom scene left it. <see cref="Game1.freezeControls"/> and <see cref="Game1.viewportFreeze"/>
    /// hold the player out of it for the duration.
    ///
    /// Normal completion deliberately leaves the season/clock/weather/camera in whatever rewound state
    /// the pan reached: that state IS the point, and the next beats (Spring 1 paint, back to the
    /// farmhouse) are somebody else's job. An ABNORMAL end (an exception mid-tick, or the player
    /// quitting to the title screen) is different: nothing else is watching, so <see cref="ForceTeardown"/>
    /// restores season, clock, weather, camera and control flags to what they were before Start and
    /// does not invoke the completion callback, rather than leave the save windy, dark and frozen.
    ///
    /// <see cref="Day28CutsceneDriver"/> opens this after RewindBedroomScene and continues into the
    /// Spring 1 paint and the morning beat. Call <see cref="Register"/> once from ModEntry.Entry, the
    /// same pattern <c>EndingEventCommands.Register</c> and <c>RewindJunimoScene.Register</c> already
    /// use, before the first <see cref="Start"/>.</summary>
    internal static class RewindPanScene
    {
        // Measured live on 2026-09-11 with tly_townroute (TownRouteProbe). Town has no warp targeting
        // Farm; the farm is reached through the Bus Stop, so PanEnd is that west-edge road, the "path
        // toward the farm in the top left" relative to Clint's.
        private static readonly Point PanStart = new Point(94, 81);   // Clint's shop door, Town.
        private static readonly Point PanEnd = new Point(0, 54);      // West-edge road out to the Bus Stop.

        // THE SHAPE OF THE SCENE (retimed 2026-09-11 after the first playtest, where the whole pan
        // ran about four seconds: the villager shot across the square diagonally, the turn-back beat
        // was never visible at all, and the light cycle was blink-and-miss). Thirty seconds, in three
        // parts, and the total is fixed no matter how many seasons are being unwound.
        private const float TravelMs = 25000f;   // the camera moving, Clint's door out to the west road
        private const float TurnHoldMs = 3000f;  // held on the villager, stopped, looking back
        private const float WalkAwayMs = 2000f;  // he walks on, the screen fades out
        private const float PanDurationMs = TravelMs + TurnHoldMs + WalkAwayMs;

        // The villager keeps station ahead of the camera rather than walking a fixed stretch of the
        // route on his own clock: his route fraction is the camera's plus a lead that closes to zero
        // by the end of the travel, so he is always on screen, always moving in the same direction as
        // the pan, and standing dead centre at the moment the camera stops for the turn. The old
        // fixed-stretch version is what produced the diagonal dash.
        private const double VillagerLeadFraction = 0.10;
        private const double VillagerEnterFraction = 0.35;   // of the travel, before which he holds
        // How far he walks on, along the route, during the fade.
        private const double VillagerWalkAwayFraction = 0.05;

        // Dusk down to dawn, looped: one cycle every two seconds, so roughly fifteen of them across
        // the scene. Impressionistic, not a calendar (Jeff, 2026-09-11), and deliberately NOT
        // synchronised with the date below.
        private const int ClockStart = 2400, ClockEnd = 600;
        private const double LightCycleMs = 2000.0;

        // EndingEventCommands.PanToName's own centring math, reused by reflection instead of
        // reimplemented (see the class comment: this task touches only this one file).
        private static readonly MethodInfo ClampedCentreMethod = typeof(EndingEventCommands).GetMethod(
            "ClampedCentre", BindingFlags.NonPublic | BindingFlags.Static);
        private static readonly MethodInfo SetCentreMethod = typeof(EndingEventCommands).GetMethod(
            "SetCentre", BindingFlags.NonPublic | BindingFlags.Static);

        private static IMonitor _monitor;
        private static MetaStore _meta;
        private static GameplayConfig _config;
        private static bool _registered;

        private static bool _active;
        private static Action _onComplete;
        private static Action _onAbort;
        private static float _elapsed;

        private static IReadOnlyList<CoreSeason> _seasons;
        private static IReadOnlyList<double> _swaps;
        private static int _seasonIndex;   // how many swaps have already landed; also indexes _seasons

        private static GameLocation _town;
        private static GameLocation _priorLocation;
        private static bool _priorFreezeControls, _priorViewportFreeze, _priorIsDebrisWeather;
        private static StardewValley.Season _priorSeason;   // Game1.season's own type
        private static int _priorTimeOfDay;
        private static int _priorDayOfMonth;
        private static CoreSeason _failed;
        private static float _fadeAlpha;

        private static NPC _villager;
        private static GameLocation _villagerHomeLocation;
        private static Vector2 _villagerHomePosition;
        private static int _villagerHomeFacing;
        private static bool _villagerEmoted;

        /// <summary>Wires the ticking and the return-to-title safety net. Safe to call more than once
        /// (later calls just refresh the stored references); the event subscription itself only
        /// happens on the first call.</summary>
        public static void Register(IMonitor monitor, IModHelper helper, MetaStore meta, GameplayConfig config)
        {
            _monitor = monitor;
            _meta = meta;
            _config = config;
            if (_registered) return;
            _registered = true;
            helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
            helper.Events.Display.Rendered += OnRendered;
            helper.Events.GameLoop.ReturnedToTitle += (_, _) => ForceTeardown("returned to title");
        }

        /// <summary>Starts the pan for a run that failed in <paramref name="failed"/>. Calls
        /// <paramref name="onComplete"/> once the camera reaches PanEnd. If Town is not loaded (should
        /// never happen; it is one of the game's always-loaded outdoor locations) the callback still
        /// fires immediately so the rest of the sequence is not stranded.
        ///
        /// <paramref name="onAbort"/> is the other half of that promise and the reason it is a second
        /// callback rather than a flag on the first: for the whole pan there is no menu of ours for
        /// the day-28 driver's steal watchdog to watch (the driver clears its _openedMenu at the
        /// hand-off precisely so the watchdog does not misread the hand-off as a steal), so if the pan
        /// ends abnormally -- an exception mid-Tick, or a quit to title -- nothing at all is watching
        /// and the driver would sit on _opened forever with PendingCutscene still Fail: no reset, no
        /// shrine, no Spring 1, the failed season rolling silently on. <see cref="ForceTeardown"/>
        /// invokes this synchronously after it has restored the world, so the driver re-arms on a
        /// world that is back where it started rather than mid-rewind. Exactly one of onComplete and
        /// onAbort ever runs.</summary>
        public static void Start(CoreSeason failed, Action onComplete, Action onAbort = null)
        {
            if (_active)
            {
                _monitor?.Log("RewindPanScene: Start called while already running; ignoring.", LogLevel.Warn);
                return;
            }

            GameLocation town = Game1.getLocationFromName("Town");
            if (town == null)
            {
                _monitor?.Log("RewindPanScene: Town is not loaded; skipping the pan.", LogLevel.Warn);
                onComplete?.Invoke();
                return;
            }

            _town = town;
            _failed = failed;
            _seasons = RewindSchedule.SeasonsToUnwind(failed);
            _swaps = RewindSchedule.SwapFractions(failed);
            _seasonIndex = 0;
            _elapsed = 0f;
            _fadeAlpha = 0f;
            _onComplete = onComplete;
            _onAbort = onAbort;

            _priorLocation = Game1.currentLocation;
            _priorFreezeControls = Game1.freezeControls;
            _priorViewportFreeze = Game1.viewportFreeze;
            _priorIsDebrisWeather = Game1.isDebrisWeather;
            _priorSeason = Game1.season;
            _priorTimeOfDay = Game1.timeOfDay;
            _priorDayOfMonth = Game1.dayOfMonth;

            // BEFORE anything below is written, not after (bug found 2026-09-11). Everything from
            // here on mutates the world, and ForceTeardown bails on !_active, so a throw inside
            // Gust() or SpawnVillager() used to leave Town painted in the failed season and the
            // camera frozen on it with no abort callback and nothing watching. Arming the flag
            // first means any throw below reaches the restore path.
            _active = true;

            Game1.currentLocation = _town;
            Game1.freezeControls = true;
            Game1.viewportFreeze = true;
            SetCentre(ClampedCentre(PanStart.X, PanStart.Y));
            // Open ON the season that just failed, which is _seasons[0]. Game1.season is NOT that
            // season here: day 28 is a season's last day, so the overnight transition has already
            // rolled it forward to the NEXT one by the time this driver ever runs (which is exactly
            // why Start is handed the failed season rather than reading the global). Without this,
            // TickSeasons' increment-then-index only ever applied _seasons[1..] and the pan opened one
            // season ahead: a Fall failure showed Winter, Summer, Spring and never Fall; a Winter
            // failure opened in SPRING, the state it is rewinding to, then went forward to Fall; a
            // Spring failure (no swaps at all) played the entire pan in Summer with no repaint. The
            // headline beat read as backwards-then-forwards in every branch.
            //
            // Repaint, never seasonUpdate(): updateSeasonalTileSheets disposes and reloads the map's
            // tilesheets under the current season key, where seasonUpdate would mutate terrain, crops
            // and features. Spring is not a special case here; it repaints Town to Spring like any
            // other, which is what makes a Spring failure look right instead of Summer-tinted.
            Game1.season = (StardewValley.Season)(int)_seasons[0];
            _town.updateSeasonalTileSheets();
            Gust();

            SpawnVillager();
        }

        /// <summary>True while the pan is running. The pan owns no menu, so this is the only way
        /// anything outside can tell it is on screen.</summary>
        public static bool IsActive => _active;

        /// <summary>Fast-forwards the pan to its last frame and finishes it, as if the whole thirty
        /// seconds had passed in one tick. The pan's own <see cref="Tick"/> does the work, so every remaining
        /// season swap still lands (<see cref="TickSeasons"/> advances with a while loop precisely so
        /// a large jump in progress does not skip any) and the clock, camera and villager all end
        /// where a watched run would leave them, which is what the next beats assume.
        ///
        /// This is the pan's half of the three per-scene skip entry points behind
        /// <c>tly_skipscene</c>; the two Junimo beats have their own
        /// <see cref="TheLongestYear.UI.RewindJunimoScene.SkipToEnd"/>. Returns false when the pan is
        /// not running, so the caller can try the next scene instead of reporting a skip that did not
        /// happen.</summary>
        public static bool SkipToEnd()
        {
            if (!_active) return false;
            _monitor?.Log("RewindPanScene: fast-forwarding the pan to its last frame.", LogLevel.Info);
            _elapsed = PanDurationMs;
            try
            {
                // A zero-length GameTime: Tick adds ElapsedGameTime to _elapsed before using it, and
                // the jump has already been made above.
                Tick(new GameTime());
            }
            catch (Exception ex)
            {
                _monitor?.Log($"RewindPanScene: {ex.GetType().Name}: {ex.Message}; ending the pan early.", LogLevel.Error);
                ForceTeardown("exception while skipping");
            }
            return true;
        }

        private static void OnUpdateTicked(object sender, UpdateTickedEventArgs e)
        {
            if (!_active) return;
            try
            {
                Tick(Game1.currentGameTime);
            }
            catch (Exception ex)
            {
                _monitor?.Log($"RewindPanScene: {ex.GetType().Name}: {ex.Message}; ending the pan early.", LogLevel.Error);
                ForceTeardown("exception");
            }
        }

        private static void Tick(GameTime time)
        {
            _elapsed += (float)time.ElapsedGameTime.TotalMilliseconds;
            double progress = Math.Clamp(_elapsed / PanDurationMs, 0.0, 1.0);
            double travel = Math.Clamp(_elapsed / TravelMs, 0.0, 1.0);

            TickCamera(travel);
            TickSeasons(progress);
            TickDate(progress);
            // The light. A repeating dusk-to-dawn loop on its OWN clock, deliberately not lined up
            // with the date above (Jeff, 2026-09-11). Game1.UpdateGameClock recomputes outdoorLight
            // from timeOfDay every tick while no menu is up, so writing the clock is the whole
            // effect; this class does no tinting of its own.
            Game1.timeOfDay = RewindSchedule.CycleClockAt(_elapsed, LightCycleMs, ClockStart, ClockEnd);
            TickVillager(travel);
            TickFade();

            if (progress >= 1.0) Finish();
        }

        /// <summary>Eased along the TRAVEL leg only, so the camera reaches the west road at 25
        /// seconds and then holds still for the turn and the walk away. Passing the whole scene's
        /// progress here instead is what would put the camera still drifting under the beat that is
        /// supposed to be a held shot.</summary>
        private static void TickCamera(double travel)
        {
            float eased = Ease(travel);
            Vector2 from = ClampedCentre(PanStart.X, PanStart.Y);
            Vector2 to = ClampedCentre(PanEnd.X, PanEnd.Y);
            SetCentre(Vector2.Lerp(from, to, eased));
        }

        private static float Ease(double t) => (float)(t * t * (3.0 - 2.0 * t));   // smoothstep, matching tlyPanTo

        /// <summary>The date the HUD shows, unwinding once and steadily across the whole thirty
        /// seconds from the failed date back to Spring 1. <see cref="RewindSchedule.DateAt"/> owns
        /// the arithmetic and divides the scene evenly between the seasons being unwound, which is
        /// the same division <see cref="RewindSchedule.SwapFractions"/> uses for the repaints, so the
        /// day counter and the map always agree on which season is on screen.</summary>
        private static void TickDate(double progress)
        {
            (CoreSeason _, int day) = RewindSchedule.DateAt(progress, _failed);
            Game1.dayOfMonth = day;
        }

        /// <summary>The last two seconds, fading out while the villager walks on. Drawn by
        /// <see cref="OnRendered"/> rather than through Game1's own fade fields, which the engine's
        /// ScreenFade owns and would fight us for.</summary>
        private static void TickFade()
        {
            float fadeStart = TravelMs + TurnHoldMs;
            _fadeAlpha = _elapsed <= fadeStart
                ? 0f
                : MathHelper.Clamp((_elapsed - fadeStart) / WalkAwayMs, 0f, 1f);
        }

        /// <summary>The pan owns no menu, so its fade has to be painted here, after everything else
        /// the frame draws. It stops the moment the pan does: on a normal finish that is the same
        /// synchronous step in which the driver opens the morning beat, and that scene draws its own
        /// full-black first frame, so the hand-off is one continuous fade rather than a flash.</summary>
        private static void OnRendered(object sender, RenderedEventArgs e)
        {
            if (!_active || _fadeAlpha <= 0f) return;
            e.SpriteBatch.Draw(
                Game1.fadeToBlackRect,
                new Rectangle(0, 0, Game1.uiViewport.Width, Game1.uiViewport.Height),
                Color.Black * _fadeAlpha);
        }

        /// <summary>Advances through <see cref="_swaps"/> with a while loop rather than a single if, so
        /// more than one swap crossed in the same tick (a very short pan, or a hitch) still lands every
        /// one of them instead of skipping. A Spring failure's empty <see cref="_swaps"/> makes the
        /// loop condition false immediately: no division, no indexing, nothing happens (and the whole
        /// pan therefore stays on the Spring that <see cref="Start"/> already applied).
        ///
        /// Increment-then-index is correct BECAUSE Start applies <c>_seasons[0]</c> itself: the season
        /// at index _seasonIndex is always the one currently on screen, and crossing swap n moves to
        /// _seasons[n]. _swaps has exactly one fewer entry than _seasons (RewindSchedule), so the last
        /// swap lands on _seasons[Count - 1] and never runs off the end.</summary>
        private static void TickSeasons(double progress)
        {
            while (_seasonIndex < _swaps.Count && progress >= _swaps[_seasonIndex])
            {
                _seasonIndex++;
                // Core.Season and StardewValley.Season share Spring=0..Winter=3 (see GameEffortData.cs).
                Game1.season = (StardewValley.Season)(int)_seasons[_seasonIndex];
                _town.updateSeasonalTileSheets();
                Gust();
            }
        }

        /// <summary>The wind for the whole pan, and the harder gust on each season swap: both are the
        /// same call. <c>populateDebrisWeatherArray</c> re-seeds debris positions AND its sprite index
        /// from the current season, so calling it again right after a tilesheet swap also refreshes the
        /// debris to match (leaves in Fall, snow in Winter) rather than leaving the old season's look
        /// blowing across the new one.</summary>
        private static void Gust()
        {
            Game1.isDebrisWeather = true;
            Game1.populateDebrisWeatherArray();
        }

        private static void SpawnVillager()
        {
            Func<string, bool> eligible = name =>
            {
                NPC candidate = Game1.getCharacterFromName(name);
                if (candidate == null || candidate is Child) return false;
                Farmer player = Game1.player;
                if (player?.spouse != null && player.spouse == name) return false;
                return true;
            };

            string name = _meta != null ? EndingSpeaker.Pick(_meta.State, _config?.DejaVuThreshold ?? 0, eligible) : null;
            if (name == null) { _villager = null; return; }

            NPC npc = Game1.getCharacterFromName(name);
            if (npc == null) { _villager = null; return; }

            _villagerHomeLocation = npc.currentLocation;
            _villagerHomePosition = npc.Position;
            _villagerHomeFacing = npc.FacingDirection;
            _villagerEmoted = false;

            npc.controller = null;
            npc.Halt();
            if (_villagerHomeLocation != _town)
            {
                _villagerHomeLocation?.characters.Remove(npc);
                if (!_town.characters.Contains(npc)) _town.characters.Add(npc);
            }
            npc.currentLocation = _town;
            // Same reason as the Junimos in the bedroom beats: a character whose forceUpdateTimer is
            // above zero gets its own update() run by GameLocation.updateCharacters even though
            // shouldTimePass is false, which would put this one back on its schedule mid-shot.
            npc.forceUpdateTimer = 0;
            npc.faceDirection(Game1.left);

            _villager = npc;
            PlaceVillagerAt(VillagerFractionAt(0.0));
        }

        /// <summary>Where along the route the villager stands at a given point in the camera's
        /// travel. He keeps station AHEAD of the camera by a lead that closes to nothing by the end
        /// of the travel, rather than walking his own fixed stretch on his own clock: that is what
        /// keeps him on screen for the whole shot, moving the same way the pan is, and standing dead
        /// centre at the moment the camera stops. Walking a fixed stretch against an eased camera is
        /// what made him shoot diagonally across the square in the first playtest.</summary>
        private static double VillagerFractionAt(double travel)
        {
            double camera = Ease(travel);
            double closing = travel <= VillagerEnterFraction
                ? 1.0
                : 1.0 - (travel - VillagerEnterFraction) / (1.0 - VillagerEnterFraction);
            return Math.Clamp(camera + VillagerLeadFraction * closing, 0.0, 1.0);
        }

        /// <summary>Three beats, on the same clock as the camera: he walks the path toward the farm
        /// while the camera travels, stops and looks back for the three held seconds, then turns away
        /// and walks on through the fade. The looking back is the cost the scene is about: the
        /// villager the player is most bonded with, half-remembering them, about to not.</summary>
        private static void TickVillager(double travel)
        {
            if (_villager == null) return;

            if (travel < 1.0)
            {
                PlaceVillagerAt(VillagerFractionAt(travel));
                _villager.faceDirection(Game1.left);
                if (travel > VillagerEnterFraction) _villager.Sprite?.AnimateLeft(Game1.currentGameTime);
                return;
            }

            float turnEnd = TravelMs + TurnHoldMs;
            if (_elapsed < turnEnd)
            {
                // Stopped, turned back the way he came. The emote is one shot, not once per tick.
                _villager.faceDirection(Game1.right);
                if (_villagerEmoted) return;
                _villagerEmoted = true;
                _villager.doEmote(Character.questionMarkEmote);
                return;
            }

            double away = MathHelper.Clamp((_elapsed - turnEnd) / WalkAwayMs, 0f, 1f);
            PlaceVillagerAt(VillagerFractionAt(1.0) + VillagerWalkAwayFraction * away);
            _villager.faceDirection(Game1.left);
            _villager.Sprite?.AnimateLeft(Game1.currentGameTime);
        }

        private static void PlaceVillagerAt(double routeFraction)
        {
            float t = (float)Math.Clamp(routeFraction, 0.0, 1.0);
            _villager.Position = new Vector2(
                MathHelper.Lerp(PanStart.X, PanEnd.X, t) * 64f,
                MathHelper.Lerp(PanStart.Y, PanEnd.Y, t) * 64f);
        }

        /// <summary>Gives the borrowed villager back exactly where it was found, regardless of whether
        /// the pan finished normally or was cut short. Idempotent and safe when nothing was spawned.</summary>
        private static void TeardownVillager()
        {
            if (_villager == null) return;
            NPC npc = _villager;
            _villager = null;
            try
            {
                npc.controller = null;
                npc.Halt();
                if (_villagerHomeLocation != null && _villagerHomeLocation != _town)
                {
                    _town.characters.Remove(npc);
                    if (!_villagerHomeLocation.characters.Contains(npc))
                        _villagerHomeLocation.characters.Add(npc);
                }
                npc.currentLocation = _villagerHomeLocation ?? npc.currentLocation;
                npc.Position = _villagerHomePosition;
                npc.faceDirection(_villagerHomeFacing);
            }
            catch (Exception ex)
            {
                _monitor?.Log($"RewindPanScene: {ex.GetType().Name} returning the villager: {ex.Message}", LogLevel.Warn);
            }
        }

        /// <summary>Normal end of the pan: the rewound season/clock/weather/camera are left exactly as
        /// the pan reached them (that IS the beat) for the next part of the sequence to paint over.
        /// Only the borrowed villager is given back.</summary>
        private static void Finish()
        {
            if (!_active) return;
            _active = false;
            TeardownVillager();
            Action onComplete = _onComplete;
            _onComplete = null;
            _onAbort = null;   // exactly one of the two ever runs
            onComplete?.Invoke();
        }

        /// <summary>Abnormal end: an exception mid-tick, or the player quitting to the title screen.
        /// Nothing else is watching this scene, so unlike <see cref="Finish"/> this restores season,
        /// clock, weather, camera and the control-freeze flags to what they were before <see cref="Start"/>
        /// rather than leave the save windy, dark and frozen, and does not invoke the completion
        /// callback (the sequence it would continue into assumes the pan actually finished). It DOES
        /// invoke the abort callback, last, once the world is restored: see <see cref="Start"/> for
        /// why leaving that unsaid strands the whole day-28 loop.</summary>
        private static void ForceTeardown(string reason)
        {
            if (!_active) return;
            _active = false;
            _fadeAlpha = 0f;
            _monitor?.Log($"RewindPanScene: forcing teardown ({reason}); restoring season, clock, weather and camera.", LogLevel.Warn);
            TeardownVillager();
            try
            {
                if (_town != null)
                {
                    Game1.season = _priorSeason;
                    _town.updateSeasonalTileSheets();
                }
                Game1.timeOfDay = _priorTimeOfDay;
                Game1.dayOfMonth = _priorDayOfMonth;
                Game1.isDebrisWeather = _priorIsDebrisWeather;
                Game1.freezeControls = _priorFreezeControls;
                Game1.viewportFreeze = _priorViewportFreeze;
                if (_priorLocation != null) Game1.currentLocation = _priorLocation;
            }
            catch (Exception ex)
            {
                _monitor?.Log($"RewindPanScene: {ex.GetType().Name} restoring state: {ex.Message}", LogLevel.Error);
            }
            _onComplete = null;
            Action onAbort = _onAbort;
            _onAbort = null;
            // Last, and outside the restore try: the driver re-arms onto a world that is already back
            // where Start found it. Its own failure must not be able to skip the restore above.
            try
            {
                onAbort?.Invoke();
            }
            catch (Exception ex)
            {
                _monitor?.Log($"RewindPanScene: {ex.GetType().Name} in the abort callback: {ex.Message}", LogLevel.Error);
            }
        }

        private static Vector2 ClampedCentre(int x, int y)
            => ClampedCentreMethod != null
                ? (Vector2)ClampedCentreMethod.Invoke(null, new object[] { x, y })
                : new Vector2(x * 64f + 32f, y * 64f + 32f);

        private static void SetCentre(Vector2 centre)
        {
            if (SetCentreMethod != null)
            {
                SetCentreMethod.Invoke(null, new object[] { centre });
                return;
            }
            Game1.viewport.X = (int)Math.Round(centre.X - Game1.viewport.Width / 2f);
            Game1.viewport.Y = (int)Math.Round(centre.Y - Game1.viewport.Height / 2f);
        }

    }
}
