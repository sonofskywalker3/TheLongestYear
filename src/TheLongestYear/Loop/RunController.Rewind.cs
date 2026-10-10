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
    internal sealed partial class RunController
    {
        /// <summary>The shrine opened by <see cref="TryOpenShrineThenContinue"/> whose
        /// <c>exitFunction</c> hasn't fired yet, with the continuation it owes.</summary>
        private (StardewValley.Menus.IClickableMenu menu, System.Action onContinue)? _menuWatch;

        /// <summary>Set by ShowHoldChoice's NotEnoughJp branch; drained here (not called inline)
        /// so the re-ask's own answer callback survives GameLocation.answerDialogue nulling
        /// afterQuestion after the first callback returns.</summary>
        private bool _holdReaskPending;

        /// <summary>Polled every tick by the day-28 driver. If the shrine was torn down without its
        /// exitFunction (a menu swapped in over it — vanilla's end-of-night SaveGameMenu is the known
        /// case) run the owed continuation once the surface is clear, so a FAIL night always ends in
        /// a reset. JP stays banked for the next shrine visit.</summary>
        public void TickShrineWatchdog()
        {
            TickRestartDeclined();
            TickVoluntaryRestart();
            if (_holdReaskPending && Game1.activeClickableMenu == null)
            {
                _holdReaskPending = false;
                ShowHoldChoice();
                return;
            }
            if (_continuePending != null && Game1.activeClickableMenu == null)
            {
                System.Action owed = _continuePending;
                _continuePending = null;
                owed();
                return;
            }
            if (_shrineOpenPending != null && Game1.activeClickableMenu == null)
            {
                System.Action owed = _shrineOpenPending;
                _shrineOpenPending = null;
                TryOpenShrineThenContinue(owed);
                return;
            }
            if (_menuWatch is not { } watch) return;
            if (ReferenceEquals(Game1.activeClickableMenu, watch.menu)) return;   // still up
            if (Game1.activeClickableMenu != null) return;                        // wait for the intruder to close
            _menuWatch = null;
            _monitor.Log(
                "A day-28 menu (hold choice or Junimo Shrine) was replaced before it closed normally; running its continuation now " +
                "(banked JP is untouched; spend it next time the shrine opens).", LogLevel.Warn);
            watch.onContinue();
        }

        /// <summary>Called by the <see cref="TheLongestYear.Integration.Day28CutsceneDriver"/> when the
        /// day-28 bedtime cutscene has finished, or directly (no scene) for a voluntary Restart.
        /// Clears the pending branch and runs its continuation: FAIL → JP shop, then on close PerformReset + forced full save
        /// (ContinueAfterResetSpend); CONTINUE → roll straight into the next season's day-start
        /// flow (no shop, no reset).</summary>
        public void OnCutsceneEnded()
        {
            Day28Branch branch = _pendingCutscene;
            _pendingCutscene = Day28Branch.None;
            _dayStartedWhileBranchPending = false;

            switch (branch)
            {
                case Day28Branch.Fail:
                    StartRewindChain();
                    break;
                case Day28Branch.Restart:
                    // Voluntary restart: the Fail chain without the scene (the driver skipped it).
                    // After "Keep playing" the won-run flag silences later wins; a restart starts a
                    // loop that can be won again. FinalizeReset's _store.Save() persists the clear.
                    if (VoluntaryRestart.ClearWonRun(_store.State))
                        _monitor.Log("Voluntary restart after Keep playing: the won-run flag is cleared, so the next loop can be won again.", LogLevel.Info);
                    if (Run.RestartMenusDone)
                    {
                        // The hold, upgrade menu and books ran before the night: reset straight away.
                        Run.RestartMenusDone = false;
                        Game1.displayHUD = false;
                        FinalizeReset("voluntary restart");
                    }
                    else
                    {
                        // A Restart queued by a build before 0.18.98 (loaded mid-night): menus now.
                        StartRewindChain();
                    }
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

        /// <summary>The rewind chain shared by a Fail night and a voluntary restart: hold question
        /// (whenever BundleHold.IsOfferable, which is every bundle source today), upgrade menu,
        /// recipe banking, reset.</summary>
        private void StartRewindChain()
        {
            // Hide the day/time HUD across the choice -> shop -> reset so the stale
            // (pre-rewind) calendar date isn't shown while the player decides and shops.
            // FinalizeReset restores it once the world is back on Spring 1.
            Game1.displayHUD = false;
            // Vanilla mode's reset regenerates the board via loadForNewGame and never
            // consults BundleSeedLoop, so holding would be a no-op that still charges JP.
            // Read the save's chosen source, not its BundleSource stamp: PerformReset
            // re-stamps from the choice at reset time, so the choice is what this reset
            // will actually run under.
            MetaState state = _store.State;
            if (!BundleHold.IsOfferable(BundleSourceNames.ForSave(state.ChosenBundleSource, state.BundleSource, state.VanillaBundleType)))
            {
                _monitor.Log("Hold choice skipped: BundleSource=Vanilla", LogLevel.Info);
                TryOpenShrineThenContinue(ContinueAfterResetSpend);
            }
            else
            {
                ShowHoldChoice();
            }
        }

        /// <summary>Continuation called after the JP-spend popup closes on a loop reset. Offers the
        /// recipe books, then performs the actual world reset and resumes the normal day-start
        /// sync + hub trigger.</summary>
        private void ContinueAfterResetSpend()
            => OfferRecipeBanking(FinishRewindChain);

        /// <summary>Nexus post ada113, 2026-09-07: the Cookbook and Craftbook start at 0 slots, the
        /// first tier is bought at the shrine that opens right here, and the reset that follows wipes
        /// every learned recipe. A book bought at the shrine therefore had nothing left to bank the
        /// first time it was opened. So between the shrine and the reset, open each book that has a
        /// free slot and something worth putting in it (see <see cref="RecipeBanking"/>), Cookbook
        /// then Craftbook, and continue once both are closed. Same watchdog as the shrine, so a menu
        /// torn down underneath us still ends in a reset. The Herd Book follows the Craftbook
        /// (RunController.HerdBook.cs).</summary>
        private void OfferRecipeBanking(System.Action onContinue)
            => OfferBook(isCooking: true, () => OfferBook(isCooking: false, () => OfferHerdBook(onContinue)));

        private void OfferBook(bool isCooking, System.Action onContinue)
        {
            Farmer player = Game1.player;
            MetaState meta = _store.State;
            if (player == null || meta == null || _launcher == null) { onContinue(); return; }

            string bookName = isCooking ? "Cookbook" : "Craftbook";
            int slots = isCooking
                ? UpgradeCatalog.CookbookSlotCount(meta.HighestKeptTier("cookbook_", maxTier: UpgradeCatalog.BookMaxTier))
                : UpgradeCatalog.CraftbookSlotCount(meta.HighestKeptTier("craftbook_", maxTier: UpgradeCatalog.BookMaxTier));
            List<string> banked = isCooking ? meta.CookbookRecipes : meta.CraftbookRecipes;
            int bankable = isCooking
                ? RecipeBanking.Bankable(player.cookingRecipes.Keys, banked, RecipeDefaults.IsDefaultCooking).Count
                : RecipeBanking.Bankable(player.craftingRecipes.Keys, banked, RecipeDefaults.IsDefaultCrafting).Count;
            if (!RecipeBanking.ShouldOfferAtReset(slots, banked.Count, bankable))
            {
                _monitor.Log($"{bookName} not offered before the reset: slots={slots}, banked={banked.Count}, bankable={bankable}.", LogLevel.Trace);
                onContinue();
                return;
            }

            string subtitle = Strings.Get("menu.books.bank-before-reset");
            WatchRewindMenu(
                bookName,
                $"slots={slots}, banked={banked.Count}, bankable={bankable}",
                () => { if (isCooking) _launcher.OpenCookbook(subtitle); else _launcher.OpenCraftbook(subtitle); },
                menu => menu is TheLongestYear.UI.CookbookMenu or TheLongestYear.UI.CraftbookMenu,
                onContinue);
        }

        /// <summary>Shared by the rewind-night books (Cookbook, Craftbook, Herd Book): open the menu,
        /// and if it is up, watch it (see <see cref="TickShrineWatchdog"/>) and continue from its
        /// exitFunction; if something else is in the way, log it and continue straight away, so the
        /// reset always happens.</summary>
        private void WatchRewindMenu(
            string name,
            string detail,
            System.Action open,
            System.Func<StardewValley.Menus.IClickableMenu, bool> isExpected,
            System.Action onContinue)
        {
            open();
            if (Game1.activeClickableMenu is { } menu && isExpected(menu))
            {
                _monitor.Log($"{name} offered before the reset: {detail}.", LogLevel.Info);
                _menuWatch = (menu, onContinue);
                menu.exitFunction = () =>
                {
                    _menuWatch = null;
                    onContinue();
                };
                return;
            }
            string blockingMenu = Game1.activeClickableMenu?.GetType().Name ?? "none";
            _monitor.Log(
                $"{name} could not open before the reset; continuing without it. " +
                $"activeClickableMenu={blockingMenu}, eventUp={Game1.eventUp}.",
                LogLevel.Warn);
            onContinue();
        }

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
            // Re-inject the manifest PerformReset just generated for the new loop -- before
            // DoDayStartSeasonAndHub below, which samples goal slots from _requirements. Null only
            // if PerformReset somehow returned without generating (shouldn't happen post-wiring;
            // guarded rather than trusted so a future refactor can't silently null out the run).
            if (_reset.LastGeneratedRequirements != null)
                ReplaceRequirements(_reset.LastGeneratedRequirements);
            else if (BundleSourceNames.IsVanilla(_store.State.BundleSource))
                _monitor.Log(
                    $"FinalizeReset ({reason}): Vanilla mode — the game's own board is classified on the post-reset reload.",
                    LogLevel.Info);
            else
                _monitor.Log(
                    $"FinalizeReset ({reason}): WorldResetService.LastGeneratedRequirements was null — " +
                    "keeping the previous requirement manifest.",
                    LogLevel.Warn);
            _reset.ProfessionPicker.DrainOnDayStart();
            // Festival memories: this loop's festivals become the next loop's half-memories. Every
            // rewind path comes through here; BeginNewRun clears the log right after.
            int festivals = Run.FestivalLog?.Count ?? 0;
            FestivalMemoryStore.Commit(_store.State, Run);
            _monitor.Log($"FinalizeReset ({reason}): festival memories committed ({festivals} festival(s) this loop, " +
                $"{_store.State.DancePartners.Count} dance partner(s), {_store.State.WinterStarRecipients.Count} secret friend(s) kept).",
                LogLevel.Info);
            Run.BeginNewRun(NewSeed());
            ActiveEffectsProvider.Clear();
            _wildcard?.OnReset();
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
    }
}
