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
    /// any day-28 pre-pick, win → log). Interim JP is banked on run end.
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
        private readonly System.Collections.Generic.IReadOnlyDictionary<string, int> _ingredientStacks;
        private readonly System.Collections.Generic.IReadOnlyDictionary<string, int> _ingredientQualities;

        /// <summary>Which day-28 bedtime cutscene is queued for the next morning (set in
        /// OnDayEnding from the gate's RunAction). The <see cref="TheLongestYear.Integration.Day28CutsceneDriver"/>
        /// reads this, plays the in-bed Junimo scene, and calls <see cref="OnCutsceneEnded"/> when
        /// it ends. None = no cutscene (normal day). Replaces the old _pendingReset bool: the reset
        /// now runs AFTER the FAIL cutscene's JP shop instead of straight out of OnDayStarted.</summary>
        private Day28Branch _pendingCutscene = Day28Branch.None;

        /// <summary>Exposed for the driver's per-tick decision.</summary>
        public Day28Branch PendingCutscene => _pendingCutscene;
        /// <summary>Set in OnDayEnding's Win branch when the loop is completed (CC restored on
        /// Winter 28). Consumed on the next OnDayStarted: opens the JP-spend shrine, then asks
        /// the player to choose "Start a new loop" or "Keep playing this run". Suppressed when
        /// <c>MetaState.VictoryAcknowledged</c> is already set (player previously chose Keep).</summary>
        private bool _pendingWinChoice;
        private TheLongestYear.UI.MenuLauncher _launcher;
        private WeeklyThemeQuestService _questService;

        /// <summary>Classified bundle requirements for this run; exposed for the UI + donation layer.</summary>
        public System.Collections.Generic.IReadOnlyList<BundleRequirement> Requirements => _requirements;

        /// <summary>CcItem catalog (rarity/season/theme metadata); exposed so the UI can look up
        /// per-season obtainability when computing the bonus-item preview per card.</summary>
        public System.Collections.Generic.IReadOnlyList<CcItem> Catalog => _catalog;

        public RunController(IMonitor monitor, MetaStore store, GameplayConfig config, WorldResetService reset,
            System.Collections.Generic.IReadOnlyList<CcItem> catalog,
            System.Collections.Generic.IReadOnlyList<BundleRequirement> requirements = null,
            System.Collections.Generic.IReadOnlyDictionary<string, int> ingredientStacks = null,
            System.Collections.Generic.IReadOnlyDictionary<string, int> ingredientQualities = null)
        {
            _monitor = monitor;
            _store = store;
            _config = config;
            _reset = reset;
            _jp = new JpCalculator(config.Jp);
            _catalog = (catalog != null && catalog.Count > 0) ? catalog : CcItemCatalog.Items;
            _requirements = requirements ?? new System.Collections.Generic.List<BundleRequirement>();
            _ingredientStacks = ingredientStacks ?? new System.Collections.Generic.Dictionary<string, int>();
            _ingredientQualities = ingredientQualities ?? new System.Collections.Generic.Dictionary<string, int>();
        }

        /// <summary>Quantity required for a bonus item's bundle slot, used by the hub UI so the
        /// icon shows the actual donation count (e.g. Wood = 99). Falls back to 1 for unknown ids
        /// (SVE additions etc.) so the display never crashes.</summary>
        public int GetStackForIngredient(string itemId)
            => _ingredientStacks.TryGetValue(itemId, out int stack) ? stack : 1;

        /// <summary>Minimum quality required for a bonus item's bundle slot, used by the hub UI
        /// so the icon shows the gold-star (or silver/iridium) badge when the slot needs above-
        /// basic quality. Scale: 0=basic, 1=silver, 2=gold, 4=iridium. Falls back to 0 for ids
        /// that don't appear in any bundle. 2026-05-29: added to fix Quality Crops parsnips
        /// showing as basic-quality in the week-2 farming preview.</summary>
        public int GetQualityForIngredient(string itemId)
            => _ingredientQualities.TryGetValue(itemId, out int q) ? q : 0;

        private RunState Run => _store.Run;

        /// <summary>Called from OnSaveLoaded: ensure the run has a seed.</summary>
        public void OnRunLoaded()
        {
            if (Run.Seed == 0)
                Run.Seed = NewSeed();

            Run.Season = (CoreSeason)(int)Game1.season;
            Run.DayOfMonth = Game1.dayOfMonth;

            _monitor.Log($"Run {Run.RunNumber} ready (seed {Run.Seed}). {DescribeWeek()}", LogLevel.Info);

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
            if (_pendingCutscene != Day28Branch.None)
            {
                // A day-28 outcome is queued. The Day28CutsceneDriver plays the in-bed Junimo
                // scene this morning (UpdateTicked), then calls OnCutsceneEnded() to open the
                // JP shop + reset (Fail) or roll into the next season (Continue). Suppress the
                // normal season-sync/hub flow until the scene resolves — same shape as the old
                // _pendingReset early-return. Manual tly_reset intentionally stays raw.
                return;
            }

            if (_pendingWinChoice)
            {
                _pendingWinChoice = false;
                // 2026-05-29 spec: on the morning after winning the loop, pop the JP-spend
                // shrine first (the player has a fresh JP bonus from the win), then ask
                // them whether to start a new loop or keep playing this run.
                TryOpenShrineThenContinue(ShowKeepPlayingChoice);
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
                new StardewValley.Response("newLoop",     "Start a new loop"),
                new StardewValley.Response("keepPlaying", "Keep playing this run")
            };
            // RunNumber is the attempt count (incremented only on a full loop reset), so it reads
            // as "which loop you won on" — the roguelite payoff stat. Grammar-cased for loop 1.
            string loopLine = Run.RunNumber <= 1
                ? "You restored it on your very first loop!"
                : $"It took {Run.RunNumber} loops, but you restored it.";
            string prompt =
                "The Junimos sing! The Community Center is restored.\n" +
                loopLine + "\n" +
                "Do you want to begin a new loop now, or keep playing this run?";

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
        private void ContinueAfterResetSpend()
        {
            _reset.PerformReset();
            _reset.ProfessionPicker.DrainOnDayStart();
            Run.BeginNewRun(NewSeed());
            ActiveEffectsProvider.Clear();
            // Persist the post-reset meta (JP spent at the shrine, new OwnedUpgrades, the bumped
            // run/reset counters) IMMEDIATELY. A deferred SaveLoaded fires after the in-place reset
            // and calls MetaStore.Load(), which would otherwise overwrite our in-memory state with
            // the stale on-disk meta from before the shrine was opened — refunding the JP the player
            // just spent and dropping their purchases. ApplyKeepPlaying + ModEntry.FullResetAndPresentOffer
            // guard the same way; this path was missing it (2026-06-01 playtest: "it refunded all my JP").
            _store.Save();
            ForceFullSave();
            _monitor.Log($"Loop reset complete. Run {Run.RunNumber} begins (seed {Run.Seed}).", LogLevel.Info);
            DoDayStartSeasonAndHub();
            // Restore the HUD hidden for the FAIL cutscene's shop→reset window (OnCutsceneEnded).
            // Safe/no-op on the non-cutscene paths that also call this (post-win, fallback).
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
                    PopulateBonusItemsForCurrentSelection();
                    var (bonus, liability) = ThemeModifiers.For(Run.CurrentSelection.Value);
                    ActiveEffectsProvider.Set(bonus, liability);
                    _monitor.Log(
                        $"Day-28 pre-pick applied: {Run.CurrentSelection} is the week-1 selection of {season}.",
                        LogLevel.Info);

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
            bool vaultGateSatisfied = VaultRules.IsVaultGateSatisfied(Run.Season, Run, _store.State);
            RunAction action = _runManager.EvaluateDayEnd(Run, _requirements, vaultGateSatisfied);
            switch (action)
            {
                case RunAction.Continue:
                    break;

                case RunAction.AdvanceMonth:
                    _monitor.Log($"Month cleared ({Run.Season}). Advancing.", LogLevel.Info);
                    // Queue the "great job, next season" Junimo cutscene for the morning. The
                    // game still advances the date; OnCutsceneEnded → DoDayStartSeasonAndHub
                    // clears the month's selections and opens the planning hub after the scene.
                    _pendingCutscene = Day28Branch.Continue;
                    break;

                case RunAction.FailReset:
                    AwardInterimJp("run failed");
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
                        AwardInterimJp("run WON — loop broken");
                        _pendingWinChoice = true;
                    }
                    break;
            }
            // Hub trigger now lives in OnDayStarted (above) — see note there. Sunday-night
            // DayEnding fires while the player can't open menus.
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
            PopulateBonusItemsForCurrentSelection();
            var (bonus, liability) = ThemeModifiers.For(theme);
            ActiveEffectsProvider.Set(bonus, liability);
            _monitor.Log(
                $"Selected {theme} (bonus {bonus}, liability {liability}). " +
                $"Bonus items this week: [{string.Join(", ", Run.CurrentWeekBonusItems)}].",
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

        /// <summary>Sample the per-week bonus list for the current selection and store it on RunState.</summary>
        private void PopulateBonusItemsForCurrentSelection()
        {
            Run.CurrentWeekBonusItems.Clear();
            if (!Run.CurrentSelection.HasValue) return;

            int maxCount = BonusListSizeForCurrentSeason();
            var sample = BonusItemSampler.SampleForTheme(
                Run.Seed, Run.WeekOfYear,
                Run.CurrentSelection.Value, Run.Season,
                _requirements,
                IsObtainableInCurrentSeason,
                RarityForItem,
                maxCount);

            Run.CurrentWeekBonusItems.AddRange(sample);
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

        /// <summary>How big the per-card bonus-item preview list should be for the season.
        /// Lives in <see cref="BonusItemSampler.DefaultMaxCountBySeason"/>.</summary>
        public int BonusListSizeForCurrentSeason()
            => BonusItemSampler.DefaultMaxCountBySeason[(int)Run.Season];

        /// <summary>
        /// Number of weather preview days to reveal (the next N days, starting tomorrow).
        /// Equals the highest Weather Sage tier owned (weather_sage_1 through weather_sage_6).
        /// Returns 0 if none owned.
        /// </summary>
        public int WeatherSageTier()
            => _store.State.HighestKeptTier("weather_sage_", 6);

        /// <summary>
        /// Number of Traveling Cart item slots to preview on the planning hub.
        /// Equals 2 * highest Cart Whisperer tier owned (cart_whisper_1 through cart_whisper_3).
        /// Returns 0 if none owned.
        /// </summary>
        public int CartPreviewSlots()
            => CartStockPreview.SlotsToReveal(_store.State.HighestKeptTier("cart_whisper_", 3));

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
        /// auto-completed when all bonus items donated.</summary>
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

            string seasonTag = seasonOverride.HasValue ? $" (for {seasonOverride.Value})" : "";
            _monitor.Log(
                $"Week {week}{seasonTag} selection offer: {string.Join(" OR ", offer)} (opening planning hub).",
                LogLevel.Info);

            Run.OfferPresentedWeek = week;
            _launcher?.OpenWeeklyHub(seasonOverride);
        }

        private void AwardInterimJp(string reason)
        {
            var lines = Run.DonatedItemIds
                .GroupBy(CcItemCatalog.RarityOf)
                .Select(g => new DonationLine(g.Key, g.Count()));
            long awarded = JpBoostHelper.Apply(
                _store.State,
                _jp.ForDonationBatch(lines, Run.WeekOfYear, bundlesCompleted: 0, roomsCompleted: 0));
            _store.State.JunimoPoints += awarded;
            _monitor.Log(
                $"Interim JP for {reason}: +{awarded} (now {_store.State.JunimoPoints}). Persists on this day's save.",
                LogLevel.Info);
        }

        private string DescribeWeek() => $"{Run.Season} day {Run.DayOfMonth} (week {Run.WeekOfYear}).";

        private static int NewSeed() => Guid.NewGuid().GetHashCode();
    }
}
