using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using TheLongestYear.Core.Rewind;

namespace TheLongestYear.Integration
{
    /// <summary>Beats 11 to 14 of the rewind cutscene (design 2026-09-11): paint versus state. Once
    /// <see cref="RewindPanScene"/> (beat 10) finishes, the screen must LOOK like Spring 1 from that
    /// last frame through the day-28 FAIL choice, the pity dialogs and the Junimo Shrine shopping,
    /// until the player actually takes control and the clock starts. The real world reset does not
    /// have to happen during any of that; <see cref="TheLongestYear.Loop.WorldResetService"/> still
    /// runs where it always did (<c>WorldResetService.cs:419</c>) and reconciles everything for real,
    /// including <c>Game1.year = 1</c>.
    ///
    /// <see cref="Apply"/> paints <c>Game1.season</c>, <c>dayOfMonth</c>, <c>timeOfDay</c> and the
    /// three weather flags from <see cref="SpringPaint.Values"/> and then HOLDS them: it subscribes
    /// (via <see cref="Register"/>) to <c>GameLoop.UpdateTicked</c> and re-paints every tick until
    /// <see cref="Release"/> is called, the same way <see cref="RewindPanScene"/> drives its own
    /// fields every tick. A one-shot assignment is not enough here because the window this covers
    /// spans several menus and continuations (the choice, the shrine, the recipe-bank prompts) that
    /// run across many frames, any of which could otherwise leave the paint stale if something else
    /// nudges those globals in between.
    ///
    /// No <c>Year</c> field: <see cref="SpringPaintValues"/> deliberately does not carry one. Ruled
    /// 2026-09-11 (task 6): Stardew's HUD clock shows season and day only, never the year, so the
    /// year is not part of what the player sees during this window, and the real reset sets
    /// <c>Game1.year = 1</c> underneath once it lands. Confirmed by this task's own audit (below):
    /// nothing that can run inside the paint window displays <c>Game1.year</c> anywhere. The only
    /// places that read it at all are internal reset/gating logic (<c>WorldResetService.cs</c>,
    /// <c>RunController.cs</c>'s Year2WallRule check and a log line, <c>WeatherModificationsPatch.cs</c>'s
    /// green-rain seed) and two debug surfaces (<c>WorldStateProbe.Capture</c>, used only by the
    /// tly_leaktest console command; <c>ModEntry.CmdNetState</c>'s tly_netstate console log), none
    /// of them player-facing HUD/menu text, and none reachable without a developer typing a console
    /// command. No concern to raise there.
    ///
    /// AUDIT (step 3, spec's flagged risk): every hit of `Game1.season`, `Game1.dayOfMonth` or
    /// `Game1.timeOfDay` in src/TheLongestYear (grep below), checked against whether it can run
    /// between the end of the pan and the player regaining control (the FAIL choice, the pity
    /// re-asks, the Junimo Shrine and its Boosts/Plan tabs, the recipe-bank prompts):
    ///
    ///   grep -rn "Game1.season\|Game1.dayOfMonth\|Game1.timeOfDay" src/TheLongestYear/ --include=*.cs
    ///
    /// - Donations/BoostContextBuilder.cs (Build): reads Game1.season/dayOfMonth for the boost
    ///   purchase's DayOfYear. Reachable: ShrinePreviewMenu's Boosts tab calls it while shopping.
    ///   SAFE: before this task the shrine read the STALE pre-rewind date here (mismatched with the
    ///   NEW loop the purchase is actually for); the paint makes it read the date the purchase will
    ///   really apply under once the reset lands. No guard needed; this task improves it.
    /// - Integration/Day28CutsceneDriver.cs (line ~114): Game1.season only read in the
    ///   Day28Branch.Continue arm (Season Turn Beats). SAFE: Continue never rewinds and is never
    ///   painted; not reachable inside the FAIL/paint window.
    /// - Integration/FamiliarityGlue.cs (Rollup): Game1.dayOfMonth read from
    ///   RunController.OnDayEnding, before the day-28 bedtime cutscene (and therefore the pan and the
    ///   paint) even starts. SAFE: runs outside the window entirely.
    /// - Integration/RewindPanScene.cs: the pan itself (beat 10), which calls Finish() and hands off
    ///   to the paint's own beat before Apply() is ever called. SAFE: not concurrent with the paint.
    /// - Loop/BoostEffectsService.cs (Today, Active): feeds SecondWindTonight/FastFriendsActive/
    ///   HagglerActive, all gated behind gameplay the player cannot perform while frozen/menu-modal
    ///   (sleeping, shop haggling, gifting). SAFE: not reachable without player control.
    /// - Loop/EventSuppressionPatch.cs (Prefix): Harmony patch on
    ///   GameLocation.checkEventPrecondition, feeding EventGatingPolicy.Decide against
    ///   EventGatingTables.Default. CORRECTED (a prior version of this comment wrongly called that
    ///   table empty; checked EventGating.cs directly): Default now holds one holdUntilSpring5 entry
    ///   (DemetriusCaveEventId) and one furnace entry, so Decide CAN act on the painted season/day.
    ///   SAFE anyway, for two independent reasons: (1) checkForEvents (the only caller of
    ///   checkEventPrecondition) runs as part of a location's normal tick, which RewindPanScene.cs's
    ///   own class comment notes vanilla gates off while a menu is up ("Game1.UpdateGameClock ...
    ///   runs unconditionally while no menu or minigame is up"), and this window is menu-dominated
    ///   throughout (Day28CutsceneMenu, then the choice/pity dialogs, then ShrinePreviewMenu, then
    ///   the recipe-bank prompts, with TickShrineWatchdog immediately opening the next one); (2) even
    ///   in a one-frame gap between menus, the Spring-hold rule reading the painted Spring 1 would
    ///   suppress DemetriusCaveEventId exactly as it should once the real reset actually lands on
    ///   Spring 1 (this only paints on the FAIL/rewind branch, which really is about to become
    ///   Spring 1), so even a reachable hit is not a misbehavior here. The furnace rule does not
    ///   depend on season/day at all. No guard needed.
    /// - Loop/FestivalTimeFlow.cs: gated on Game1.isFestival, which cannot be true during the day-28
    ///   reset window. SAFE.
    /// - Loop/OnboardingMailService.cs (OnDayStarted): checked from SMAPI's DayStarted event, which
    ///   only fires at the real day transition, after FinalizeReset's real reset has landed, never
    ///   mid-cutscene. SAFE.
    /// - Loop/QueenOfSaucePatch.cs (Prefix): only runs when the player clicks a TV, which needs free
    ///   player control. SAFE: unreachable while frozen/menu-modal.
    /// - Loop/RunController.cs itself: the BeginNewRun/DoDayStartSeasonAndHub calendar syncs (lines
    ///   ~127, ~147, ~935, ~962) all run AFTER FinalizeReset's real PerformReset, by which point
    ///   Game1's real values already equal the painted ones. DebugSetDay's write is a manual-only
    ///   console command. SAFE. The "season was {Game1.season} {Game1.dayOfMonth}" log line in
    ///   FinalizeReset (~line 821) now logs the painted Spring 1 instead of the true pre-reset season
    ///   on the FAIL path, a minor diagnostic-text inaccuracy for developers only, not a player-facing
    ///   or gameplay concern, left as-is.
    /// - Loop/WorldResetService.cs (PerformReset): the real reset this paint mirrors and that
    ///   reconciles everything for real, including Game1.year. This is the goal state, not a risk.
    /// - Loop/WorldStateProbe.cs (Capture): only invoked by the tly_leaktest debug console command
    ///   (ModEntry.cs), never automatically. SAFE: debug-only, not player-visible.
    /// - ModEntry.cs: two different buckets, not one (a prior version of this comment wrongly lumped
    ///   them together as "every hit lives inside a Cmd* console-command handler," which is false):
    ///     - Sweep/crab-pot logs, CmdNetState, CmdTv and the rest of the Cmd* handlers: each hit lives
    ///       inside a Cmd* console-command method, triggered only by a developer typing a tly_ command.
    ///       SAFE: none fire automatically during the window.
    ///     - TodayDayOfYear() (~line 1583) is ALSO wired as three long-lived delegates in
    ///       ModEntry.OnSaveLoaded, none of them console commands, checked individually below:
    ///       - BoostChecker.YearTwoSeedsActive (~line 501): consumed by MixedSeedsPatch.Postfix, a
    ///         Harmony patch on Crop.ResolveSeedId, which only runs from Crop's constructor, i.e. when
    ///         a seed is actually planted on tilled dirt. That needs the player to walk, select a seed
    ///         item and use it, all of which need the free player control freezeControls/the modal
    ///         shrine/choice menus deny during this window. SAFE: not reachable without player control.
    ///       - BoostChecker.SneakPeekActive (~line 502): consumed by QueenOfSaucePatch.Prefix (see its
    ///         own entry below), which only runs when the player clicks a TV. SAFE: same reason.
    ///       - ActiveEffectsProvider.AttachBoosts (~line 707): consumed by
    ///         ActiveEffectsProvider.BonusStacks/ActiveBonus, which nine Harmony patches read
    ///         (AllDropsPatch, AnimalDoubleProductPatch, CropGrowthPatch, FishBiteRatePatch,
    ///         ForageYieldPatch, MachineSpeedPatch, MineDropsPatch, MonsterThemePatches,
    ///         TerrainBonusPatches). Checked each patch's Harmony target directly: all but two hook a
    ///         direct player tool/interaction call (performToolAction, DoFunction, checkAction,
    ///         OutputMachine, pullFishFromWater, takeDamage, monsterDrop, sellToStorePrice), none of
    ///         which the player can trigger without control. The other two (AnimalDoubleProductPatch's
    ///         FarmAnimal.dayUpdate, CropGrowthPatch's Crop.newDay) are overnight day-transition hooks,
    ///         not continuous ticks, and no day transition happens mid-window (the real day transition
    ///         is what FinalizeReset's PerformReset performs, and that lands the real Spring 1 before
    ///         these could read anything painted). SAFE: not reachable without player control or a day
    ///         transition, neither of which the window allows.
    /// - UI/ShrinePreviewMenu.cs (BuildForesight, lines ~141/158/163): reads Game1.dayOfMonth /
    ///   Game1.season DIRECTLY for the Weather Sage forecast seed and the Traveling Cart schedule,
    ///   while the rest of this same class (its own Today property) deliberately reads
    ///   _run.Season/_run.DayOfMonth (the real, not-yet-reset loop state) for everything else. This
    ///   IS the shrine open during the paint window. CONCERN (reported, not guarded): the forecast is
    ///   also seeded from Game1.uniqueIDForThisGame/Game1.stats.DaysPlayed, which stay the OLD
    ///   pre-reset values until PerformReset actually runs, so mixing the old id with the newly
    ///   painted Spring-1 date produces a seed combination that never really occurs in play (it did
    ///   not match before this task either, when it mixed the old id with the stale pre-rewind date,
    ///   so this is not a new break, just a different wrong forecast). The obvious-looking fix, read
    ///   _run.Season/_run.DayOfMonth here too to match Today, touches UI/ShrinePreviewMenu.cs, which
    ///   is outside this task's file list, so it is reported for a ruling rather than patched here.
    /// - UI/WeeklyHubMenu.cs (lines ~149/150): same WeatherForecast.Build call, but only opened by
    ///   DoDayStartSeasonAndHub, which always runs after the real reset has already landed. SAFE.
    /// </summary>
    internal static class RewindSpringPaint
    {
        private static IMonitor _monitor;
        private static bool _registered;
        private static bool _holding;

        /// <summary>Wires the per-tick hold. Safe to call more than once (later calls just refresh
        /// the stored monitor); the event subscription itself only happens on the first call. Call
        /// once from ModEntry.Entry, the same pattern <see cref="RewindPanScene.Register"/> uses.</summary>
        public static void Register(IMonitor monitor, IModHelper helper)
        {
            _monitor = monitor;
            if (_registered) return;
            _registered = true;
            helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
        }

        /// <summary>Paints Spring 1 now and starts holding it every tick until <see cref="Release"/>.
        /// Idempotent: calling this again while already holding just re-paints immediately.</summary>
        public static void Apply()
        {
            _holding = true;
            Paint();
        }

        /// <summary>Stops holding the paint once the player takes control and the clock starts. Does
        /// NOT revert the painted values; by the time this is called the real reset has already
        /// reconciled them (or is about to on the same tick), so there is nothing to restore to.</summary>
        public static void Release()
        {
            _holding = false;
        }

        private static void OnUpdateTicked(object sender, UpdateTickedEventArgs e)
        {
            if (_holding) Paint();
        }

        private static void Paint()
        {
            SpringPaintValues v = SpringPaint.Values();
            // Core.Season and StardewValley.Season share Spring=0..Winter=3 (see GameEffortData.cs;
            // RewindPanScene.cs uses the same cast).
            Game1.season = (StardewValley.Season)(int)v.Season;
            Game1.dayOfMonth = v.DayOfMonth;
            Game1.timeOfDay = v.TimeOfDay;
            Game1.isRaining = v.Raining;
            Game1.isSnowing = v.Snowing;
            Game1.isDebrisWeather = v.DebrisWeather;
        }
    }
}
