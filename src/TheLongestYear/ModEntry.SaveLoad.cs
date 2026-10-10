using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarmonyLib;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Menus;
using StardewValley.Network;
using StardewValley.Quests;
using TheLongestYear.Core;
using TheLongestYear.Core.Availability;
using TheLongestYear.Donations;
using TheLongestYear.Integration;
using TheLongestYear.Loop;
using TheLongestYear.UI;

namespace TheLongestYear
{
    public sealed partial class ModEntry
    {
        /// <summary>True when Tech's Cross-Mod Bundles is installed and loaded.</summary>
        private bool IsTechCrossModBundlesLoaded()
            => this.Helper.ModRegistry.IsLoaded(TheLongestYear.Core.TechBundlesReroll.ModId);

        /// <summary>SaveLoaded at normal priority: runs <see cref="OnSaveLoaded"/> unless Tech's
        /// Cross-Mod Bundles is loaded, in which case <see cref="OnSaveLoadedLate"/> does.</summary>
        private void OnSaveLoadedNormal(object sender, SaveLoadedEventArgs e)
        {
            if (!TheLongestYear.Core.TechBoardOfRecord.RunLoadLate(IsTechCrossModBundlesLoaded()))
                OnSaveLoaded(sender, e);
        }

        /// <summary>SaveLoaded at low priority, only with Tech's Cross-Mod Bundles loaded: its own
        /// normal-priority SaveLoaded handler writes its saved board over the live one, so TLY's load
        /// must come after it, whatever the load order, to put this loop's board back (spec 2026-10-08
        /// addendum 3).</summary>
        [EventPriority(EventPriority.Low)]
        private void OnSaveLoadedLate(object sender, SaveLoadedEventArgs e)
        {
            if (TheLongestYear.Core.TechBoardOfRecord.RunLoadLate(IsTechCrossModBundlesLoaded()))
                OnSaveLoaded(sender, e);
        }

        /// <summary>Load this playthrough's banked progress when a save opens. The phases run in a
        /// fixed order: activation first, the board restored before anything reads it, the data
        /// models before the board repair, the repair before the catalog, the catalog before the
        /// run controller.</summary>
        private void OnSaveLoaded(object sender, SaveLoadedEventArgs e)
        {
            // The save carries its own options, so the unfocused-pause setting is re-applied here.
            this.KeepRunningUnfocused("save load");
            // Bundle-relevance set is per-save (bundle data can differ) — rebuild on the next use.
            TheLongestYear.Loop.BundleRelevanceIndex.Invalidate();

            // Cleared until _meta.Load() runs below, so the early-return paths leave OnSaving inert
            // (it must not persist empty defaults over the player's banked progression).
            _metaLoaded = false;

            if (!_config.Enabled)
            {
                DeactivateTly();
                this.Monitor.Log("TLY disabled in config — skipping all save-load setup.", LogLevel.Info);
                return;
            }

            // Every farm type is allowed (0.17.3). Kept buildings return to the player's own
            // spots and the stash chest places relative to the farmhouse door, so nothing here
            // depends on the Standard layout any more. Log the type for bug reports.
            this.Monitor.Log($"Farm type: {Game1.whichFarm} ({Game1.GetFarmTypeID()}).", LogLevel.Info);

            _meta.Load();

            if (!DecideActivation(out bool wasNewGame))
                return;
            ApplyNewSaveStamps(wasNewGame);
            RunActivation.Activate();
            _metaLoaded = true;
            RestoreBoardFromOtherMods(wasNewGame);
            ApplyPerSaveFlags();

            var ctx = new LoadContext();
            BuildResetServices(ctx);
            BuildDataModels(ctx);
            RepairBoard(ctx);
            BuildCatalogAndRequirements(ctx);
            WireRunController(ctx);
            PlaceAndWireFarmServices();
            this.Monitor.Log(
                $"Run {_meta.Run.RunNumber} loaded ({_meta.Run.Season} {_meta.Run.DayOfMonth}). JP banked: {_meta.State.JunimoPoints}.",
                LogLevel.Info);
        }

        /// <summary>The values one <see cref="OnSaveLoaded"/> call hands from phase to phase.</summary>
        private sealed class LoadContext
        {
            public IReadOnlyDictionary<string, TheLongestYear.Core.Theme> ThemeOverrides;
            public IReadOnlyDictionary<string, TheLongestYear.Core.Season> ItemSeasonPins;
            public IReadOnlyDictionary<string, int[]> BundleQuotas;
            /// <summary>Kept for its LastReachability: the board repair re-checks against it.</summary>
            public TheLongestYear.Loop.GameDataPools EnginePoolReader;
            public TheLongestYear.Core.ItemPools EnginePools;
        }

        /// <summary>Load phase: TLY runs only on a save started as a Longest Year run (or adopted as one). False
        /// leaves the mod dormant on this save.</summary>
        private bool DecideActivation(out bool wasNewGame)
        {
            // Per-save opt-in. TLY only activates on a save that was STARTED as a Longest Year run:
            //   - a brand-new game created this session (_isNewGame, set by OnSaveCreating), or
            //   - a save that already carries the run marker, or
            //   - a pre-existing TLY save with banked data from before the marker existed (back-fill).
            // Any other save — a normal vanilla playthrough loaded with the mod installed — leaves
            // TLY fully dormant: no Harmony effects, no HUD, no reset loop. _metaLoaded stays false
            // so OnSaving never persists empty defaults over the player's real save data.
            wasNewGame = _isNewGame;
            bool isLongestYearSave = _isNewGame || _meta.State.IsLongestYearRun || _meta.LoadedExistingData;
            _isNewGame = false; // consume — only the load right after SaveCreating counts as new
            // A farm quit before its first night, saved by the game at character creation before
            // the marker existed: adopt it (Nexus, 2026-09-14). Same policy as a new game.
            if (!isLongestYearSave && _config.Enabled && Game1.stats != null
                && RunAdoption.IsUnsavedFirstMorning(
                    _meta.LoadedExistingData, Game1.stats.DaysPlayed,
                    (TheLongestYear.Core.Season)(int)Game1.season, Game1.dayOfMonth, Game1.year))
            {
                isLongestYearSave = true;
                this.Monitor.Log(
                    "This save is a brand-new farm that was quit before its first night, so it never got " +
                    "the Longest Year marker. Adopting it as a Longest Year run.",
                    LogLevel.Warn);
            }
            if (!isLongestYearSave)
            {
                DeactivateTly();
                this.Monitor.Log(
                    "This save wasn't started as a Longest Year run — the mod will stay dormant and " +
                    "leave it untouched. Start a new game to play The Longest Year.",
                    LogLevel.Info);
                return false;
            }
            return true;
        }

        /// <summary>Load phase: stamp the run marker and, on a new game, the mod-items default, the skip-intro
        /// choice and the Advanced Options bundle source.</summary>
        private void ApplyNewSaveStamps(bool wasNewGame)
        {
            // Stamp the marker so new games (and back-filled legacy TLY saves) take the clean flag
            // path next load; it persists with the game's own save via OnSaving.
            _meta.State.IsLongestYearRun = true;
            // "Allow mod items in custom bundles" (spec 2026-10-08 addendum 1). A new game (or an
            // adopted farm with no TLY data yet) starts on the title-screen default, which is off.
            // Any other save without a value predates the option and keeps mod items on, as its
            // boards were built. Written here, before ResolveRequirements builds a fresh-run board.
            if (_meta.State.AllowModItemsInCustomBundles == null)
            {
                bool isNewSave = wasNewGame || !_meta.LoadedExistingData;
                _meta.State.AllowModItemsInCustomBundles = TheLongestYear.Core.CustomBoardModItems.Initial(
                    isNewSave, _config.AllowModItemsInCustomBundles);
                this.Monitor.Log(
                    $"Allow mod items in custom bundles: {(_meta.State.AllowModItemsInCustomBundles.Value ? "on" : "off")} " +
                    $"for this save ({(isNewSave ? "new game, from the default" : "existing save, keeps mod items")}).",
                    LogLevel.Info);
            }
            if (wasNewGame)
            {
                // "Skip intro" ticked on character creation: plant the cc-seen flag now, so the
                // intro driver goes straight to the theme picker on this first morning and
                // OnSaving promotes it to HasSeenIntro like a watched cutscene would.
                if (SkipIntroChoicePatch.Choice.Consume() && Game1.player != null
                    && !Game1.player.mailReceived.Contains(TheLongestYear.Core.Intro.IntroEventKeys.CcSeenMail))
                {
                    Game1.player.mailReceived.Add(TheLongestYear.Core.Intro.IntroEventKeys.CcSeenMail);
                    this.Monitor.Log("Intro: skipped by the character-creation checkbox; opening the theme picker instead.", LogLevel.Info);
                }

                // Per-save bundle source from the Advanced Options dropdown (BundleOptionPatch):
                // TLY Custom (default) → Engine; Normal/Remixed → Vanilla + the vanilla type so
                // every reset regenerates the same kind of board (Nexus bug 1108030 root cause:
                // Game1.bundleType is never persisted by the game).
                BundleOptionPatch.Choice choice = BundleOptionPatch.ConsumeLastChoice();
                string chosenSource = BundleOptionPatch.SourceFor(choice);
                // Kept on the save, not mirrored into the config: the config is shared by every
                // save, and mirroring it here is how a new TLY Custom game flipped an older Normal
                // save to custom bundles at its next reset (victoriatauanem, Nexus 2026-09-28).
                // Same stamp as the creation-time marker (OnSaveCreating).
                TheLongestYear.Core.NewRunStamp.ApplyBundleChoice(_meta.State, chosenSource);

                this.Monitor.Log(
                    $"New game: bundle source={chosenSource} (Advanced Options choice {choice}, vanilla type {_meta.State.VanillaBundleType}).",
                    LogLevel.Info);
            }
        }

        /// <summary>Load phase: put this loop's board back over the one Tech's Cross-Mod Bundles wrote, sync the
        /// bundle keys, and store loop 1's board when it needs a board of record.</summary>
        private void RestoreBoardFromOtherMods(bool wasNewGame)
        {
            // Tech's Cross-Mod Bundles writes its own saved board over the live one on every load
            // (its SaveLoaded handler, which ran before this one; see OnSaveLoadedLate). Put this
            // loop's board back before anything below repairs, classifies or verifies it (spec
            // 2026-10-08 addendum 3). No-op without Tech's mod or without a stored board.
            TheLongestYear.Core.TechBoardOfRecord.RestoreOnLoad(
                IsTechCrossModBundlesLoaded(), Context.IsMainPlayer, _meta.State.WrittenBoard,
                new TheLongestYear.Loop.LiveBundleBoard(),
                message => this.Monitor.Log(message, LogLevel.Info));
            // A TLY Custom board whose rooms are not the game's default size (the bundle-count
            // dial, spec 2026-10-09): the load put Data/Bundles' default keys back and built the
            // CC's room lookups from them. Drop the bundles this board does not have and refresh
            // the lookups, before anything below repairs, classifies or reads the board.
            if (!BundleSourceNames.IsVanilla(_meta.State.BundleSource))
                TheLongestYear.Loop.BundleKeySync.SyncToStoredBoard(_meta.State.WrittenBoard, this.Monitor);
            // Loop 1 of a new Normal/Remixed game has no reset to store its board, so store it here,
            // on the new-game load, after Tech's handler and before TLY's own load-time edits (the
            // unstackable clamp and later the week discount mirror into it from here on). An
            // existing loop-1 save without one is not adopted: Tech has already rewritten its board
            // by now, so it waits for its next reset (spec 2026-10-08 addendum 3).
            Dictionary<string, string> newGameBoard = TheLongestYear.Core.TechBoardOfRecord.NewGameBoardToStore(
                wasNewGame || !_meta.LoadedExistingData, Context.IsMainPlayer,
                BundleSourceNames.IsVanilla(_meta.State.BundleSource), IsTechCrossModBundlesLoaded(),
                _meta.State.WrittenBoard, Game1.netWorldState?.Value?.BundleData);
            if (newGameBoard != null)
            {
                _meta.State.WrittenBoard = newGameBoard;
                this.Monitor.Log(
                    $"New game: stored loop 1's board ({newGameBoard.Count} bundles) as the board of record, since Tech's Cross-Mod Bundles rewrites the board on every load.",
                    LogLevel.Info);
            }
        }

        /// <summary>Load phase: per-save flags and providers (intro mail, deja-vu caps, upgrade and boost checks,
        /// patch providers, asset refreshes) and the replayable-cutscene scan.</summary>
        private void ApplyPerSaveFlags()
        {
            // Inject the tly_intro_done mail flag now if the player has already seen the intro
            // on a prior loop — that's what suppresses both intro events for years 2+.
            _introInjector?.ApplyMailFlagsForRun();
            // Saves from before 0.19.23 kept the deja-vu caps across rewinds; drop a stamp left
            // over from an earlier loop so the villager lines can come back on this one.
            if (Game1.stats != null
                && TheLongestYear.Core.DejaVuRules.RepairStaleCaps(_meta.Run, (int)Game1.stats.DaysPlayed))
                this.Monitor.Log("Deja-vu: cleared caps left over from an earlier loop.", LogLevel.Info);
            UpgradeChecker.HasUpgrade = id => _meta.State.HasUpgrade(id);
            _animalPowers.RefreshPets("save load");
            BoostChecker.YearTwoSeedsActive = () => TheLongestYear.Core.BoostState.YearTwoSeedsActive(_meta.Run, TodayDayOfYear());
            TheLongestYear.Loop.PastSeasonSpawnsService.BoostedOn = day => TheLongestYear.Core.PastSeasonBoosts.Active(_meta.Run, day);
            _pastSeasonSpawns.Refresh(TodayDayOfYear());
            BoostChecker.SneakPeekActive = () => TheLongestYear.Core.BoostState.SneakPeekActive(_meta.Run, TodayDayOfYear());
            // The fruit/mushroom/fish each flavored bundle slot names, for the live board only.
            // Null map (a pre-0.18.33 board, or Vanilla board mode) means no flavors are applied.
            TheLongestYear.Patches.FlavoredSlotPatch.FlavorsProvider =
                () => (IReadOnlyDictionary<string, string>)_meta.State.WrittenBoardFlavors;
            CartSlotLimitPatch.RunProvider = () => _meta.Run;
            CartDaysPatch.RunProvider = () => _meta.Run;
            CartDaysPatch.Settings = week => _runController?.RandomizerForWeekPeek(week);
            CartSlotLimitPatch.StartingSlotsProvider = () => _meta.State.EffectiveDifficulty(_config).StartingCartSlots;
            // Once-per-day guard for festival main events (Egg Hunt and friends): TLY festivals do
            // not end the day, so the map stays re-entrant and vanilla would offer the hunt again.
            TheLongestYear.Loop.FestivalMainEventOncePatch.RunProvider = () => _meta.Run;
            TheLongestYear.Loop.FestivalMainEventOncePatch.Monitor = this.Monitor;
            TheLongestYear.Loop.CommunityCenterCompletePatch.Monitor = this.Monitor;
            TheLongestYear.Loop.CommunityCenterCompletePatch.ResetLogGuards();
            // Ownership is per save: re-evaluate the Pierre year-2-seeds shop edit for this save.
            this.Helper.GameContent.InvalidateCache(TheLongestYear.Loop.PierreYear2SeedsService.ShopAssetName);
            // Same for the Sneak Peek channel label: the Boost is per save and per season.
            this.RefreshSneakPeekChannelLabel();
            // Generalize the replayable-cutscene set: scan the live save's Data/Events for any
            // unlock-granting cutscene (recipe/mail/quest) so a mod's teach/unlock scene re-fires each
            // loop, merged with the vanilla furnace/cave ids. FarmerReset consults it at reset time.
            TheLongestYear.Loop.ReplayableEventScan.Populate(
                this.Helper.GameContent,
                Game1.locations,
                EventGatingTables.Default.ReplayableEventIds,
                BuildReplayableExclude(),
                _config.AutoDetectReplayableUnlockCutscenes,
                this.Monitor);
        }

        /// <summary>A brand-new game is being created. If TLY is enabled, this save becomes a Longest
        /// Year run — remember it so the OnSaveLoaded that follows activates the mod, and put the run
        /// marker into the save right now. The game writes a new farm's file at character creation and
        /// SMAPI does not raise Saving for that write, so a marker stamped only in OnSaving never reached
        /// a farm quit before its first night (Nexus, 2026-09-14). Loading an existing save never fires
        /// this, which is what keeps TLY dormant on non-TLY saves.</summary>
        private void OnSaveCreating(object sender, SaveCreatingEventArgs e)
        {
            if (!_config.Enabled)
                return;
            _isNewGame = true;
            try
            {
                string chosenSource = BundleOptionPatch.SourceFor(BundleOptionPatch.PeekLastChoice());
                _meta.StampNewRunMarker(TheLongestYear.Core.NewRunStamp.Marker(chosenSource, _config.AllowModItemsInCustomBundles));
                // Skip intro: plant the cc-seen flag in the creation-time save too, so a farm quit
                // before its first night does not replay the intro the player skipped. The choice
                // is consumed (and the flag re-checked) by the new-game load as before.
                if (SkipIntroChoicePatch.Choice.Pending && Game1.player != null
                    && !Game1.player.mailReceived.Contains(TheLongestYear.Core.Intro.IntroEventKeys.CcSeenMail))
                    Game1.player.mailReceived.Add(TheLongestYear.Core.Intro.IntroEventKeys.CcSeenMail);
            }
            catch (System.InvalidOperationException ex)
            {
                // Save data not writable yet on this platform: the load-time adoption rule covers it.
                this.Monitor.Log($"Could not stamp the run marker at save creation ({ex.Message}); a quit before the first night will be adopted on reload instead.", LogLevel.Warn);
            }
        }

        /// <summary>Returning to title means the loaded save is gone — drop the runtime gate so no
        /// stale state leaks into the next save the player loads.</summary>
        private void OnReturnedToTitle(object sender, ReturnedToTitleEventArgs e)
            => DeactivateTly();

        /// <summary>Put TLY fully to sleep: clear the master runtime gate and null every static
        /// provider so no Harmony patch, HUD draw, or tick handler does anything until a TLY save
        /// re-activates it. Called for a non-TLY (or disabled / non-Standard) save load and on
        /// return to title. The per-patch null-guards already short-circuit once the providers are
        /// null, and <see cref="RunActivation.IsActive"/> backstops the rest.</summary>
        private void DeactivateTly()
        {
            RunActivation.Deactivate();
            TheLongestYear.Loop.GroundDrop.ResetDropsOnGround = false;
            // The quarter baseline belongs to one save's season; carrying it to the title screen would
            // let the next save's quarter 2 plan against the previous save's ledger.
            _playSeasonBaseline = null;
            _playSeasonDonatedThisSeason = 0;
            TheLongestYear.Patches.BundleDonationPatches.LiveBoardHasNonObjectSlots = false;
            ActiveEffectsProvider.Clear();
            DayEffects.Clear();
            TheLongestYear.Loop.WildcardDayService.GrowthNight = null;
            TheLongestYear.Loop.WildcardDayService.NightRun = null;
            TheLongestYear.Loop.RockslidePatch.Forget();
            TheLongestYear.Loop.UpgradeChecker.HasUpgrade = null;
            _animalPowers?.RefreshPets("title");
            TheLongestYear.Loop.BoostChecker.YearTwoSeedsActive = null;
            TheLongestYear.Loop.PastSeasonSpawnsService.BoostedOn = null;
            TheLongestYear.Loop.BoostChecker.SneakPeekActive = null;
            TheLongestYear.Patches.FlavoredSlotPatch.FlavorsProvider = null;
            TheLongestYear.Loop.BoostEffectsService.SecondWindTonight = null;
            TheLongestYear.Loop.BoostEffectsService.FastFriendsActive = null;
            TheLongestYear.Loop.BoostEffectsService.HagglerActive = null;
            ActiveEffectsProvider.DetachBoosts();
            TheLongestYear.Loop.CartSlotLimitPatch.RunProvider = null;
            TheLongestYear.Loop.CartDaysPatch.RunProvider = null;
            TheLongestYear.Loop.CartDaysPatch.Settings = null;
            TheLongestYear.Loop.CartSlotLimitPatch.StartingSlotsProvider = null;
            TheLongestYear.Loop.FestivalMainEventOncePatch.RunProvider = null;
            BundleOptionPatch.ResetChoice();
            _boardBuilder = null;
            _boardFingerprint = null;
            this.Helper.GameContent.InvalidateCache(TheLongestYear.Loop.PierreYear2SeedsService.ShopAssetName);
            // BoostChecker is null from here, so the channel edit no longer applies: drop the
            // relabelled string so a non-TLY save never shows a TLY channel name.
            if (_sneakPeekLabelActive)
            {
                _sneakPeekLabelActive = false;
                this.Helper.GameContent.InvalidateCache(TheLongestYear.Loop.SneakPeekChannelService.StringsAssetName);
            }
            DonationService.Active = null;
            ShrineDonationService.Active = null;
            TheLongestYear.Loop.ReplayableEventScan.Clear();
            TheLongestYear.Loop.HerdBookService.ClearPending();
            // The peak-mine-floor tracker is only subscribed/unsubscribed on the proceed path of
            // OnSaveLoaded; the dormant bail returns before that, so detach here too or a tracker
            // left over from a prior TLY save keeps firing on the non-TLY save's warps.
            if (_peakMineFloorTracker != null)
                this.Helper.Events.Player.Warped -= _peakMineFloorTracker.OnWarped;
        }

        /// <summary>Commit meta-state as part of the game's save — never eagerly, to prevent save-scumming.</summary>
        private void OnSaving(object sender, SavingEventArgs e)
        {
            // If this save opened without TLY setup (disabled in config),
            // _meta.Load() never ran and State/Run are empty defaults — persisting them would wipe
            // the player's banked progression. Skip the save entirely in that case.
            if (!_metaLoaded)
                return;

            // Promote per-run tly_intro_cc_seen mail to cross-run MetaState.HasSeenIntro BEFORE
            // we persist, so a save+reset can't lose the flag (mailReceived gets wiped by
            // FarmerReset.loadForNewGame, MetaState doesn't).
            _introInjector?.MarkIntroSeenIfApplicable();
            RecordSeenEvents();
            if (RunActivation.IsActive)
                TheLongestYear.Loop.AnimalSpeciesRecorder.Record(_meta.State, this.Monitor);
            _meta.Save();
            this.Monitor.Log($"Meta-state saved with the game. JP banked: {_meta.State.JunimoPoints}.", LogLevel.Trace);
        }
    }
}
