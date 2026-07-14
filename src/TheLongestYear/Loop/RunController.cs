using System;
using System.Collections.Generic;
using System.Linq;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using TheLongestYear.Core;
using CoreSeason = TheLongestYear.Core.Season;
using TheLongestYear.Core.Day28;

namespace TheLongestYear.Loop
{
    /// <summary>
    /// Drives the run loop: syncs RunState from the game date, opens the Sunday-night planning
    /// hub with the weekly selection offer, evaluates the day-end gate via RunManager + bundle
    /// requirements, and executes the action (fail → reset next morning, advance month → consume
    /// any day-28 pre-pick, win → log). JP banks live as donations happen (DonationService); nothing extra is awarded at run end.
    /// </summary>
    internal sealed class RunController
    {
        private readonly IMonitor _monitor;
        private readonly MetaStore _store;
        private readonly GameplayConfig _config;
        private readonly WorldResetService _reset;
        private readonly RunManager _runManager = new RunManager(new GateEvaluator());
        private readonly JpCalculator _jp;
        private readonly System.Collections.Generic.IReadOnlyList<CcItem> _catalog;
        private readonly System.Collections.Generic.IReadOnlyList<BundleRequirement> _requirements;

        /// <summary>Which day-28 bedtime cutscene is queued for the next morning (set in
        /// OnDayEnding from the gate's RunAction). The <see cref="TheLongestYear.Integration.Day28CutsceneDriver"/>
        /// reads this, plays the in-bed Junimo scene, and calls <see cref="OnCutsceneEnded"/> when
        /// it ends. None = no cutscene (normal day). Replaces the old _pendingReset bool: the reset
        /// now runs AFTER the FAIL cutscene's JP shop instead of straight out of OnDayStarted.</summary>
        private Day28Branch _pendingCutscene = Day28Branch.None;

        /// <summary>Exposed for the driver's per-tick decision.</summary>
        public Day28Branch PendingCutscene => _pendingCutscene;
        private TheLongestYear.UI.MenuLauncher _launcher;
        private WeeklyThemeQuestService _questService;

        /// <summary>A planning-hub offer that couldn't open because the menu surface was busy
        /// (e.g. the post-win keep-playing <c>DialogueBox</c> still closing when the new loop's
        /// reset fired). Held here and re-attempted by <see cref="TryDrainDeferredOffer"/> once
        /// the surface clears, so a blocked open is retried instead of silently lost.</summary>
        private (int week, CoreSeason? season)? _deferredOffer;

        /// <summary>Classified bundle requirements for this run; exposed for the UI + donation layer.</summary>
        public System.Collections.Generic.IReadOnlyList<BundleRequirement> Requirements => _requirements;

        /// <summary>CcItem catalog (rarity/season/theme metadata); exposed so the UI can look up
        /// per-season obtainability when computing the bonus-item preview per card.</summary>
        public System.Collections.Generic.IReadOnlyList<CcItem> Catalog => _catalog;

        public RunController(IMonitor monitor, MetaStore store, GameplayConfig config, WorldResetService reset,
            System.Collections.Generic.IReadOnlyList<CcItem> catalog,
            System.Collections.Generic.IReadOnlyList<BundleRequirement> requirements = null)
        {
            _monitor = monitor;
            _store = store;
            _config = config;
            _reset = reset;
            _jp = new JpCalculator(config.Jp);
            _catalog = (catalog != null && catalog.Count > 0) ? catalog : CcItemCatalog.Items;
            _requirements = requirements ?? new System.Collections.Generic.List<BundleRequirement>();
        }

        private RunState Run => _store.Run;

        /// <summary>The current attempt count (loop the player is on / won), surfaced so the
        /// <see cref="TheLongestYear.Integration.Day28CutsceneDriver"/> can pass it into the
        /// <see cref="TheLongestYear.UI.VictoryMenu"/> loop-count line.</summary>
        public int CurrentRunNumber => Run.RunNumber;

        /// <summary>Called from OnSaveLoaded: ensure the run has a seed.</summary>
        public void OnRunLoaded()
        {
            if (Run.Seed == 0)
                Run.Seed = NewSeed();

            // Month-rollover-on-load (khauser13 soft lock, 2026-06-10): when the player slept
            // into a new month and quit BEFORE completing its first day, the on-disk run-state
            // predates the rollover (vanilla saves before OnDayStarted runs BeginNewMonth). The
            // old blind `Run.Season = calendar` sync here destroyed the season mismatch that
            // DoDayStartSeasonAndHub uses to detect the rollover, so SelectedThemesThisMonth
            // kept LAST month's picks, accumulated past 4 themes, and OfferForWeek eventually
            // returned an EMPTY offer — an unclosable theme picker. Run the same rollover the
            // live day-start path would have: clear month state + consume the day-28 pre-pick,
            // then re-sample the bonus list for the new season. The effect/quest restore below
            // then operates on the rolled-over (correct) selection state.
            var calendarSeason = (CoreSeason)(int)Game1.season;
            if (calendarSeason != Run.Season)
            {
                _monitor.Log(
                    $"Load is in {calendarSeason} but run-state was saved in {Run.Season} — the quit " +
                    "raced the month rollover. Running BeginNewMonth now (clears last month's theme " +
                    "selections; consumes any day-28 pre-pick).",
                    LogLevel.Info);
                Run.BeginNewMonth(calendarSeason);
                if (Run.CurrentSelection.HasValue)
                {
                    PopulateBonusSlotsForCurrentSelection();
                    // Empty new-season pool for the pre-picked theme: lift the drawback now, same
                    // as the live rollover paths (SelectByName/DoDayStartSeasonAndHub). Safe re
                    // ordering: the effects-restore block below calls ActiveEffectsProvider.Set
                    // (which clears suppression) but then re-applies SuppressLiability because
                    // this sets Run.LiabilitySuppressedThisWeek.
                    ApplyEmptyPoolLiftIfNeeded();
                }
            }
            Run.DayOfMonth = Game1.dayOfMonth;

            // 2026-07-09 slot redesign migration: a mid-week save from an older version has the
            // legacy id-only bonus list but no slot goals. Re-sample once (the week's goals
            // re-roll — one-time, beta-acceptable) and rebuild the quest from slots.
            if (Run.CurrentSelection.HasValue
                && Run.CurrentWeekBonusSlots.Count == 0
                && Run.CurrentWeekBonusItems.Count > 0)
            {
                _monitor.Log(
                    "Migrating this week's bonus list to slot-based goals (one-time re-sample).",
                    LogLevel.Info);
                PopulateBonusSlotsForCurrentSelection();
                ApplyEmptyPoolLiftIfNeeded();
                _questService?.OnThemeSelected();
            }

            _monitor.Log($"Run {Run.RunNumber} ready (seed {Run.Seed}). {DescribeWeek()}", LogLevel.Info);

            // Diagnostic: surface this save's resolved Vault (bus-repair) bundle indices + gold so a
            // remixed-bundle save's renumbered vault room (e.g. 23-26 instead of the vanilla 34-37)
            // is visible in the log. Confirms VaultBundleMap derived the right indices from live
            // bundle data without needing a vault-payment playtest.
            var vaultParts = new System.Collections.Generic.List<string>();
            foreach (int idx in TheLongestYear.Integration.VaultBundleMap.Indices())
                vaultParts.Add($"{idx}={TheLongestYear.Integration.VaultBundleMap.GoldForIndex(idx):N0}g");
            _monitor.Log(
                $"Vault bundles (this save): {(vaultParts.Count > 0 ? string.Join(", ", vaultParts) : "none in bundle data")}",
                LogLevel.Info);

            // Restore active effects from persisted selection (if any).
            if (Run.CurrentSelection.HasValue)
            {
                var (bonus, liability) = ThemeModifiers.For(Run.CurrentSelection.Value);
                ActiveEffectsProvider.Set(bonus, liability);
                _monitor.Log(
                    $"Restored active effects for week {Run.WeekOfYear}: theme={Run.CurrentSelection}, " +
                    $"bonus={bonus}, liability={liability}" +
                    (Run.LiabilitySuppressedThisWeek ? " (suppressed)" : ""),
                    LogLevel.Info);

                // Sync the persisted "quest complete = liability lifted" state — the provider
                // resets to unsuppressed on Set above, so re-apply if the run-state flag is on.
                if (Run.LiabilitySuppressedThisWeek)
                    ActiveEffectsProvider.SuppressLiability();

                // 2026-05-28 fix: do NOT re-sweep on save load even if forage_off is active.
                // The sweep only needs to run when the theme is FRESHLY selected (via
                // SelectByName or BeginNewMonth's pre-pick activation) — that's when vanilla
                // has just spawned forage that we need to clean up. On a save reload the
                // forage state already reflects whatever was on the map at save time, and
                // ForageOffPatch's per-spawn suppression continues to block future spawns.
                // Re-sweeping on reload destroyed a user's intent to re-pick the week's
                // theme: they reloaded Spring 1 to switch from Mining → Foraging, but the
                // sweep had already wiped forage from a prior Mining-pick session.
            }
            else
            {
                ActiveEffectsProvider.Clear();
            }

            // Refresh the weekly quest's objective text against the restored ledger so a
            // save+reload mid-week reflects already-donated items.
            _questService?.OnRunLoaded();

            // Clear the stale vanilla "Rat Problem" quest (the CC is already open this run). The
            // Harmony prefix stops new adds; this strips it from a save that already received it.
            RatProblemQuestPatch.StripFromLog(_monitor);
        }

        /// <summary>
        /// Remove already-spawned wild forage from every outdoor non-mine location. Called when
        /// the active liability becomes forage_off, since vanilla's per-day forage spawn runs
        /// during newDay/save-load BEFORE the player picks the week's theme — meaning ForageOffPatch
        /// (which suppresses future spawns) can't catch day-1 forage. Without this sweep, the
        /// 2026-05-27 playtest found leek + horseradish on Spring 1 even after picking Mining.
        /// </summary>
        private void SweepExistingForage()
        {
            int removed = 0;
            int locationsTouched = 0;
            foreach (var loc in Game1.locations)
            {
                if (loc is StardewValley.Locations.MineShaft) continue;
                if (!loc.IsOutdoors) continue;

                var toRemove = new System.Collections.Generic.List<Microsoft.Xna.Framework.Vector2>();
                foreach (var pair in loc.objects.Pairs)
                {
                    if (pair.Value.IsSpawnedObject && pair.Value.isForage())
                        toRemove.Add(pair.Key);
                }
                foreach (var tile in toRemove)
                    loc.objects.Remove(tile);

                if (toRemove.Count > 0)
                {
                    locationsTouched++;
                    removed += toRemove.Count;
                }
            }

            _monitor.Log(
                $"forage_off sweep: removed {removed} spawned-forage objects from {locationsTouched} locations.",
                LogLevel.Info);
        }

        public void OnDayStarted(object sender, DayStartedEventArgs e)
        {
            // Runs before the cutscene early-return: vanilla's morning ownership pass has already
            // blanked an unowned/unnamed horse by now, and it must be repaired on day-28 mornings too.
            HorseCarryoverService.EnsureHorseNamed(_store.State, _monitor);

            if (_pendingCutscene != Day28Branch.None)
            {
                // A day-28 outcome is queued. The Day28CutsceneDriver plays the in-bed Junimo
                // scene this morning (UpdateTicked), then calls OnCutsceneEnded() to open the
                // JP shop + reset (Fail) or roll into the next season (Continue). Suppress the
                // normal season-sync/hub flow until the scene resolves — same shape as the old
                // _pendingReset early-return. Manual tly_reset intentionally stays raw.
                return;
            }

            DoDayStartSeasonAndHub();
        }

        /// <summary>Post-win choice dialog: after the shrine menu closes, ask the player
        /// whether to start a new loop (triggers PerformReset) or keep playing this run
        /// indefinitely (sets VictoryAcknowledged + falls through to the normal day-start
        /// flow with no reset). Uses vanilla's <c>createQuestionDialogue</c> so the prompt
        /// renders identically to other in-world Y/N choices the player has seen.</summary>
        private void ShowKeepPlayingChoice()
        {
            var responses = new[]
            {
                new StardewValley.Response("newLoop",     Strings.Get("dialog.win.new-loop")),
                new StardewValley.Response("keepPlaying", Strings.Get("dialog.win.keep-playing"))
            };
            string loopLine = WinSummary.LoopLine(Run.RunNumber);
            string prompt = Strings.Get("dialog.win.prompt",
                new Dictionary<string, string> { ["loopline"] = loopLine });

            GameLocation loc = Game1.currentLocation ?? Game1.player?.currentLocation;
            if (loc == null)
            {
                // Defensive: no location to host the dialogue. Default to keep-playing
                // (the safer choice — never destroys the won run's state silently).
                _monitor.Log("Post-win choice: no currentLocation available, defaulting to 'Keep playing'.", LogLevel.Warn);
                ApplyKeepPlaying();
                return;
            }

            loc.createQuestionDialogue(prompt, responses, (Farmer who, string key) =>
            {
                if (key == "newLoop")
                {
                    _monitor.Log("Post-win choice: 'Start a new loop' — triggering reset.", LogLevel.Info);
                    ContinueAfterResetSpend();
                }
                else
                {
                    _monitor.Log("Post-win choice: 'Keep playing this run' — VictoryAcknowledged set.", LogLevel.Info);
                    ApplyKeepPlaying();
                }
            });
        }

        /// <summary>"Keep playing" branch of the post-win choice. Marks VictoryAcknowledged
        /// (suppresses the popup on subsequent Winter 28 wins) and runs the normal day-start
        /// flow so the player lands on Spring 1 Year 2 with the planning hub.</summary>
        private void ApplyKeepPlaying()
        {
            _store.State.VictoryAcknowledged = true;
            _store.Save();   // persist immediately — no save-scum revert
            DoDayStartSeasonAndHub();
        }

        /// <summary>Try to open the Junimo Shrine menu; on close, run <paramref name="onContinue"/>.
        /// If the menu can't open (cutscene blocking, already-open menu), run onContinue
        /// immediately so the gameplay path never gets stranded waiting for a missed popup.</summary>
        private void TryOpenShrineThenContinue(System.Action onContinue)
        {
            _launcher?.OpenShrineShop();
            if (Game1.activeClickableMenu is TheLongestYear.UI.JunimoShrineMenu shrine)
            {
                shrine.exitFunction = () => onContinue();
                return;
            }
            // Menu didn't open — fall through.
            onContinue();
        }

        /// <summary>Called by <see cref="TheLongestYear.Integration.Day28CutsceneDriver"/> when the
        /// day-28 bedtime cutscene has finished. Clears the pending branch and runs its
        /// continuation: FAIL → JP shop, then on close PerformReset + forced full save
        /// (ContinueAfterResetSpend); CONTINUE → roll straight into the next season's day-start
        /// flow (no shop, no reset).</summary>
        public void OnCutsceneEnded()
        {
            Day28Branch branch = _pendingCutscene;
            _pendingCutscene = Day28Branch.None;

            switch (branch)
            {
                case Day28Branch.Fail:
                    // Hide the day/time HUD across the shop → reset so the stale (pre-rewind)
                    // calendar date isn't shown on the clock panel while the player shops.
                    // ContinueAfterResetSpend restores it once the world is back on Spring 1.
                    Game1.displayHUD = false;
                    TryOpenShrineThenContinue(ContinueAfterResetSpend);
                    break;
                case Day28Branch.Continue:
                    DoDayStartSeasonAndHub();
                    break;
                case Day28Branch.Win:
                    // After the win screen closes: open the JP-spend shrine (the player spends the JP
                    // banked across the run), then ask "start a new loop" vs "keep playing". Same order as the
                    // old _pendingWinChoice path, with the win screen now in front of it.
                    TryOpenShrineThenContinue(ShowKeepPlayingChoice);
                    break;
                case Day28Branch.None:
                default:
                    // Defensive: driver fired with nothing queued. Fall back to the normal flow
                    // so the morning is never stranded.
                    DoDayStartSeasonAndHub();
                    break;
            }
        }

        /// <summary>Debug: fire the SAME fail-reset flow a day-28 gate miss triggers
        /// (<see cref="OnDayStarted"/> line 175) — open the JP-spend shrine, then on close run
        /// <see cref="ContinueAfterResetSpend"/> (PerformReset + persist). Lets a playtest exercise
        /// the exact spend-at-shrine → reset → reload path without grinding to day 28. Unlike
        /// <c>tly_reset</c> (which resets raw, no shrine), this reproduces the real loop-boundary
        /// purchase flow that the JP-refund bug lived in.</summary>
        public void DebugForceFailReset()
        {
            _monitor.Log("tly_failreset: queuing the day-28 FAIL cutscene (Junimo → shrine → reset).", LogLevel.Info);
            // Set the pending branch so the Day28CutsceneDriver plays the real bedtime scene this
            // tick (the driver polls UpdateTicked, not just DayStarted), exercising the full
            // cutscene → shop → reset → forced-save path from anywhere. tly_reset stays raw.
            _pendingCutscene = Day28Branch.Fail;
        }

        /// <summary>Debug: queue the CONTINUE (gate-passed) day-28 cutscene so a playtest can watch
        /// the "great job, next season" branch without reaching a real passing day-28. Sets the
        /// pending branch; the Day28CutsceneDriver plays the scene this tick and OnCutsceneEnded
        /// rolls into DoDayStartSeasonAndHub (no shop, no reset).</summary>
        public void DebugForceContinueCutscene()
        {
            _monitor.Log("tly_day28continue: queuing the day-28 CONTINUE cutscene (Junimo → next season).", LogLevel.Info);
            _pendingCutscene = Day28Branch.Continue;
        }

        /// <summary>Debug: queue the WIN screen so a playtest can watch the win → shrine →
        /// keep-playing flow without grinding to a real Winter-28 win. Sets the pending branch;
        /// the Day28CutsceneDriver opens VictoryMenu this tick and OnCutsceneEnded opens the JP
        /// shrine then the keep-playing choice. Bypasses the VictoryAcknowledged "first win only"
        /// gate (that lives in OnDayEnding), so it is re-runnable from any loaded save.</summary>
        public void DebugForceWin()
        {
            _monitor.Log("tly_win: queuing the WIN screen (Junimos → shrine → keep-playing choice).", LogLevel.Info);
            _pendingCutscene = Day28Branch.Win;
        }

        /// <summary>Debug: jump the in-game date to <paramref name="day"/> of the current season so a
        /// playtest can sleep straight into the day-28 gate (and exercise the REAL sleep → morning
        /// cutscene timing) without grinding a whole month. Sets both the game date and the run's
        /// day so OnDayEnding's gate evaluates for the right day, plus the load-menu display field.</summary>
        public void DebugSetDay(int day)
        {
            Run.DayOfMonth = day;
            Game1.dayOfMonth = day;
            Game1.netWorldState.Value.Date.DayOfMonth = day;
            if (Game1.player != null)
                Game1.player.dayOfMonthForSaveGame = day;
            _monitor.Log(
                $"tly_setday: date set to {Run.Season} {day}. Sleep to trigger the day-{day} gate.",
                LogLevel.Info);
        }

        /// <summary>Continuation called after the JP-spend popup closes on a loop reset. Performs
        /// the actual world reset and resumes the normal day-start sync + hub trigger.</summary>
        private void ContinueAfterResetSpend() => FinalizeReset("shrine closed");

        /// <summary>
        /// THE one loop-reset finalizer (tech-debt consolidation, 2026-06-10). Every path that
        /// rewinds the world to Spring 1 of a new run MUST route through here so cross-cutting
        /// fixes land once: the real fail/win→new-loop reset calls it via
        /// <see cref="ContinueAfterResetSpend"/>, and the debug <c>tly_reset</c>/<c>tly_resetif</c>
        /// path calls it from <c>ModEntry.FullResetAndPresentOffer</c> — which previously
        /// hand-copied a subset (it missed ActiveEffectsProvider.Clear, so a debug reset leaked the
        /// old theme's bonus/liability, and skipped ForceFullSave, so it wasn't a faithful stand-in
        /// for a real reset — the v0.9.38 mine-elevator test fell into exactly that gap).
        /// NOTE: <see cref="ApplyKeepPlaying"/> is intentionally NOT routed through here — keeping
        /// a won run is not a reset (no PerformReset/BeginNewRun); it shares only the
        /// persist-immediately + day-start-hub tail.
        /// </summary>
        public void FinalizeReset(string reason)
        {
            _monitor.Log(
                $"FinalizeReset ({reason}): applying reset (eventUp={Game1.eventUp}, " +
                $"farmEvent={Game1.farmEvent?.GetType().Name ?? "none"}, season was {Game1.season} {Game1.dayOfMonth}).",
                LogLevel.Info);
            // Capture a partial-reset failure explicitly. PerformReset changes uniqueIDForThisGame
            // early then does heavy world work; if it threw mid-way the old code swallowed it up the
            // stack and the game limped on in a half-reset state. Log the full exception, then
            // rethrow (behaviour-preserving) so nothing is silently masked.
            try
            {
                _reset.PerformReset();
            }
            catch (System.Exception ex)
            {
                _monitor.Log($"FinalizeReset ({reason}): PerformReset threw — reset NOT applied: {ex}", LogLevel.Error);
                throw;
            }
            _reset.ProfessionPicker.DrainOnDayStart();
            Run.BeginNewRun(NewSeed());
            ActiveEffectsProvider.Clear();
            // Persist the post-reset meta (JP spent at the shrine, new OwnedUpgrades, the bumped
            // run/reset counters) IMMEDIATELY. A deferred SaveLoaded fires after the in-place reset
            // and calls MetaStore.Load(), which would otherwise overwrite our in-memory state with
            // the stale on-disk meta from before the shrine was opened — refunding the JP the player
            // just spent and dropping their purchases (2026-06-01 playtest: "it refunded all my JP").
            _store.Save();
            ForceFullSave();
            _monitor.Log($"Loop reset complete. Run {Run.RunNumber} begins (seed {Run.Seed}).", LogLevel.Info);
            DoDayStartSeasonAndHub();
            // Re-persist the meta AFTER the hub opened. DoDayStartSeasonAndHub → PresentOffer sets
            // Run.OfferPresentedWeek when the week-1 hub appears, but that happens after the Save()
            // above — so the on-disk run-state still has the marker UNSET. A deferred SaveLoaded
            // (fired by the in-place reset) then calls MetaStore.Load(), reverting the in-memory
            // marker to that stale value; the next OnDayStarted sees OfferPresentedWeek != WeekOfYear
            // and RE-presents the offer. Because the first pick is now in SelectedThemesThisMonth,
            // OfferForWeek re-rolls a different pair and the second pick OVERWRITES the first
            // (2026-06-08 playtest "double-pick theme on reset"). Saving the now-set marker makes
            // the deferred reload read it back as presented, so the day-start guard skips the re-fire.
            _store.Save();
            // Restore the HUD hidden for the FAIL cutscene's shop→reset window (OnCutsceneEnded).
            // Safe/no-op on the non-cutscene paths that also call this (post-win, debug, fallback).
            Game1.displayHUD = true;
        }

        /// <summary>Write a full game save right after the in-place reset so the on-disk save —
        /// whose folder + inner files PerformReset just renamed to the new uniqueID — holds the
        /// consistent Spring 1 world NOW, closing the rename window the reset would otherwise leave
        /// open until the next natural sleep (batch-B notes §4). <c>SaveGame.Save()</c> is an
        /// enumerator that runs the write on a background task and yields until done, so we drain
        /// it to completion. Guarded: SaveGame.Save no-ops while an event/minigame is up, and any
        /// failure degrades to the existing inner-file-rename mitigation (the save stays loadable;
        /// the next sleep rewrites it) rather than aborting the reset.</summary>
        private void ForceFullSave()
        {
            try
            {
                if (Game1.eventUp || Game1.currentMinigame != null)
                {
                    _monitor.Log(
                        "Day-28 save: skipped (event/minigame active). Save stays loadable via the " +
                        "inner-file rename; the next sleep will rewrite it.",
                        LogLevel.Warn);
                    return;
                }

                var save = StardewValley.SaveGame.Save();
                while (save.MoveNext()) { }

                _monitor.Log(
                    "Day-28 save: full save written post-reset — save folder is now consistent at Spring 1.",
                    LogLevel.Info);

                // The new canonical save folder now exists on disk. Delete the pre-reset folder so
                // the reset leaves exactly one save (no "None2_" duplicate). Done only here, after a
                // confirmed save, so a failure/kill earlier never destroys the only loadable copy.
                _reset.CleanupAbandonedSaveFolder();
            }
            catch (System.Exception ex)
            {
                _monitor.Log(
                    $"Day-28 save: forced save failed ({ex.Message}). Save remains loadable via the " +
                    "inner-file rename; the next natural sleep will rewrite it.",
                    LogLevel.Warn);
            }
        }

        /// <summary>The post-reset (or no-reset-this-day) day-start work: sync season from
        /// Game1.season, advance month if it changed, fire the week-start planning hub if
        /// it's a week-start morning. Extracted 2026-05-29 so the reset path can defer
        /// through a JP-spend popup and still re-enter this same logic via the menu's
        /// exitFunction callback.</summary>
        private void DoDayStartSeasonAndHub()
        {
            // Sync state from the game date; a new month clears the month's selections (the
            // previous-day's Sunday-night day-28 pre-pick is consumed inside BeginNewMonth →
            // CurrentSelection).
            var season = (CoreSeason)(int)Game1.season;
            if (season != Run.Season)
            {
                Run.BeginNewMonth(season);
                // BeginNewMonth may have just installed NextMonthSelection as CurrentSelection;
                // the bonus list still reflects last month's season, so re-sample now.
                if (Run.CurrentSelection.HasValue)
                {
                    PopulateBonusSlotsForCurrentSelection();
                    var (bonus, liability) = ThemeModifiers.For(Run.CurrentSelection.Value);
                    ActiveEffectsProvider.Set(bonus, liability);
                    _monitor.Log(
                        $"Day-28 pre-pick applied: {Run.CurrentSelection} is the week-1 selection of {season}.",
                        LogLevel.Info);

                    ApplyEmptyPoolLiftIfNeeded();

                    // Spawn the weekly tracker for the freshly-activated pre-pick.
                    _questService?.OnThemeSelected();

                    // Same reasoning as SelectByName/OnRunLoaded: vanilla already ran the day's
                    // spawnObjects pass, so a freshly-activated forage_off has to clean up.
                    if (liability == "forage_off")
                        SweepExistingForage();
                }
            }
            Run.Season = season;
            Run.DayOfMonth = Game1.dayOfMonth;

            // Open the weekly planning hub on every week-start morning (days 1, 8, 15, 22).
            // This replaces the prior Sunday-night DayEnding trigger, which fired during the
            // sleep/save sequence when Game1.player.CanMove == false — MenuLauncher.CanOpen
            // blocked the menu and the hub never appeared (2026-05-26 playtest:
            // "Cannot open menu: cutscene or input lock" right before "starting spring 8").
            // OnDayStarted runs after the wake-up cutscene resolves, so the menu opens cleanly.
            //
            // OfferPresentedWeek guards against re-firing within the same week (e.g. if the
            // player saves + reloads on day 8 morning).
            // On the fresh-run intro morning the IntroSequenceDriver opens the picker at the end
            // of the Lewis->Junimo chain. Suppress the normal auto-open here so it doesn't pop on
            // the black save-creation screen (and so it isn't opened twice).
            bool introWillOpenPicker = TheLongestYear.Core.Intro.IntroGate.IsFreshIntroMorning(
                _store.State.HasSeenIntro, Run.Season, Run.DayOfMonth);

            if (!introWillOpenPicker
                && Calendar.IsWeekStart(Run.DayOfMonth)
                && Run.OfferPresentedWeek != Run.WeekOfYear)
            {
                // CurrentSelection from a previous week intentionally persists until the next
                // pick overwrites it — the hub still opens to let the player choose this week's
                // theme. OfferPresentedWeek is per-week + persists across save reload, so the
                // hub fires exactly once per week (no repeat on load).
                PresentOffer(targetWeekOfYear: Run.WeekOfYear);
            }
        }

        public void OnDayEnding(object sender, DayEndingEventArgs e)
        {
            TheLongestYear.Integration.VaultPaymentSync.Reconcile(Run);
            // Backstop the live DonationObserver: union any bundle slot vanilla shows as deposited
            // into the ledger before the gate reads it, so a deposit the observer missed can't
            // fail an otherwise-complete season (beta report, khauser13).
            TheLongestYear.Integration.ItemDonationSync.Reconcile(Run);
            bool vaultGateSatisfied = VaultRules.IsVaultGateSatisfied(Run.Season, Run, _store.State);
            RunAction action = _runManager.EvaluateDayEnd(Run, _requirements, vaultGateSatisfied);
            switch (action)
            {
                case RunAction.Continue:
                    break;

                case RunAction.AdvanceMonth:
                    _monitor.Log($"Month cleared ({Run.Season}). Advancing.", LogLevel.Info);
                    // Season-checkpoint award (spec 2026-07-14 economy Change 2): pays at the ENTERING
                    // season's multiplier so progressing always out-earns re-farming spring.
                    long checkpointJp = JpBoostHelper.Apply(_store.State, _jp.CheckpointBonus(Run.WeekOfYear + 1));
                    _store.State.JunimoPoints += checkpointJp;
                    _monitor.Log(
                        $"Season checkpoint passed -> +{checkpointJp} JP (now {_store.State.JunimoPoints}).",
                        LogLevel.Info);
                    Game1.addHUDMessage(new HUDMessage(
                        Strings.Get("hud.checkpoint-award", new Dictionary<string, string> { ["jp"] = checkpointJp.ToString() }),
                        HUDMessage.newQuest_type));
                    // Queue the "great job, next season" Junimo cutscene for the morning. The
                    // game still advances the date; OnCutsceneEnded → DoDayStartSeasonAndHub
                    // clears the month's selections and opens the planning hub after the scene.
                    _pendingCutscene = Day28Branch.Continue;
                    break;

                case RunAction.FailReset:
                    // The morning rewind un-restores every CC room. Strip any room the player
                    // FINISHED TODAY out of mailForTomorrow so its overnight restoration scene
                    // (the bus/greenhouse/minecart WorldChangeEvent) never plays — otherwise the
                    // player watches the Junimos lovingly fix the bus seconds before the loop
                    // rewinds it broken again (user feedback 2026-06-08). This also removes the
                    // bus-scene-vs-reset race that was the mechanism of #1b.
                    SuppressResetDoomedRoomScenes();
                    _pendingCutscene = Day28Branch.Fail;
                    break;

                case RunAction.Win:
                    // 2026-05-29 continue-after-victory: only award the win-JP + queue the
                    // post-win choice popup on the FIRST win this playthrough. Subsequent
                    // Winter 28 wins (after the player chose Keep playing) re-fire RunAction.Win
                    // but should be silent — we don't want to double-pay JP or re-ask the
                    // question we already answered.
                    if (!_store.State.VictoryAcknowledged)
                    {
                        // Queue the win screen for the morning. Routes through the same
                        // Day28CutsceneDriver/OnCutsceneEnded path as Fail/Continue (the driver
                        // opens VictoryMenu for the Win branch); OnCutsceneEnded then opens the
                        // JP shrine and the keep-playing choice. Replaces the old _pendingWinChoice.
                        _pendingCutscene = Day28Branch.Win;
                    }
                    break;
            }
            // Hub trigger now lives in OnDayStarted (above) — see note there. Sunday-night
            // DayEnding fires while the player can't open menus.
        }

        /// <summary>Remove this-night's CC room-restoration mail so the matching overnight
        /// WorldChangeEvent doesn't play on a fail loop (the rewind un-restores the room, so the
        /// scene would show a repair the world is about to undo). Only rooms completed TODAY carry a
        /// mailForTomorrow entry — rooms finished on earlier days already played their scene and are
        /// untouched. A no-op (other than a trace) when nothing was finished today. The list +
        /// removal live in <see cref="CcRestorationMail"/> (shared with the reset purge).</summary>
        private void SuppressResetDoomedRoomScenes()
        {
            var stripped = CcRestorationMail.PurgeFromMailForTomorrow(Game1.player);
            if (stripped.Count > 0)
                _monitor.Log(
                    $"Fail loop: suppressed {stripped.Count} reset-doomed CC restoration scene(s) " +
                    $"([{string.Join(", ", stripped)}]) — no \"fix the bus\" cutscene before the rewind undoes it.",
                    LogLevel.Info);
            else
                _monitor.Log("Fail loop: no CC room finished today, so no overnight restoration scene to suppress.", LogLevel.Trace);
        }

        private static CoreSeason NextSeason(CoreSeason s) => s switch
        {
            CoreSeason.Spring => CoreSeason.Summer,
            CoreSeason.Summer => CoreSeason.Fall,
            CoreSeason.Fall => CoreSeason.Winter,
            _ => CoreSeason.Spring
        };

        /// <summary>Select one of this week's offered themes (driven by the UI; debug command + UI).</summary>
        public void SelectByName(string themeName, bool skipOfferCheck = false)
        {
            if (!Enum.TryParse(themeName, ignoreCase: true, out Theme theme))
            {
                _monitor.Log($"Unknown theme '{themeName}'. Options: {string.Join(", ", Enum.GetNames(typeof(Theme)))}.", LogLevel.Warn);
                return;
            }

            // skipOfferCheck = true: invoked from the playtest re-roll path on the hub. The
            // canonical OfferForWeek is seeded-deterministic and reflects only the originally
            // rolled pair, so a rerolled theme would always be rejected here. The reroll path
            // already filters against SelectedThemesThisMonth, so the only invariant we'd
            // lose by skipping is "the theme was in this week's official offer" — by design.
            if (!skipOfferCheck)
            {
                var offer = SelectionService.OfferForWeek(Run);
                if (!offer.Contains(theme))
                {
                    _monitor.Log($"{theme} is not offered this week. Offer: {string.Join(", ", offer)}.", LogLevel.Warn);
                    return;
                }
            }

            Run.Select(theme);
            // A made pick CONSUMES the week's offer, however it was made (hub card, rerolled
            // card, console). Mark the week presented and drop any deferred re-present for it —
            // otherwise a stale deferred offer (stashed while a picker was already up) drains the
            // moment the pick closes the hub and opens a SECOND picker, whose next pick then
            // overwrites this one (2026-07-09 playtest: picked Farming, ghost picker popped,
            // Mixed overwrote Farming).
            Run.OfferPresentedWeek = Run.WeekOfYear;
            if (_deferredOffer is { } stale && stale.week == Run.WeekOfYear)
                _deferredOffer = null;
            PopulateBonusSlotsForCurrentSelection();
            var (bonus, liability) = ThemeModifiers.For(theme);
            ActiveEffectsProvider.Set(bonus, liability);
            ApplyEmptyPoolLiftIfNeeded();
            _monitor.Log(
                $"Selected {theme} (bonus {bonus}, liability {liability}). " +
                $"Goal slots this week: [{string.Join(", ", Run.CurrentWeekBonusSlots.Select(s => $"{s.ItemId}@{s.BundleName}#{s.IngredientIndex}"))}].",
                LogLevel.Info);

            // Surface the weekly theme + bonus checklist as a quest entry.
            _questService?.OnThemeSelected();

            // Sweep already-spawned forage if forage_off just activated — vanilla's day-start
            // spawnObjects already ran on each outdoor location during the load/sleep sequence
            // (well before the player got to the planning hub), so the Harmony prefix on
            // future spawnObjects calls is too late for today's wild forage on the maps.
            if (liability == "forage_off")
                SweepExistingForage();
        }

        /// <summary>Sample the per-week goal slots for the current selection and store them on
        /// RunState. Clears the legacy id list so post-migration saves stop carrying it.</summary>
        private void PopulateBonusSlotsForCurrentSelection()
        {
            Run.CurrentWeekBonusSlots.Clear();
            Run.CurrentWeekBonusItems.Clear();
            if (!Run.CurrentSelection.HasValue) return;
            var sample = SampleSlotsForTheme(Run.CurrentSelection.Value, Run.Season, Run.WeekOfYear);
            Run.CurrentWeekBonusSlots.AddRange(sample);
        }

        /// <summary>Empty goal pool (everything for this theme already donated): no quest this
        /// week, drawback auto-lifted, no weekly JP bonus (spec 2026-07-09 §3). The
        /// LiabilitySuppressedThisWeek flag doubles as the completion-reward idempotency guard,
        /// so setting it here also prevents a later JP payout.</summary>
        private void ApplyEmptyPoolLiftIfNeeded()
        {
            if (!Run.CurrentSelection.HasValue) return;
            if (Run.CurrentWeekBonusSlots.Count > 0) return;
            if (Run.LiabilitySuppressedThisWeek) return;

            Run.LiabilitySuppressedThisWeek = true;
            ActiveEffectsProvider.SuppressLiability();
            Game1.addHUDMessage(new HUDMessage(
                Strings.Get("hud.nothing-to-donate"),
                HUDMessage.newQuest_type));
            _monitor.Log(
                $"Weekly goal pool for {Run.CurrentSelection} is empty (all in-play slots donated) - " +
                "no quest this week; drawback auto-lifted, no weekly JP bonus.",
                LogLevel.Info);
        }

        /// <summary>Rarity lookup for the bonus sampler: pull from the pre-built CcItem catalog so
        /// the hot path doesn't re-resolve via ItemRegistry per call. Unknown ids default to Common
        /// (full weight) so SVE/mod additions still surface in the bonus pool.</summary>
        private Rarity RarityForItem(string itemId)
        {
            foreach (var item in _catalog)
                if (item.Id == itemId)
                    return item.Rarity;
            return Rarity.Common;
        }

        /// <summary>Public mirror so the planning-hub UI's preview path uses the exact same rarity
        /// lookup as the selection-time commit. Keeps the two samples deterministically aligned.</summary>
        public Rarity GetRarityForItem(string itemId) => RarityForItem(itemId);

        /// <summary>Live per-slot completion state for a bundle (vanilla source of truth), or
        /// null when absent. Same NetBundles access pattern as ItemDonationSync/VaultPaymentSync:
        /// FieldDict.ContainsKey is the safe presence check.</summary>
        internal static bool[] SlotStateForBundle(int bundleIndex)
        {
            var bundles = Game1.netWorldState?.Value?.Bundles;
            if (bundles?.FieldDict == null) return null;
            return bundles.FieldDict.ContainsKey(bundleIndex) ? bundles[bundleIndex] : null;
        }

        /// <summary>Sample this week's goal slots for a theme+season — shared by the hub preview
        /// and the selection-time commit so both show the same goals. Pool = open, in-play slots
        /// (already-donated slots are never sampled; a complete bundle's leftover lines are dead).
        /// The pool is re-derived from live CC state at call time; the selection-time result is
        /// persisted in RunState.CurrentWeekBonusSlots, so committed goals don't reshuffle as
        /// slots complete mid-week.</summary>
        public System.Collections.Generic.IReadOnlyList<BonusSlot> SampleSlotsForTheme(
            Theme theme, CoreSeason season, int weekOfYear)
        {
            var bundleData = Game1.netWorldState?.Value?.BundleData;
            if (bundleData == null) return System.Array.Empty<BonusSlot>();
            var pool = SlotPoolBuilder.OpenSlotsForTheme(
                bundleData, SlotStateForBundle, _requirements,
                theme, season, id => IsObtainableInSeason(id, season));
            return BonusSlotSampler.SampleSlots(
                Run.Seed, weekOfYear, theme, pool, RarityForItem, BonusListSizeFor(season));
        }

        /// <summary>How big the per-card bonus-item preview list should be for the given season.
        /// Lives in <see cref="BonusItemSampler.DefaultMaxCountBySeason"/>.</summary>
        public int BonusListSizeFor(CoreSeason season)
            => BonusItemSampler.DefaultMaxCountBySeason[(int)season];

        /// <summary>How big the per-card bonus-item preview list should be for the current season.
        /// Lives in <see cref="BonusItemSampler.DefaultMaxCountBySeason"/>.</summary>
        public int BonusListSizeForCurrentSeason()
            => BonusListSizeFor(Run.Season);

        /// <summary>
        /// Number of weather preview days to reveal (the next N days, starting tomorrow).
        /// Equals the highest Weather Sage tier owned (weather_sage_1 through weather_sage_6).
        /// Returns 0 if none owned.
        /// </summary>
        public int WeatherSageTier()
            => _store.State.HighestKeptTier("weather_sage_", 6);

        /// <summary>
        /// Number of Traveling Cart item slots to preview on the planning hub.
        /// Equals 2 * highest Cart Whisperer tier owned (cart_whisper_1 through cart_whisper_5).
        /// Returns 0 if none owned.
        /// </summary>
        public int CartPreviewSlots()
            => CartStockPreview.SlotsToReveal(_store.State.HighestKeptTier("cart_whisper_", 5));

        /// <summary>Obtainability predicate for the sampler: looks up the item in the CcItem
        /// catalog and tests against this season's ObtainableSeasons. Items not in the catalog
        /// default to obtainable so SVE/mod additions aren't silently excluded.</summary>
        private bool IsObtainableInCurrentSeason(string itemId)
            => IsObtainableInSeason(itemId, Run.Season);

        /// <summary>Mirror for the UI: same predicate, callable from the menu's bonus-preview path.</summary>
        public bool IsObtainableForCurrentSeason(string itemId)
            => IsObtainableInCurrentSeason(itemId);

        /// <summary>Same predicate but for an arbitrary season — used by the Sunday-night day-28
        /// hub when previewing NEXT season's bonus pool.</summary>
        public bool IsObtainableInSeason(string itemId, CoreSeason season)
        {
            foreach (var item in _catalog)
                if (item.Id == itemId)
                    return item.ObtainableSeasons.Contains(season);
            return true;
        }

        /// <summary>Day-28 Sunday-night flow: store the player's pick for week 1 of next month.</summary>
        public void PreSelectForNextMonth(Theme theme)
        {
            Run.NextMonthSelection = theme;
            _monitor.Log(
                $"Pre-pick set: {theme} will be the week-1 selection of {NextSeason(Run.Season)}.",
                LogLevel.Info);
        }

        /// <summary>Simulate a CC donation (the real donation surface is via DonationService).</summary>
        public void Donate(string itemId)
        {
            Run.RecordDonation(itemId);
            _monitor.Log($"Donated '{itemId}'. Ledger size {Run.DonatedItemIds.Count}.", LogLevel.Info);
        }

        public void PrintRunState()
        {
            _monitor.Log(
                $"Run {Run.RunNumber}: {Run.Season} day {Run.DayOfMonth} (week {Run.WeekOfYear}). " +
                $"Selection={Run.CurrentSelection?.ToString() ?? "none"}, " +
                $"selectedThisMonth=[{string.Join(",", Run.SelectedThemesThisMonth)}], " +
                $"donated={Run.DonatedItemIds.Count}, JP banked={_store.State.JunimoPoints}.",
                LogLevel.Info);
        }

        /// <summary>Wired by ModEntry after the launcher is constructed.</summary>
        public void AttachLauncher(TheLongestYear.UI.MenuLauncher launcher) => _launcher = launcher;

        /// <summary>Wired by ModEntry after the quest service is constructed. Drives the per-week
        /// quest in the player's quest log — created on theme selection, refreshed on donation,
        /// auto-completed when every goal slot is complete.</summary>
        public void AttachQuestService(WeeklyThemeQuestService quest) => _questService = quest;

        /// <summary>Open the planning hub for a specific upcoming week. Sunday-night flow passes
        /// <c>Run.WeekOfYear + 1</c> (and a <paramref name="seasonOverride"/> on day 28) so the
        /// offer pool reflects what the player is actually choosing for.</summary>
        public void PresentOffer(int? targetWeekOfYear = null, CoreSeason? seasonOverride = null)
        {
            int week = targetWeekOfYear ?? Run.WeekOfYear;

            if (Run.OfferPresentedWeek == week)
            {
                _monitor.Log($"PresentOffer: already shown for week {week}, skipping.", LogLevel.Trace);
                return;
            }

            var selectionsForOffer = seasonOverride.HasValue
                ? (System.Collections.Generic.IReadOnlyCollection<Theme>)System.Array.Empty<Theme>()
                : Run.SelectedThemesThisMonth;
            var offer = SelectionService.OfferForWeek(Run.Seed, week, selectionsForOffer);

            // Backstop (khauser13 soft lock): the hub has no close button by design — a forced
            // 1-of-N choice — so an EMPTY offer would hard-lock the player. The month rollover
            // fix in OnRunLoaded removes the known cause (a stale month exhausting all 5 themes),
            // but if any state ever exhausts the pool again, skip the week instead of locking:
            // mark it presented (no theme, no bonus, no liability) and move on.
            if (offer.Count == 0)
            {
                _monitor.Log(
                    $"Week {week} offer is EMPTY (selected this month: " +
                    $"[{string.Join(",", Run.SelectedThemesThisMonth)}]). Skipping the theme pick " +
                    "this week instead of opening an unclosable hub.",
                    LogLevel.Warn);
                Run.OfferPresentedWeek = week;
                _deferredOffer = null;
                return;
            }

            string seasonTag = seasonOverride.HasValue ? $" (for {seasonOverride.Value})" : "";
            _monitor.Log(
                $"Week {week}{seasonTag} selection offer: {string.Join(" OR ", offer)} (opening planning hub).",
                LogLevel.Info);

            bool opened = _launcher?.OpenWeeklyHub(seasonOverride) ?? false;
            if (opened)
            {
                // Mark the week presented ONLY once the hub is genuinely on screen. Advancing
                // it before a confirmed open (the original bug) marks the week "offered" even
                // when the open was refused, so the day-start guard never re-fires it and the
                // theme picker is lost for the week (2026-06-05 playtest: win → "start a new
                // loop" → no theme picker, because the keep-playing DialogueBox was still the
                // active menu when this ran).
                Run.OfferPresentedWeek = week;
                _deferredOffer = null;
            }
            else
            {
                // Surface busy (menu/cutscene up). Keep OfferPresentedWeek unadvanced and stash
                // the offer; TryDrainDeferredOffer re-attempts it once activeClickableMenu clears.
                _deferredOffer = (week, seasonOverride);
                _monitor.Log(
                    $"Planning hub blocked (a menu/cutscene is up); deferring the week {week} offer to a free tick.",
                    LogLevel.Trace);
            }
        }

        /// <summary>Re-attempt a planning-hub open that <see cref="PresentOffer"/> deferred because
        /// the menu surface was busy. ModEntry's update loop calls this each tick once
        /// <c>Game1.activeClickableMenu</c> is clear; a no-op when nothing is pending.</summary>
        public void TryDrainDeferredOffer()
        {
            if (_deferredOffer is not { } pending)
                return;
            // Clear first so a still-blocked re-open simply re-stashes via PresentOffer rather
            // than looping. The week is not yet marked presented, so PresentOffer's own guard
            // lets the retry through.
            _deferredOffer = null;
            // A deferred offer for a week that's no longer current is garbage (the week rolled
            // over, or a reset replaced the run while it sat pending) — drop it instead of
            // presenting a picker for a week the player is no longer in.
            if (pending.week != Run.WeekOfYear)
            {
                _monitor.Log(
                    $"Dropping stale deferred offer for week {pending.week} (current week {Run.WeekOfYear}).",
                    LogLevel.Trace);
                return;
            }
            PresentOffer(pending.week, pending.season);
        }

        private string DescribeWeek() => $"{Run.Season} day {Run.DayOfMonth} (week {Run.WeekOfYear}).";

        private static int NewSeed() => Guid.NewGuid().GetHashCode();
    }
}
