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
    /// year visibly runs backward underneath it. Four dials tick off one normalised progress value
    /// (0 at PanStart, 1 at PanEnd):
    ///
    /// - Camera: PanStart to PanEnd, eased the same smoothstep <see cref="EndingEventCommands.PanToName"/>
    ///   (tlyPanTo) uses. That command's own centring math (ClampedCentre/SetCentre) is reused here
    ///   through reflection rather than reimplemented, so this file is the only one this task touches
    ///   (EndingEventCommands.cs is not modified).
    /// - Seasons: <see cref="RewindSchedule.SwapFractions"/>/<see cref="RewindSchedule.SeasonsToUnwind"/>
    ///   drive <c>GameLocation.updateSeasonalTileSheets()</c> swaps only. NEVER <c>seasonUpdate()</c>,
    ///   which would mutate terrain, crops and features instead of just repainting them.
    /// - Clock: <see cref="RewindSchedule.ClockAt"/> every tick. <c>Game1.UpdateGameClock</c> already
    ///   recomputes <c>outdoorLight</c> from <c>Game1.timeOfDay</c> on its own each frame (it runs
    ///   unconditionally while no menu or minigame is up), so driving the clock backward lights the
    ///   valley backward for free; this class does no tinting of its own.
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
    /// This class only builds the scene; nothing opens it yet (a later task wires <see cref="Start"/>
    /// into the day-28 driver after RewindBedroomScene). Call <see cref="Register"/> once from
    /// ModEntry.Entry, the same pattern <c>EndingEventCommands.Register</c> and
    /// <c>RewindBedroomScene.Register</c> already use, before the first <see cref="Start"/>.</summary>
    internal static class RewindPanScene
    {
        // Measured live on 2026-09-11 with tly_townroute (TownRouteProbe). Town has no warp targeting
        // Farm; the farm is reached through the Bus Stop, so PanEnd is that west-edge road, the "path
        // toward the farm in the top left" relative to Clint's.
        private static readonly Point PanStart = new Point(94, 81);   // Clint's shop door, Town.
        private static readonly Point PanEnd = new Point(0, 54);      // West-edge road out to the Bus Stop.

        private const float PanDurationMs = 11000f;

        // The villager walks a stretch of the same measured route (30% to 70% of the way along it)
        // rather than a second guessed pair of coordinates, so its path stays inside the framed shot.
        private const double VillagerStartFraction = 0.30, VillagerStopFraction = 0.70;
        private const double WalkOutStart = 0.12, WalkOutEnd = 0.42;
        private const double WalkBackStart = 0.60, WalkBackEnd = 0.90;

        private static readonly Point VillagerStart = LerpPoint(PanStart, PanEnd, VillagerStartFraction);
        private static readonly Point VillagerStop = LerpPoint(PanStart, PanEnd, VillagerStopFraction);

        // Midnight down to 6am: the backward clock's range for the whole pan, regardless of how many
        // seasons are being unwound.
        private const int ClockStart = 2400, ClockEnd = 600;

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
            _seasons = RewindSchedule.SeasonsToUnwind(failed);
            _swaps = RewindSchedule.SwapFractions(failed);
            _seasonIndex = 0;
            _elapsed = 0f;
            _onComplete = onComplete;
            _onAbort = onAbort;

            _priorLocation = Game1.currentLocation;
            _priorFreezeControls = Game1.freezeControls;
            _priorViewportFreeze = Game1.viewportFreeze;
            _priorIsDebrisWeather = Game1.isDebrisWeather;
            _priorSeason = Game1.season;
            _priorTimeOfDay = Game1.timeOfDay;

            Game1.currentLocation = _town;
            Game1.freezeControls = true;
            Game1.viewportFreeze = true;
            SetCentre(ClampedCentre(PanStart.X, PanStart.Y));
            Gust();

            SpawnVillager();

            _active = true;
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

            TickCamera(progress);
            TickSeasons(progress);
            Game1.timeOfDay = RewindSchedule.ClockAt(progress, ClockStart, ClockEnd);
            TickVillager(progress);

            if (progress >= 1.0) Finish();
        }

        private static void TickCamera(double progress)
        {
            float eased = (float)(progress * progress * (3.0 - 2.0 * progress));   // smoothstep, matching tlyPanTo
            Vector2 from = ClampedCentre(PanStart.X, PanStart.Y);
            Vector2 to = ClampedCentre(PanEnd.X, PanEnd.Y);
            SetCentre(Vector2.Lerp(from, to, eased));
        }

        /// <summary>Advances through <see cref="_swaps"/> with a while loop rather than a single if, so
        /// more than one swap crossed in the same tick (a very short pan, or a hitch) still lands every
        /// one of them instead of skipping. A Spring failure's empty <see cref="_swaps"/> makes the
        /// loop condition false immediately: no division, no indexing, nothing happens.</summary>
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
            npc.Position = new Vector2(VillagerStart.X * 64f, VillagerStart.Y * 64f);
            npc.faceDirection(Game1.left);

            _villager = npc;
        }

        private static void TickVillager(double progress)
        {
            if (_villager == null) return;

            if (progress < WalkOutStart) return;   // holding at VillagerStart

            if (progress < WalkOutEnd)
            {
                double t = (progress - WalkOutStart) / (WalkOutEnd - WalkOutStart);
                PlaceVillagerAlong(VillagerStart, VillagerStop, t);
                _villager.faceDirection(Game1.left);
                return;
            }

            if (progress < WalkBackStart)
            {
                if (_villagerEmoted) return;
                _villagerEmoted = true;
                _villager.faceDirection(Game1.up);
                _villager.doEmote(Character.questionMarkEmote);
                return;
            }

            if (progress < WalkBackEnd)
            {
                double t = (progress - WalkBackStart) / (WalkBackEnd - WalkBackStart);
                PlaceVillagerAlong(VillagerStop, VillagerStart, t);
                _villager.faceDirection(Game1.right);
                return;
            }

            // Holding at VillagerStart again for the remainder of the pan (turned back).
        }

        private static void PlaceVillagerAlong(Point from, Point to, double t)
        {
            float clamped = (float)Math.Clamp(t, 0.0, 1.0);
            float x = MathHelper.Lerp(from.X, to.X, clamped) * 64f;
            float y = MathHelper.Lerp(from.Y, to.Y, clamped) * 64f;
            _villager.Position = new Vector2(x, y);
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

        private static Point LerpPoint(Point a, Point b, double t)
            => new Point(a.X + (int)Math.Round((b.X - a.X) * t), a.Y + (int)Math.Round((b.Y - a.Y) * t));
    }
}
