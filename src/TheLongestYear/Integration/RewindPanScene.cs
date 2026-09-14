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
    /// TWENTY-FIVE SECONDS, FIXED, no matter how many seasons are being unwound: unbroken camera
    /// travel throughout, the last two of them fading to black while the camera is still moving.
    /// (Retimed 2026-09-11: the first playtest ran the whole thing in about four seconds and nothing
    /// in it had time to read; thirty then felt long and came back down by five.) Dials:
    ///
    /// - Camera: PanStart to PanEnd across the whole twenty-five seconds, eased the same smoothstep
    ///   <see cref="EndingEventCommands.PanToName"/> (tlyPanTo) uses. That command's own centring
    ///   math (ClampedCentre/SetCentre) is reused here through reflection rather than
    ///   reimplemented.
    /// - Seasons: <see cref="RewindSchedule.SwapFractions"/>/<see cref="RewindSchedule.SeasonsToUnwind"/>
    ///   drive <c>GameLocation.updateSeasonalTileSheets()</c> swaps only. NEVER <c>seasonUpdate()</c>,
    ///   which would mutate terrain, crops and features instead of just repainting them.
    /// - Light: <see cref="RewindSchedule.CycleClockAt"/>, a sunrise-and-sunset loop about every
    ///   three seconds, so roughly eight cycles across the scene. <c>Game1.UpdateGameClock</c>
    ///   already recomputes <c>outdoorLight</c> from <c>Game1.timeOfDay</c> on its own each frame
    ///   (it runs unconditionally while no menu or minigame is up), so driving the clock is the
    ///   whole effect; this class does no tinting of its own.
    ///
    /// NO DATE DIAL, AND NO CLOCK ON SCREEN. The date used to unwind across the pan for the HUD to
    /// show. It never showed: Game1 skips drawHUD entirely while <c>freezeControls</c> is set, which
    /// this scene sets for its whole run, so the clock and date were hidden the whole time and the
    /// dial had no output at all. Jeff, 2026-09-11, ruled that the clock SHOULD stay hidden (the
    /// light cycle would make nonsense of it), which leaves writing <c>Game1.dayOfMonth</c> as pure
    /// risk with nothing to show for it, and it was live risk: see the gameTimeInterval note in
    /// <see cref="Tick"/>.
    /// - Weather: <c>Game1.isDebrisWeather</c> for the whole pan (the wind), with a harder gust
    ///   (re-seeded debris via <c>Game1.populateDebrisWeatherArray()</c>) on every season swap to hide
    ///   the tilesheet cut, which is a dispose-and-reload and cannot cross-fade.
    ///
    /// NO SINGLE VILLAGER, BUT TRAFFIC. The spec's beat 10 had the villager the player is most
    /// bonded with walking the path to the farm, stopping and turning back, the cost of the rewind
    /// made personal. It was built twice and cut on Jeff's call (2026-09-11): "Drop the NPC, it
    /// doesn't work." It covered the whole route in seconds and read as a prop being dragged rather
    /// than a person leaving. What replaced it, at his ask, is ambient rather than focal:
    /// <see cref="RewindReversedExtras"/> puts a handful of townsfolk on the road at an ordinary
    /// walking pace, facing the way they are going and sliding the other way, as one more reading
    /// that time is running backwards.
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

        // THE SHAPE OF THE SCENE. Twenty-five seconds of unbroken camera travel, and the total is fixed
        // no matter how many seasons are being unwound. The last two of those twenty-five fade the screen
        // out WHILE the camera is still moving: the shot never stops (Jeff, 2026-09-11, "don't hold.
        // add the extra 5 seconds into the sweep timer, and fade out for the last 2 seconds while
        // you're still sweeping" -- the version before this one stopped for three seconds before
        // fading, which was the held beat the cut villager used to fill).
        private const float PanDurationMs = 25000f;
        private const float TravelMs = PanDurationMs;
        private const float FadeOutMs = 2000f;

        // Sunset and sunrise, looped: three seconds a cycle, a second and a half each way, so about
        // eight of them across the scene. Slowed by half from two seconds on 2026-09-11; at the old
        // rate the valley strobed rather than breathed. The dark end is ten at night (Jeff, 2026-09-11); the light end is
        // read off the location per season rather than fixed, because the hour the valley starts to
        // darken moves (Spring 1800, Fall 1700, Winter 1500) and a fixed floor would never reach
        // full daylight in Winter. Impressionistic, not a calendar, and deliberately NOT synchronised
        // with the date.
        private const int LightCycleDusk = 2200;
        private const int LightCycleDawnMargin = 100;   // below the hour the valley starts to darken
        private const double LightCycleMs = 3000.0;

        /// <summary>The latest clock this scene will ever leave on the world. Vanilla passes the
        /// farmer out at 2600 (Game1.cs:6021), so anything at or past it is a NewDay with a fuse on
        /// it. See <see cref="Start"/>.</summary>
        private const int LastSafeClock = 2550;

        // THE WEATHER, one kind per season (Jeff, 2026-09-11): rain in Spring, a storm in Summer,
        // the really windy day in Fall, a snowstorm in Winter. No green rain, by his call. Each one
        // lands on the season swap, so the year running backwards changes the sky as well as the map.
        //
        // Wind runs under ALL of them, because the gust is what hides the tilesheet swap, which is a
        // dispose-and-reload and cannot cross-fade. Fall is simply the windiest.
        private const int GaleDebris = 110;          // vanilla's own gust is a random 16 to 64
        private const int FallGaleDebris = 170;      // "the really windy day"
        private const float GaleDriftX = -1.4f;      // vanilla drifts at -0.2 to 0
        private const float GaleDriftSpread = 0.9f;
        private const float GaleFallY = 0.35f;
        private const double StormFlashEveryMs = 1900.0;

        // ONE DAY OF IT, not the whole season. The weather used to be switched on at the season swap
        // and left on until the next one, so every season was a solid block of rain or snow: "I
        // didn't mean for the weather to happen for the entirety of that seasons turn, just one of
        // the day/night cycles within that season, like the actual game does" (Jeff, 2026-09-11).
        // Each season now gets a single cycle of its weather, held off the swap by one cycle so the
        // change of sky reads as its own beat rather than as part of the change of map.
        private const double WeatherAfterSwapMs = 3000.0;   // one light cycle
        private const double WeatherLastsMs = 3000.0;       // one light cycle

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
        private static bool _priorCanMove;
        private static bool _priorTownDebrisWeather, _priorTownRain, _priorTownSnow, _priorTownLightning;
        private static double _stormFlashMs;
        private static bool _weatherOn;
        private static double _extrasLogMs;
        private static StardewValley.Season _priorSeason;   // Game1.season's own type
        private static int _priorTimeOfDay;
        private static float _fadeAlpha;

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
            _priorTownDebrisWeather = town.GetWeather().IsDebrisWeather;
            _priorTownRain = town.GetWeather().IsRaining;
            _priorTownSnow = town.GetWeather().IsSnowing;
            _priorTownLightning = town.GetWeather().IsLightning;
            _stormFlashMs = 0.0;
            _weatherOn = false;
            _extrasLogMs = 0.0;
            _priorSeason = Game1.season;
            _priorTimeOfDay = Game1.timeOfDay;

            // BEFORE anything below is written, not after (bug found 2026-09-11). Everything from
            // here on mutates the world, and ForceTeardown bails on !_active, so a throw inside
            // Gust() used to leave Town painted in the failed season and the
            // camera frozen on it with no abort callback and nothing watching. Arming the flag
            // first means any throw below reaches the restore path.
            _active = true;

            Game1.currentLocation = _town;
            Game1.freezeControls = true;
            Game1.viewportFreeze = true;
            // THE CLOCK MUST COME OFF 2am IN THIS CALL, not on the first Tick (bug found from a
            // screenshot, 2026-09-11). The bedroom beat paints the failed night, which leaves
            // Game1.timeOfDay at 2600, and vanilla passes the farmer out at 2am: Game1.cs:6021 runs
            // "if (timeOfDay >= 2600 ... && activeClickableMenu == null) { player.startToPassOut();
            // player.freezePause = 7000; }" every tick. All through the bedroom that branch was held
            // off by the scene's own menu; the moment the scene finished and handed over to this pan,
            // which deliberately owns NO menu, the branch fired on the very next frame. Seven seconds
            // later the farmer passed out, the game ran a full NewDay ("Can't wake up in last sleep
            // location 'Town'", then a save, then "starting spring 1 Y2"), the player was warped home
            // and the whole pan was over in four: "you never fixed the pan only lasting like
            // 4 seconds" (Jeff, 2026-09-11).
            //
            // Start runs synchronously inside the bedroom menu's own update, so writing the clock
            // here lands BEFORE that check next sees it. CanMove is the same guard from the other
            // side: the branch also requires player.canMove, and the farmer is not walking anywhere
            // during a camera pan. Both, because either one alone would put the whole beat back on
            // one line holding.
            Game1.timeOfDay = RewindSchedule.CycleClockAt(0.0, LightCycleMs, LightCycleDusk, DaylightTime());
            _priorCanMove = Game1.player?.canMove ?? true;
            if (Game1.player != null) Game1.player.CanMove = false;
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
            ClearWeather();
            Gust();

            RewindReversedExtras.Spawn(_monitor, _town, PanStart, PanEnd, PanDurationMs);
            RewindReversedExtras.RefreshAppearance();
        }

        /// <summary>True while the pan is running. The pan owns no menu, so this is the only way
        /// anything outside can tell it is on screen.</summary>
        public static bool IsActive => _active;

        /// <summary>Fast-forwards the pan to its last frame and finishes it, as if the whole run
        /// had passed in one tick. The pan's own <see cref="Tick"/> does the work, so every remaining
        /// season swap still lands (<see cref="TickSeasons"/> advances with a while loop precisely so
        /// a large jump in progress does not skip any) and the clock, camera and fade all end where
        /// a watched run would leave them, which is what the next beats assume.
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
            // NOTHING MUST TICK OVER while the clock is being used as a light dial.
            // Game1.UpdateGameClock fires performTenMinuteClockUpdate once gameTimeInterval passes
            // realMilliSecondsPerGameTenMinutes, and it arrives here already full from normal play,
            // so it fired during the pan. That handler reads Data/Festivals/<season><day> whenever
            // today is a festival and can show the festival-has-started message when the clock it
            // is handed matches the start time, both of which this scene is now sweeping past
            // eight times. Live 2026-09-11, on a save sitting on the Flower Dance, it threw
            // ContentLoadException for Data/Festivals/spring15 three times in one pan. Zeroing the
            // accumulator every tick stops the handler firing at all, and also takes its
            // contribution out of the outdoorLight ramp below, which is one fewer thing moving
            // under the light cycle.
            Game1.gameTimeInterval = 0;
            // Hold the 2am pass-out off for the whole beat, not just its first frame. See Start: the
            // branch at Game1.cs:6021 is checked every tick, this scene has no menu to block it, and
            // anything that puts the clock back past 2600 mid-pan would end the beat the same way.
            if (Game1.player != null) Game1.player.CanMove = false;
            // The light. A sunrise-and-sunset loop on its own clock. Game1.UpdateGameClock
            // recomputes outdoorLight from timeOfDay every tick while no menu is up, so writing the
            // clock is the whole effect; this class does no tinting of its own.
            Game1.timeOfDay = RewindSchedule.CycleClockAt(
                _elapsed, LightCycleMs, LightCycleDusk, DaylightTime());
            TickWeather();
            TickStorm(time);
            TickFade();
            RewindReversedExtras.Tick(_elapsed, Ease(travel), time);
            TickExtrasLog(time);

            if (progress >= 1.0) Finish();
        }

        /// <summary>Says once a second where the extras are against where the camera is pointed.
        /// See RewindReversedExtras.Positions.</summary>
        private static void TickExtrasLog(GameTime time)
        {
            _extrasLogMs += time.ElapsedGameTime.TotalMilliseconds;
            if (_extrasLogMs < 1000.0) return;
            _extrasLogMs = 0.0;
            _monitor?.Log($"RewindPanScene: {Game1.season} {RewindReversedExtras.Positions()}", LogLevel.Trace);
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

        /// <summary>The last two seconds, fading out. Drawn by
        /// <see cref="OnRendered"/> rather than through Game1's own fade fields, which the engine's
        /// ScreenFade owns and would fight us for.</summary>
        private static void TickFade()
        {
            float fadeStart = PanDurationMs - FadeOutMs;
            _fadeAlpha = _elapsed <= fadeStart
                ? 0f
                : MathHelper.Clamp((_elapsed - fadeStart) / FadeOutMs, 0f, 1f);
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
                // The extras change with the map. See RewindReversedExtras.RefreshAppearance.
                RewindReversedExtras.RefreshAppearance();
                Gust();
            }
        }

        /// <summary>The wind for the whole pan, and the harder gust on each season swap: both are the
        /// same call. <c>populateDebrisWeatherArray</c> re-seeds debris positions AND its sprite index
        /// from the current season, so calling it again right after a tilesheet swap also refreshes the
        /// debris to match (leaves in Fall, snow in Winter) rather than leaving the old season's look
        /// blowing across the new one.</summary>
        /// <summary>The light end of the cycle: just under the hour this location and season start to
        /// darken, so the loop always reaches genuine full daylight (below that threshold
        /// <c>UpdateGameClock</c> leaves outdoorLight at the ambient and stops drawing lighting at
        /// all) and spends the whole second ramping rather than sitting flat.</summary>
        private static int DaylightTime()
            => Game1.getStartingToGetDarkTime(_town ?? Game1.currentLocation) - LightCycleDawnMargin;

        /// <summary>The sky for the season now on screen. See the weather constants for the shape of
        /// it. This sets the LOCATION's weather, which is what every one of vanilla's draw paths
        /// actually reads (<c>IsRainingHere</c>, <c>IsSnowingHere</c> and the rest all go through
        /// <c>GetWeather()</c>), and mirrors it onto the Game1 globals other code still uses. Rain
        /// needs its drop positions seeded or the flag draws nothing at all.</summary>
        private static void ApplyWeather(CoreSeason season)
        {
            if (_town == null) return;
            StardewValley.Network.LocationWeather weather = _town.GetWeather();
            bool rain = season == CoreSeason.Spring || season == CoreSeason.Summer;
            bool storm = season == CoreSeason.Summer;
            bool snow = season == CoreSeason.Winter;
            // NO PETALS UNDER RAIN OR SNOW. Game1.updateWeather returns early for a raining or snowing
            // location before it reaches the debris branch (Game1.cs:6169-6219), but the draw pass
            // still paints the debris (Game1.cs:13923), so the petals hung frozen in mid-air for the
            // whole rainy day ("The petals on the screen stopped moving during the rainy day", Jeff,
            // 2026-09-14). The wind comes back with ClearWeather.
            bool debris = !rain && !snow;

            weather.IsRaining = rain;
            weather.IsLightning = storm;
            weather.IsSnowing = snow;
            weather.IsDebrisWeather = debris;
            weather.IsGreenRain = false;    // explicitly out, by the designer's call

            Game1.isRaining = rain;
            Game1.isLightning = storm;
            Game1.isSnowing = snow;
            Game1.isDebrisWeather = debris;
            Game1.isGreenRain = false;

            if (!debris) Game1.debrisWeather?.Clear();
            if (rain) Game1.randomizeRainPositions();
            if (season == CoreSeason.Fall) Gale();
        }

        /// <summary>Back to an ordinary sky: the breeze that hides the tilesheet swaps stays, and
        /// everything else goes.</summary>
        private static void ClearWeather()
        {
            if (_town == null) return;
            StardewValley.Network.LocationWeather weather = _town.GetWeather();
            weather.IsRaining = false;
            weather.IsLightning = false;
            weather.IsSnowing = false;
            weather.IsGreenRain = false;
            weather.IsDebrisWeather = true;

            Game1.isRaining = false;
            Game1.isLightning = false;
            Game1.isSnowing = false;
            Game1.isGreenRain = false;
            Game1.isDebrisWeather = true;
            Game1.flashAlpha = 0f;
            // The petals ApplyWeather cleared for the rain or snow blow back in with the clear sky.
            if (Game1.debrisWeather == null || Game1.debrisWeather.Count == 0) Gust();
        }

        /// <summary>Switches this season's weather on for one day/night cycle and off again. See
        /// WeatherAfterSwapMs. The span of the pan that belongs to the season on screen runs from the
        /// swap that brought it in to the swap that takes it away, and the weather sits one cycle
        /// inside that, so it never lands on the same frame as the map changing.</summary>
        private static void TickWeather()
        {
            if (_seasons == null || _seasonIndex >= _seasons.Count) return;
            double spanStart = _seasonIndex == 0 ? 0.0 : _swaps[_seasonIndex - 1];
            double spanEnd = _seasonIndex < _swaps.Count ? _swaps[_seasonIndex] : 1.0;
            double startMs = spanStart * PanDurationMs;
            double endMs = spanEnd * PanDurationMs;

            double from = startMs + WeatherAfterSwapMs;
            double to = from + WeatherLastsMs;
            if (to > endMs)
            {
                // A season too short to hold a held-off day still gets one, centred in what it has.
                double middle = (startMs + endMs) / 2.0;
                from = middle - WeatherLastsMs / 2.0;
                to = middle + WeatherLastsMs / 2.0;
            }

            bool wanted = _elapsed >= from && _elapsed < to;
            if (wanted == _weatherOn) return;
            _weatherOn = wanted;
            if (wanted) ApplyWeather(_seasons[_seasonIndex]);
            else ClearWeather();
        }

        /// <summary>The storm's flashes, while Summer is the season on screen.
        ///
        /// <c>Game1.flashAlpha</c> is vanilla's own lightning flash and it fades itself, so this only
        /// has to strike it. What it deliberately does NOT do is call
        /// <c>Utility.performLightningUpdate</c>, which is the real strike: that one hits the farm,
        /// can kill crops and charge lightning rods, and none of that belongs in a scene that is a
        /// memory of a year rather than a night in it.</summary>
        private static void TickStorm(GameTime time)
        {
            if (_seasons == null || _seasonIndex >= _seasons.Count) return;
            if (!_weatherOn || _seasons[_seasonIndex] != CoreSeason.Summer) { _stormFlashMs = 0.0; return; }
            _stormFlashMs += time.ElapsedGameTime.TotalMilliseconds;
            if (_stormFlashMs < StormFlashEveryMs) return;
            _stormFlashMs = 0.0;
            Game1.flashAlpha = 1f;
        }

        private static void Gust()
        {
            // BOTH of these, and the location one is the one that draws. Game1.drawWeather gates the
            // debris on currentLocation.IsDebrisWeatherHere(), which reads the location's own
            // LocationWeather, not the Game1 flag: setting the flag alone left the season swaps
            // completely unhidden ("there's no wind blowing to obscure the season change", Jeff,
            // 2026-09-11). The Game1 flag stays set because other code still reads it.
            Game1.isDebrisWeather = true;
            if (_town != null) _town.GetWeather().IsDebrisWeather = true;
            Game1.populateDebrisWeatherArray();
        }

        /// <summary>Replaces vanilla's gust with a real one.
        ///
        /// <c>populateDebrisWeatherArray</c> makes between sixteen and sixty-four pieces of debris
        /// drifting at -0.2 to 0 across, which on screen is a breeze: "it was just gentle brezes the
        /// whole way through. it helped some, but is there a more windy option?" (Jeff, 2026-09-11).
        /// This keeps vanilla's call for the season-correct sprite index, then refills the array with
        /// far more of it moving far faster. Fall gets the most, that being the season the valley's
        /// windy day belongs to.</summary>
        private static void Gale()
        {
            if (Game1.debrisWeather == null) return;
            bool fall = _seasons != null && _seasonIndex < _seasons.Count
                        && _seasons[_seasonIndex] == CoreSeason.Fall;
            int count = fall ? FallGaleDebris : GaleDebris;
            int which = Game1.debrisWeather.Count > 0 ? Game1.debrisWeather[0].which : 0;
            Game1.debrisWeather.Clear();
            for (int i = 0; i < count; i++)
            {
                var at = new Vector2(
                    Game1.random.Next(0, Math.Max(1, Game1.viewport.Width)),
                    Game1.random.Next(0, Math.Max(1, Game1.viewport.Height)));
                float drift = GaleDriftX - (float)Game1.random.NextDouble() * GaleDriftSpread;
                Game1.debrisWeather.Add(new WeatherDebris(
                    at, which, (float)Game1.random.Next(15) / 500f, drift,
                    (float)Game1.random.NextDouble() * GaleFallY));
            }
        }

        /// <summary>Puts the town's own debris weather back. The wind is this scene's, not the
        /// save's, and a location's weather outlives the day it was set on.</summary>
        private static void RestoreTownWeather()
        {
            try
            {
                if (_town == null) return;
                StardewValley.Network.LocationWeather weather = _town.GetWeather();
                weather.IsDebrisWeather = _priorTownDebrisWeather;
                weather.IsRaining = _priorTownRain;
                weather.IsSnowing = _priorTownSnow;
                weather.IsLightning = _priorTownLightning;
                Game1.isRaining = _priorTownRain;
                Game1.isSnowing = _priorTownSnow;
                Game1.isLightning = _priorTownLightning;
                Game1.flashAlpha = 0f;
            }
            catch (Exception ex)
            {
                _monitor?.Log($"RewindPanScene: could not restore the town's weather: {ex.Message}", LogLevel.Warn);
            }
        }

        /// <summary>Normal end of the pan: the rewound season/clock/weather/camera are left exactly as
        /// the pan reached them (that IS the beat) for the next part of the sequence to paint over.
        /// Nothing of the pan's own is left in the world to take back.</summary>
        private static void Finish()
        {
            if (!_active) return;
            _active = false;
            RestoreTownWeather();
            RewindReversedExtras.Teardown();
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
            RewindReversedExtras.Teardown();
            _monitor?.Log($"RewindPanScene: forcing teardown ({reason}); restoring season, clock, weather and camera.", LogLevel.Warn);
            try
            {
                if (_town != null)
                {
                    Game1.season = _priorSeason;
                    _town.updateSeasonalTileSheets();
                }
                // Not past 2am, whatever it was. The clock this scene was handed IS 2600 (the
                // bedroom's painted failed night), and putting that back on a world with no menu on
                // it is the same pass-out trap Start documents: the abort path would hand the driver
                // a farmer who collapses seven seconds later. One minute short of it is close enough
                // for a path whose whole job is to leave the save usable.
                Game1.timeOfDay = Math.Min(_priorTimeOfDay, LastSafeClock);
                Game1.isDebrisWeather = _priorIsDebrisWeather;
                RestoreTownWeather();
                if (Game1.player != null) Game1.player.CanMove = _priorCanMove;
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
