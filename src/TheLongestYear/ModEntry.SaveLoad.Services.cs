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
        /// <summary>Load phase: the unlock, stash and patch services, the config merges and the reset service.</summary>
        private void BuildResetServices(LoadContext ctx)
        {
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
            ctx.ThemeOverrides = ParseThemeOverrides();
            ctx.ItemSeasonPins = ParseItemSeasonPins();
            ctx.BundleQuotas = ParseBundleQuotas();
            _reset = new WorldResetService(
                this.Monitor, _meta.State, _meta.Run, _config, _ccUnlock,
                this.Helper.DirectoryPath, farmerReset, professionPicker,
                _stashService, _mountainUnlock, _bookFurniture, _planningShrine,
                ctx.ItemSeasonPins, ctx.BundleQuotas, this.Helper.GameContent);
            // The rewind must let a legendary be caught again: the game blocks a repeat catch
            // through SpawnFishData.CatchLimit against player.fishCaught, and FarmerReset never
            // touched that record. Read the catch-limited ids once here (same shape as
            // GameDataPools's own Data/Locations read) and forward them through the reset service.
            _reset.CatchLimitedFishIds = ReadCatchLimitedFishIds();
            // Normal and Remixed roll a fresh Tech's Cross-Mod Bundles board each loop when that mod is loaded.
            _reset.TechBundles = new TheLongestYear.Loop.TechCrossModBundlesTarget(this.Helper.ModRegistry);
        }

        /// <summary>Load phase: the engine pools, season resolver, effort tables and the item availability model.</summary>
        private void BuildDataModels(LoadContext ctx)
        {
            // Engine pools double as season ground truth: fish/crab-pot spawn seasons feed
            // the SeasonResolver (so weekly themes can't ask for out-of-season fish, Nexus
            // 1122423) and DerivedSeasonPins feed the obtainability clamp below.
            // Held in a local (rather than the old new-and-Build one-liner) so its
            // LastReachability survives the call: the board repair below re-checks the LIVE board
            // against exactly the verdicts these pools were built from.
            ctx.EnginePoolReader = new TheLongestYear.Loop.GameDataPools(this.Monitor);
            ctx.EnginePools =
                ctx.EnginePoolReader.Build(_config.PoolTuning,
                    TheLongestYear.Core.YearTwoCrops.ExcludedFor(
                        _meta.State.HasUpgrade, _meta.State.BoardDifficulty(_config).Steps.ItemRarity));
            _seasonResolver = new SeasonResolver(
                TheLongestYear.Core.SpawnSeasonMap.FromPools(ctx.EnginePools));
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
            _enginePools = ctx.EnginePools;
            _engineReachability = ctx.EnginePoolReader.LastReachability;
            _itemSeasonPins = ctx.ItemSeasonPins;
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
        }

        /// <summary>Load phase: clamp unstackable asks and replace unreachable ones on the live board.</summary>
        private void RepairBoard(LoadContext ctx)
        {
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
                ? ctx.EnginePools
                : new TheLongestYear.Loop.GameDataPools(this.Monitor).Build(_config.PoolTuning,
                    TheLongestYear.Core.YearTwoCrops.ExcludedFor(
                        _meta.State.HasUpgrade, _meta.State.BoardDifficulty(_config).Steps.ItemRarity),
                    TheLongestYear.Loop.BundleEngine.VanillaOnlyIds);
            int repaired = new TheLongestYear.Loop.BoardRepairService(
                this.Monitor, ctx.EnginePoolReader.LastReachability, repairPools,
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
        }

        /// <summary>Load phase: the bundle catalog, the requirements, the board fingerprint and the donation services.</summary>
        private void BuildCatalogAndRequirements(LoadContext ctx)
        {
            _boardBuilder = new BundleCatalogBuilder(
                _config.RarityThresholds, _seasonResolver, this.Monitor,
                ctx.ThemeOverrides, ctx.ItemSeasonPins, ctx.BundleQuotas, _availability);
            // Obtainability clamp for the read-and-classify path: curated pins + the engine's
            // derived (earliest-obtainable) pins, so a Remixed/modded board can't demand an
            // unobtainable minimum. Due-date (PerItem) pins stay the curated set.
            var obtainabilityPins = new Dictionary<string, TheLongestYear.Core.Season>(
                ctx.EnginePools.DerivedSeasonPins,
                StringComparer.Ordinal);
            foreach (KeyValuePair<string, TheLongestYear.Core.Season> pin in ctx.ItemSeasonPins)
                obtainabilityPins[pin.Key] = pin.Value;
            _boardBuilder.ObtainabilityPins = obtainabilityPins;
            var builder = _boardBuilder;
            _catalog = builder.Build();
            _requirements = ResolveRequirements(builder, ctx.ItemSeasonPins, ctx.BundleQuotas);
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
        }

        /// <summary>Load phase: the weekly quest, shrine donations, the run controller and its hooks, and the mine tracker.</summary>
        private void WireRunController(LoadContext ctx)
        {
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
                new GoalGroupCap(ctx.EnginePools.FruitTreeFruitIds, 1),
                new GoalGroupCap(ctx.EnginePools.TrapFishIds, 1),
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
        }

        /// <summary>Load phase: stash, shrine, ground drops, purchases, menus, boosts, books and intro quests.</summary>
        private void PlaceAndWireFarmServices()
        {
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
        }
    }
}
