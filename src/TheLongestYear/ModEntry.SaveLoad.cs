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

        /// <summary>Load this playthrough's banked progress when a save opens.</summary>
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

            // Per-save opt-in. TLY only activates on a save that was STARTED as a Longest Year run:
            //   - a brand-new game created this session (_isNewGame, set by OnSaveCreating), or
            //   - a save that already carries the run marker, or
            //   - a pre-existing TLY save with banked data from before the marker existed (back-fill).
            // Any other save — a normal vanilla playthrough loaded with the mod installed — leaves
            // TLY fully dormant: no Harmony effects, no HUD, no reset loop. _metaLoaded stays false
            // so OnSaving never persists empty defaults over the player's real save data.
            bool wasNewGame = _isNewGame;
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
                return;
            }

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
            RunActivation.Activate();
            _metaLoaded = true;
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
            _ccUnlock = new CommunityCenterUnlock(this.Monitor);
            _ccUnlock.Apply();
            _mountainUnlock = new MountainUnlock(this.Monitor);
            _mountainUnlock.Apply();
            var farmerReset = new FarmerReset(this.Monitor)
            {
                ResendBetterStartGift = () => _config.ResendBetterStartGift,
            };
            var professionPicker = new ProfessionPickerScheduler(this.Monitor);
            _stashService = new JunimoStashService(this.Monitor, _meta.State, _config);
            JunimoStashService.SetTextureLoader(
                () => this.Helper.ModContent.Load<Microsoft.Xna.Framework.Graphics.Texture2D>("assets/junimo_stash.png"));
            _meta.AttachStashService(_stashService);
            JunimoStashCapPatch.Connect(this.Monitor, _meta.State);
            JunimoStashCapacityPatch.Connect(_meta.State);
            XpMultiplierPatch.Connect(_meta.State);
            TheLongestYear.Loop.DejaVuDialoguePatch.Enabled = _config.EnableDejaVuDialogue;
            TheLongestYear.Loop.AnimalDoubleProductPatch.Connect(() => _meta.Run);
            TheLongestYear.Loop.DejaVuDialoguePatch.Connect(_meta.State, () => _meta.Run, _config, this.Monitor,
                () => this.Helper.Translation.GetTranslations().Select(t => t.Key).ToList());
            TheLongestYear.Loop.FestivalMemoryContext.Connect(_meta.State, () => _meta.Run, _config, this.Monitor,
                () => this.Helper.Translation.GetTranslations().Select(t => t.Key).ToList());
            PatchLog.Connect(this.Monitor);
            // Computed once and shared by both the reset service (owned-bundle engine seed-time
            // manifest generation, see WorldResetService.PerformReset) and the catalog builder
            // below -- the same merged config the legacy classify path has always used.
            var themeOverrides = ParseThemeOverrides();
            var itemSeasonPins = ParseItemSeasonPins();
            var bundleQuotas = ParseBundleQuotas();
            _reset = new WorldResetService(
                this.Monitor, _meta.State, _meta.Run, _config, _ccUnlock,
                this.Helper.DirectoryPath, farmerReset, professionPicker,
                _stashService, _mountainUnlock, _bookFurniture, _planningShrine,
                itemSeasonPins, bundleQuotas, this.Helper.GameContent);
            // The rewind must let a legendary be caught again: the game blocks a repeat catch
            // through SpawnFishData.CatchLimit against player.fishCaught, and FarmerReset never
            // touched that record. Read the catch-limited ids once here (same shape as
            // GameDataPools's own Data/Locations read) and forward them through the reset service.
            _reset.CatchLimitedFishIds = ReadCatchLimitedFishIds();
            // Normal and Remixed roll a fresh Tech's Cross-Mod Bundles board each loop when that mod is loaded.
            _reset.TechBundles = new TheLongestYear.Loop.TechCrossModBundlesTarget(this.Helper.ModRegistry);

            // Engine pools double as season ground truth: fish/crab-pot spawn seasons feed
            // the SeasonResolver (so weekly themes can't ask for out-of-season fish, Nexus
            // 1122423) and DerivedSeasonPins feed the obtainability clamp below.
            // Held in a local (rather than the old new-and-Build one-liner) so its
            // LastReachability survives the call: the board repair below re-checks the LIVE board
            // against exactly the verdicts these pools were built from.
            var enginePoolReader = new TheLongestYear.Loop.GameDataPools(this.Monitor);
            TheLongestYear.Core.ItemPools enginePools =
                enginePoolReader.Build(_config.PoolTuning,
                    TheLongestYear.Core.YearTwoCrops.ExcludedFor(
                        _meta.State.HasUpgrade, _meta.State.BoardDifficulty(_config).Steps.ItemRarity));
            _seasonResolver = new SeasonResolver(
                TheLongestYear.Core.SpawnSeasonMap.FromPools(enginePools));
            // Derived item model: earliest-possible season and effort per item, from the same
            // live pools the engine generates from. Curated pins ride along as season overrides.
            // Built here because it needs enginePools, and consumed by everything below that
            // classifies bundles. _reset is constructed above (it does not need the pools), so it
            // takes the model through its settable AvailabilityModel property instead. Built with
            // the run's live difficulty step (spec 2026-08-28-obtainable-board, section 1): Easy
            // and Normal answer with the pacing week, Hard moves gates to the hard week, Extreme
            // moves gates and cards both. WorldResetService.RebuildAvailabilityModel rebuilds it
            // at the same point a reset re-resolves the difficulty for the new run.
            _effortData = new TheLongestYear.Loop.GameEffortData(this.Monitor)
                .Build(_config.PoolTuning.ExcludedLocationMarkers);
            _enginePools = enginePools;
            _engineReachability = enginePoolReader.LastReachability;
            _itemSeasonPins = itemSeasonPins;
            _availability = BuildAvailabilityModelFor(_meta.State.BoardDifficulty(_config).Steps.ItemRarity);
            _reset.AvailabilityModel = _availability;
            _reset.RebuildAvailabilityModel = BuildAvailabilityModelFor;
            this.Monitor.Log(
                $"Item availability model built from live pools: "
                + $"{_availability.DerivedCount} id(s) derived, "
                + $"{_availability.DerivedEffortCount} effort-only id(s) derived, "
                + $"{_availability.RejectedSeasonOverrides.Count} curated season pin(s) rejected for "
                + "demanding an item earlier than it can exist.",
                LogLevel.Trace);
            if (_availability.RejectedSeasonOverrides.Count > 0)
                this.Monitor.Log(
                    "Rejected season pins (derived floor kept instead): "
                    + string.Join(", ", _availability.RejectedSeasonOverrides),
                    LogLevel.Warn);
            // An ask above one for a hat, weapon or trophy ring can never be deposited (Nexus bug
            // report 2026-09-14). Fixed first, so the requirement manifest below reads the repaired
            // board and matches what the fixed generator re-derives.
            bool oncePerLoopAsksOne = _meta.State.Difficulty?.OncePerLoopAsksOne ?? _config.OncePerLoopAsksOne;
            TheLongestYear.Loop.BoardRepairService.ClampUnstackableAsks(this.Monitor, _meta.State, oncePerLoopAsksOne);
            // Repair a board built before the reachability rule existed (spec
            // 2026-09-10-source-reachability, task 9). Runs HERE, above the catalog and the
            // fingerprint, so everything downstream reads the repaired board rather than the one
            // with the impossible ask still in it. Host only, donated slots untouched, and a
            // no-op on a clean board.
            // A TLY Custom board takes its replacements from vanilla-only pools, like the engine
            // that wrote it (spec 2026-10-08-custom-board-vanilla-only), unless that board was built
            // with mod items allowed (its stamp, never the live choice). A Normal or Remixed board
            // keeps the shared pools, other mods' items included, as before.
            TheLongestYear.Core.ItemPools repairPools =
                BundleSourceNames.IsVanilla(_meta.State.BundleSource) || _meta.State.ModItemsOnBoard()
                ? enginePools
                : new TheLongestYear.Loop.GameDataPools(this.Monitor).Build(_config.PoolTuning,
                    TheLongestYear.Core.YearTwoCrops.ExcludedFor(
                        _meta.State.HasUpgrade, _meta.State.BoardDifficulty(_config).Steps.ItemRarity),
                    TheLongestYear.Loop.BundleEngine.VanillaOnlyIds);
            int repaired = new TheLongestYear.Loop.BoardRepairService(
                this.Monitor, enginePoolReader.LastReachability, repairPools,
                _config.PoolTuning, _availability,
                // The board's own seed basis, not the run seed (a new game assigns that only after
                // this runs), and the stored board of record takes every swap, so a reload restores
                // the repaired board and the repair finds nothing left to do (0.19.9).
                TheLongestYear.Core.BoardRepairStability.Seed(
                    unchecked((ulong)Game1.player.UniqueMultiplayerID), _meta.State.EffectiveBundleSeedLoop),
                oncePerLoopAsksOne, _meta.State.WrittenBoard).RepairIfNeeded();
            if (repaired > 0)
                this.Monitor.Log(
                    $"Board repair: {repaired} unreachable ask(s) replaced. Your donated items were left alone.",
                    LogLevel.Info);

            _boardBuilder = new BundleCatalogBuilder(
                _config.RarityThresholds, _seasonResolver, this.Monitor,
                themeOverrides, itemSeasonPins, bundleQuotas, _availability);
            // Obtainability clamp for the read-and-classify path: curated pins + the engine's
            // derived (earliest-obtainable) pins, so a Remixed/modded board can't demand an
            // unobtainable minimum. Due-date (PerItem) pins stay the curated set.
            var obtainabilityPins = new Dictionary<string, TheLongestYear.Core.Season>(
                enginePools.DerivedSeasonPins,
                StringComparer.Ordinal);
            foreach (KeyValuePair<string, TheLongestYear.Core.Season> pin in itemSeasonPins)
                obtainabilityPins[pin.Key] = pin.Value;
            _boardBuilder.ObtainabilityPins = obtainabilityPins;
            var builder = _boardBuilder;
            _catalog = builder.Build();
            _requirements = ResolveRequirements(builder, itemSeasonPins, bundleQuotas);
            _boardFingerprint = BoardInspection.Fingerprint(Game1.netWorldState.Value.BundleData);
            // The weapon/hat donation patches must stay live for a board that already carries
            // (W)/(H) slots, whatever EnableNonObjectDonations says now (it governs the NEXT
            // board). Read the live data AFTER ResolveRequirements so a fresh-run write counts.
            TheLongestYear.Patches.BundleDonationPatches.LiveBoardHasNonObjectSlots =
                BoardInspection.HasNonObjectIngredients(Game1.netWorldState.Value.BundleData);
            if (TheLongestYear.Patches.BundleDonationPatches.LiveBoardHasNonObjectSlots && !_config.EnableNonObjectDonations)
                this.Monitor.Log(
                    "EnableNonObjectDonations is off but the live board still has weapon/hat slots — " +
                    "keeping the donation patches on for this loop; rings-only from the next reset.",
                    LogLevel.Info);
            DonationService.Active = new DonationService(this.Monitor, _meta, _config);

            _questService = new WeeklyThemeQuestService(
                this.Monitor, _meta, _config,
                slotStateForBundle: RunController.SlotStateForBundle);
            // Wire the post-donation callback so each CC deposit refreshes the quest's progress
            // text (and auto-completes when every goal slot this week is complete).
            DonationService.Active.AfterDonation = _questService.OnItemDonated;
            ShrineDonationService.Active = new ShrineDonationService(this.Monitor, _meta, _config)
            {
                AfterDonation = _questService.OnItemDonated,
            };

            _runController = new RunController(this.Monitor, _meta, _config, _reset, _catalog, _requirements);
            _runController.GoalCaps = new[]
            {
                new GoalGroupCap(enginePools.FruitTreeFruitIds, 1),
                new GoalGroupCap(enginePools.TrapFishIds, 1),
                new GoalGroupCap(GoalGroupCap.JellyIds, 1),
            };
            _runController.Availability = _availability;
            // Random shrine donations: the theme item pools and seasons for off-board goals.
            _runController.ShrineThemeIds = theme => _enginePools == null || _effortData == null
                ? Array.Empty<string>()
                : ThemeEffortPools.IdsFor(theme, _enginePools, _effortData.Objects);
            _runController.ShrineExcludedIds = () => _enginePools?.ExcludedIds;
            _runController.SeasonsOf = id => _seasonResolver?.SeasonsFor(id);
            // The theme week discount rewrites stacks on the board; that is our own write, not
            // another mod's, so the vanilla-mode fingerprint follows it.
            _runController.AfterBoardWrite = () =>
                _boardFingerprint = BoardInspection.Fingerprint(Game1.netWorldState.Value.BundleData);
            _runController.ItemKindOf = id =>
            {
                string bare = BundleParsing.StripQualifier(id);
                return Game1.objectData != null && Game1.objectData.TryGetValue(bare, out var objectData)
                    ? ItemKindClassifier.From(objectData.Category, objectData.Type)
                    : ItemKind.Other;
            };
            _runController.AttachQuestService(_questService);
            _wildcardDays = new TheLongestYear.Loop.WildcardDayService(this.Monitor, () => _meta.Run);
            TheLongestYear.Loop.WildcardDayService.GrowthNight = () => _meta.Run.WildcardGrowthNight;
            TheLongestYear.Loop.WildcardDayService.NightRun = () => _meta.Run;
            _runController.AttachWildcardService(_wildcardDays);
            _runController.OnRunLoaded();
            if (_peakMineFloorTracker != null)
                this.Helper.Events.Player.Warped -= _peakMineFloorTracker.OnWarped;
            _peakMineFloorTracker = new PeakMineFloorTracker(this.Monitor, _meta.Run);
            this.Helper.Events.Player.Warped += _peakMineFloorTracker.OnWarped;
            // Restore stash chest on every save load (not just after reset), so a
            // save-and-reload mid-run re-places the chest correctly.
            _stashService.PlaceChest();
            _stashService.PopulateFromMeta();
            _planningShrine.Place(_stashService.LastPlacedTile);
            // Items the last rewind dropped on the ground, quit before the first night: drop them again.
            TheLongestYear.Loop.GroundDrop.RedropAfterLoad(_meta.State, this.Monitor);
            _purchases = new UpgradePurchaseService(this.Monitor, _meta, _config);
            _purchases.Purchased = id =>
            {
                if (id == TheLongestYear.Loop.PierreYear2SeedsService.UpgradeId)
                    this.Helper.GameContent.InvalidateCache(TheLongestYear.Loop.PierreYear2SeedsService.ShopAssetName);
                if (id == TheLongestYear.Core.AnimalPowers.LoyalPet)
                    _animalPowers.RefreshPets("bought");
            };
            _launcher = new MenuLauncher(this.Monitor, _config, _meta, _runController, _purchases);
            _runController.AttachLauncher(_launcher);
            _bookFurniture.AttachLauncher(() => _launcher);
            _planningShrine.AttachState(() => _meta.State);
            _planningShrine.AttachPriceFactor(() => _meta.State.EffectiveDifficulty(_config).ShrinePriceFactor);
            _boostEffects = new TheLongestYear.Loop.BoostEffectsService(this.Monitor, _meta);
            _boostPurchases = new BoostPurchaseService(this.Monitor, _meta, _boostEffects);
            _boostPurchases.Bought = id =>
            {
                if (TheLongestYear.Core.PastSeasonBoosts.SeasonOf(id) != null) _pastSeasonSpawns.Refresh(TodayDayOfYear());
            };
            _planningShrine.AttachBoosts(() => _meta.Run, (id, skill) =>
            {
                BoostPurchase.Result result = _boostPurchases.TryBuy(id, skill);
                // Sneak Peek takes over the Wednesday channel the moment it is bought, so the
                // label has to be re-edited now, not at the next day roll: the player can walk
                // home and switch the TV on the same afternoon.
                if (result == BoostPurchase.Result.Success && id == BoostId.SneakPeek)
                    this.RefreshSneakPeekChannelLabel();
                return result;
            });
            _planningShrine.AttachRestart(
                () => _runController?.IsVoluntaryRestartOffered() == true,
                () => _runController?.AskVoluntaryRestart());
            _planningShrine.AttachDonate(() => ShrineDonationService.Active);
            TheLongestYear.Loop.BoostEffectsService.SecondWindTonight = () => _boostEffects.Active(BoostId.SecondWind);
            TheLongestYear.Loop.BoostEffectsService.FastFriendsActive = () => _boostEffects.Active(BoostId.FastFriends);
            TheLongestYear.Loop.BoostEffectsService.HagglerActive = () => _boostEffects.Active(BoostId.Haggler);
            ActiveEffectsProvider.AttachBoosts(() => TheLongestYear.Core.BoostState.ActiveModifierIds(_meta.Run, TodayDayOfYear()));
            _boostEffects.ApplyDailyBuffs();
            TheLongestYear.Integration.RunReachEvaluator.AttachRunState(() => _meta.Run);
            TheLongestYear.Integration.RunReachEvaluator.DebugLog = s => this.Monitor.Log(s, LogLevel.Info);
            // Mid-run safety: ensure a loaded save has exactly one of each book in inventory.
            _bookFurniture.ReconcileInventory();
            // Fire intro quests (cookbook / craftbook / stash / fireplace) on every save load,
            // not just after reset. AddIntroQuest is idempotent against the questLog, so this
            // safely surfaces quests added in code rounds that pre-date this save (e.g. the
            // fireplace board intro added 2026-05-29 — without this call, current playthroughs
            // would have to roll over a full year before seeing it).
            _reset.FireBookQuestIntros();
            this.Monitor.Log(
                $"Run {_meta.Run.RunNumber} loaded ({_meta.Run.Season} {_meta.Run.DayOfMonth}). JP banked: {_meta.State.JunimoPoints}.",
                LogLevel.Info);
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
