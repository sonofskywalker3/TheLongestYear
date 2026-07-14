using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarmonyLib;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using TheLongestYear.Core;
using TheLongestYear.Donations;
using TheLongestYear.Integration;
using TheLongestYear.Loop;
using TheLongestYear.UI;

namespace TheLongestYear
{
    public sealed class ModEntry : Mod
    {
        private GameplayConfig _config;
        private MetaStore _meta;
        private CommunityCenterUnlock _ccUnlock;
        private MountainUnlock _mountainUnlock;
        private StandardFarmEnforcer _standardFarmEnforcer;
        private WorldResetService _reset;
        private RunController _runController;
        private UpgradePurchaseService _purchases;
        private MenuLauncher _launcher;
        private SeasonResolver _seasonResolver;
        private IReadOnlyList<CcItem> _catalog = new List<CcItem>();
        private IReadOnlyList<BundleRequirement> _requirements = new List<BundleRequirement>();
        private DonationObserver _donationObserver;
        private CaveChoicePrompt _caveChoicePrompt;
        private PeakMineFloorTracker _peakMineFloorTracker;
        private JunimoStashService _stashService;
        private WeeklyThemeQuestService _questService;
        private IntroEventInjector _introInjector;
        private IntroSequenceDriver _introDriver;
        private Day28CutsceneDriver _day28Driver;
        private BookFurniture _bookFurniture;
        private UI.PlanningShrineService _planningShrine;
        private TheLongestYear.Loop.OnboardingMailService _onboardingMail;

        // Debug command-file bridge: lets the developer trigger tly_ actions by writing lines into a file
        // in the mod folder, so PC in-game testing needs no console typing (the mod polls + executes them).
        private const string DebugCommandFileName = "tly_commands.txt";
        private const int DebugPollTicks = 30;
        private string _commandFilePath;

        // True only once OnSaveLoaded has actually called _meta.Load() for the current save. Guards
        // OnSaving: when a save opens with TLY disabled or on a non-Standard farm we skip Load (the
        // early returns below), leaving MetaStore.State/Run at empty defaults — persisting those on the
        // next save would overwrite the player's banked progression with nothing. Reset on every load.
        private bool _metaLoaded;

        // True for the single OnSaveLoaded that immediately follows SaveCreating — i.e. a brand-new
        // game. That's the ONLY way to begin a Longest Year run: OnSaveLoaded stamps the per-save
        // marker and activates TLY. Loading any existing non-TLY save never sets this, so the mod
        // stays dormant. Consumed (reset to false) the moment OnSaveLoaded reads it.
        private bool _isNewGame;

        public override void Entry(IModHelper helper)
        {
            // .Default(key) makes a missing translation echo the raw key exactly, matching the
            // test provider's behavior (see I18nFixture) — SMAPI's own fallback is otherwise
            // "(no translation:{key})", which breaks ThemeModifiers.DisplayNameFor's raw-id
            // fallback check (it compares the resolved string against the key itself).
            TheLongestYear.Core.Strings.Init((key, tokens) =>
                tokens == null
                    ? this.Helper.Translation.Get(key).Default(key).ToString()
                    : this.Helper.Translation.Get(key, tokens).Default(key).ToString());

            _config = helper.ReadConfig<GameplayConfig>();

            // One-shot config migration.
            bool migrated = false;
            // 2026-05-28 second-pass migration for the stash tile:
            // The first migration set (72,12) as a hardcoded default, but the 2026-05-27 playtest
            // showed that tile is invisible on the Standard farm (under the farmhouse roof on
            // the user's save). Reset to (0,0) so JunimoStashService.PlaceChest auto-picks
            // relative to the FarmHouse entry instead.
            if (_config.StashTileX == 72 && _config.StashTileY == 12)
            {
                _config.StashTileX = 0; _config.StashTileY = 0; migrated = true;
            }
            if (migrated)
                this.Monitor.Log("Migrated config.json: applied new default tile coords.", LogLevel.Info);

            // Always write the config back on Entry so any newly-added fields (Enabled, new
            // tile defaults, future tuning knobs) become visible in config.json for the
            // player to edit. Existing customizations were already deserialized into _config
            // and are preserved by the write. SMAPI's WriteConfig is idempotent for
            // unchanged values.
            helper.WriteConfig(_config);

            _meta = new MetaStore(helper.Data);
            // v1.1 narrative intro — porch + CC events injected via asset edit. Constructed at
            // Entry (not OnSaveLoaded) so AssetRequested is hooked before the first asset load.
            // The edit handlers themselves don't touch MetaState; the mail-flag plumbing fires
            // later in OnSaveLoaded / OnSaving once a save is open.
            _introInjector = new IntroEventInjector(this.Monitor, _meta);
            // Drives the Lewis->Junimo cutscenes before player control on a fresh run, then opens
            // the picker. _launcher isn't built until OnSaveLoaded, so hand it a lazy accessor.
            _introDriver = new IntroSequenceDriver(this.Monitor, _meta, _config);
            _introDriver.Attach(helper, () => _launcher);
            // Day-28 bedtime Junimo cutscene (FAIL → shop+reset, CONTINUE → next season). Attached
            // once here; _runController is built on save load, so resolve it lazily like the picker.
            _day28Driver = new Day28CutsceneDriver(this.Monitor);
            _day28Driver.Attach(helper, () => _runController);
            // Placeable book furniture (Cookbook/Craftbook/Bundle-log) — registers via asset edit.
            _bookFurniture = new BookFurniture(this.Monitor, helper);
            // View-only planning shrine — registers its furniture + auto-places near the stash.
            _planningShrine = new UI.PlanningShrineService(this.Monitor, helper);
            // First-loop Spring-1 onboarding letter. Constructed at Entry so AssetRequested is
            // hooked before the first asset load (same reason as _introInjector above).
            _onboardingMail = new TheLongestYear.Loop.OnboardingMailService(this.Monitor, _meta);
            helper.Events.Content.AssetRequested += _onboardingMail.OnAssetRequested;
            helper.Events.GameLoop.SaveLoaded += this.OnSaveLoaded;
            helper.Events.GameLoop.SaveCreating += this.OnSaveCreating;
            helper.Events.GameLoop.ReturnedToTitle += this.OnReturnedToTitle;
            helper.Events.GameLoop.Saving += this.OnSaving;
            helper.Events.GameLoop.DayStarted += this.OnDayStarted;
            helper.Events.GameLoop.DayEnding += this.OnDayEnding;
            helper.Events.GameLoop.UpdateTicked += this.OnUpdateTicked;
            // 2026-05-29 round 10: switched RenderedHud → RenderingHud so the journal-icon
            // hover tooltip (drawn by vanilla as part of the regular HUD pass) lands ON TOP
            // of the JP HUD instead of being hidden behind it. Vanilla HUD elements like the
            // day/time/money box still cover our box at any overlap, but the position is
            // already below the box so no visual overlap there.
            helper.Events.Display.RenderingHud += this.OnRenderedHud;
            helper.Events.GameLoop.GameLaunched += this.OnGameLaunched;
            // Re-inject the onboarding mail body/title and furniture display names in the new
            // language when the player switches locale mid-session.
            this.Helper.Events.Content.LocaleChanged += (_, _) =>
            {
                this.Helper.GameContent.InvalidateCache("Data/Mail");
                this.Helper.GameContent.InvalidateCache("Data/Furniture");
            };

            // Force every new TLY game onto the Standard farm. Wired here (not in OnSaveLoaded)
            // because the enforcer needs to fire on the title screen / character-creation flow,
            // which is before any save is loaded.
            _standardFarmEnforcer = new StandardFarmEnforcer(this.Monitor, _config);
            _standardFarmEnforcer.Attach(helper);

            // 2026-05-29 round 11: PatchAll iterates [HarmonyPatch] classes in assembly order,
            // and a SINGLE bad attribute (e.g. ambiguous method match) throws and aborts the
            // rest of the iteration — that's how the round-8 EventSuppressionPatch silently
            // killed every later patch including the bonus-drop and stash-capacity ones. Walk
            // the patch classes ourselves and isolate each one so a single failure logs +
            // continues instead of cratering the whole pass.
            var harmony = new Harmony(this.ModManifest.UniqueID);
            int patched = 0, failed = 0;
            foreach (var type in System.Reflection.Assembly.GetExecutingAssembly().GetTypes())
            {
                if (type.GetCustomAttributes(typeof(HarmonyPatch), false).Length == 0) continue;
                try
                {
                    new PatchClassProcessor(harmony, type).Patch();
                    patched++;
                }
                catch (System.Exception ex)
                {
                    failed++;
                    this.Monitor.Log(
                        $"Harmony patch '{type.FullName}' failed to apply: {ex.GetType().Name}: {ex.Message}. " +
                        "Other patches will continue.",
                        LogLevel.Error);
                }
            }
            this.Monitor.Log(
                $"Harmony: {patched} patch class(es) applied, {failed} failed.",
                failed > 0 ? LogLevel.Warn : LogLevel.Info);

            // Observation-based donation detector. See DonationObserver.cs for why we can't rely
            // on a Harmony patch of Bundle.tryToDepositThisItem alone (the 2026-05-26 playtest
            // showed it didn't fire on real CC deposits).
            _donationObserver = new DonationObserver(helper, this.Monitor);

            // Per-loop mushrooms-vs-bats re-choice on cave entry — replaces the replaying
            // Demetrius cutscene (event-hygiene pass; see CaveChoicePrompt).
            _caveChoicePrompt = new CaveChoicePrompt(helper, this.Monitor);

            // The Cookbook, Craftbook, and Bundle-log are placeable book furniture now
            // (see BookFurniture) — no tile-anchored interactables.

            _commandFilePath = Path.Combine(helper.DirectoryPath, DebugCommandFileName);

            helper.ConsoleCommands.Add("tly_meta", "Print The Longest Year meta-state (requires a loaded save).", this.PrintMeta);
            helper.ConsoleCommands.Add("tly_loadsave", "Load a save by folder name from the title screen (debug/automation). Usage: tly_loadsave <saveFolderName>", this.CmdLoadSave);
            helper.ConsoleCommands.Add("tly_addjp", "Add Junimo Points in memory; persists on the next save. Usage: tly_addjp <amount>", this.AddJp);
            helper.ConsoleCommands.Add("tly_addmoney", "Add gold to the loaded farmer (debug). Usage: tly_addmoney <amount>", this.AddMoney);
            helper.ConsoleCommands.Add("tly_additem", "Grant an item to the farmer (debug). Usage: tly_additem <qualifiedId> [count]", this.CmdAddItem);
            helper.ConsoleCommands.Add("tly_removehorse", "Remove the stable + horse, clear the carryover snapshot, and drop the Keep Horse upgrade so it's re-buyable (debug — clean slate for a Keep-Horse carryover test).", this.CmdRemoveHorse);
            helper.ConsoleCommands.Add("tly_reset", "Force an in-place reset to Spring 1 (debug).", this.ForceReset);
            helper.ConsoleCommands.Add("tly_setday", "Jump the in-game date to <day> of the current season so you can sleep straight into that day's gate (e.g. day 28) without grinding a month. Sleep to trigger it. Usage: tly_setday <day>", this.CmdSetDay);
            helper.ConsoleCommands.Add("tly_failreset", "Simulate a day-28 gate-miss reset: opens the JP shrine, then resets to Spring 1 on close (debug — exercises the natural loop-reset path the JP-refund bug lived in).", this.CmdFailReset);
            helper.ConsoleCommands.Add("tly_win", "Open the basic win screen, then the JP shrine + keep-playing choice (debug — bypasses the first-win-only gate, re-runnable).", this.CmdForceWin);
            helper.ConsoleCommands.Add("tly_resetif", "Reset only if the loaded farmer's name matches. Usage: tly_resetif <name>", this.ResetIfNameMatches);
            helper.ConsoleCommands.Add("tly_leaktest", "Reset twice and report any state that leaks between runs (debug).", this.LeakTest);
            helper.ConsoleCommands.Add("tly_select", "Select one of this week's offered themes. Usage: tly_select <theme>", this.CmdSelect);
            helper.ConsoleCommands.Add("tly_offer", "Show this week's selection offer.", this.CmdOffer);
            helper.ConsoleCommands.Add("tly_donate", "Simulate a CC donation. Usage: tly_donate <itemId>", this.CmdDonate);
            helper.ConsoleCommands.Add("tly_runstate", "Print the current run state.", this.CmdRunState);
            helper.ConsoleCommands.Add("tly_catalog", "Print the bundle-derived CC catalog summary.", this.CmdCatalog);
            helper.ConsoleCommands.Add("tly_classify", "Re-run bundle classification over the live BundleData and log the summary (diagnostics only — does not touch the active run). Pairs with 'debug ShuffleBundles' to exercise remixed classification in memory.", this.CmdClassify);
            helper.ConsoleCommands.Add("tly_testdonate", "Simulate a CC donation through the JP service. Usage: tly_testdonate <qualifiedId> [count]", this.CmdTestDonate);
            helper.ConsoleCommands.Add("tly_openhub", "Open the weekly planning hub menu (debug).", this.CmdOpenHub);
            helper.ConsoleCommands.Add("tly_openshop", "Open the Junimo Shrine upgrade shop (debug).", this.CmdOpenShop);
            helper.ConsoleCommands.Add("tly_listupgrades", "List the upgrade catalog grouped by category.", this.CmdListUpgrades);
            helper.ConsoleCommands.Add("tly_dumpevents", "Audit Data/Events for furnace/cave/early-scene ids (debug — logs candidates so the event-gating tables use real ids, not guesses).", this.CmdDumpEvents);
            helper.ConsoleCommands.Add("tly_dumpreplayable", "Audit which Data/Events cutscenes the loop treats as REPLAYABLE (re-fire each loop): logs each unlock-granting event id, the matched grant command, whether it's excluded, and the active exclusion set (debug — diagnoses 'an event keeps replaying').", this.CmdDumpReplayable);
            helper.ConsoleCommands.Add("tly_buyupgrade", "Buy an upgrade by id (debug). Usage: tly_buyupgrade <id>", this.CmdBuyUpgrade);
            helper.ConsoleCommands.Add("tly_payvault", "Mark a Vault bundle as paid this run (debug — Harmony hookup is Plan 06). Usage: tly_payvault <season|index>", this.CmdPayVault);
            helper.ConsoleCommands.Add("tly_here", "Print the player's current tile coords (debug — useful for tuning interactable tile coords).", this.CmdHere);
            helper.ConsoleCommands.Add("tly_opencookbook",
                "Open the Cookbook menu directly (debug).",
                this.CmdOpenCookbook);
            helper.ConsoleCommands.Add("tly_opencraftbook",
                "Open the Craftbook menu directly (debug).",
                this.CmdOpenCraftbook);
            helper.ConsoleCommands.Add("tly_activeeffects",
                "Print the currently active theme bonus and liability.",
                this.CmdActiveEffects);
            helper.ConsoleCommands.Add("tly_setstash",
                "Anchor the Junimo Stash chest to the tile you are facing on the Farm. Writes config.json.",
                this.CmdSetStash);
            helper.ConsoleCommands.Add("tly_openstash",
                "Open the Junimo Stash chest directly (debug).",
                this.CmdOpenStash);
            helper.ConsoleCommands.Add("tly_stashclear",
                "Clear all items from the Junimo Stash MetaState (debug — DESTRUCTIVE).",
                this.CmdStashClear);
            helper.ConsoleCommands.Add("tly_wipemeta",
                "Wipe MetaState (JP, owned upgrades, stash contents, dismissed indicators) without " +
                "deleting the save. Persists immediately. Reload the save to fully apply " +
                "(some services cache the MetaState reference). DESTRUCTIVE.",
                this.CmdWipeMeta);
            helper.ConsoleCommands.Add("tly_replayintro",
                "Clear MetaState.HasSeenIntro + per-run intro mail flags so the day-1 Lewis+Junimo " +
                "intro chain re-fires on the next Spring 1. Pair with tly_reset to test immediately.",
                this.CmdReplayIntro);

            this.Monitor.Log("The Longest Year loaded.", LogLevel.Info);
        }

        /// <summary>Load this playthrough's banked progress when a save opens.</summary>
        private void OnSaveLoaded(object sender, SaveLoadedEventArgs e)
        {
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

            // Standard farm only. Tile defaults + building placement coords assume
            // the Standard farm layout. Other farm types (Riverland, Forest, Beach, etc.)
            // would land the stash chest / cookbook / craftbook / pre-built coops + barns
            // in unpredictable places (or in water). Skip setup with a clear log message.
            if (Game1.whichFarm != 0)
            {
                DeactivateTly();
                this.Monitor.Log(
                    $"TLY only supports the Standard farm (Game1.whichFarm == 0). " +
                    $"Current farm type is {Game1.whichFarm}. Skipping all setup. " +
                    $"To use TLY, start a new game on the Standard farm.",
                    LogLevel.Info);
                return;
            }

            _meta.Load();

            // Per-save opt-in. TLY only activates on a save that was STARTED as a Longest Year run:
            //   - a brand-new game created this session (_isNewGame, set by OnSaveCreating), or
            //   - a save that already carries the run marker, or
            //   - a pre-existing TLY save with banked data from before the marker existed (back-fill).
            // Any other save — a normal vanilla playthrough loaded with the mod installed — leaves
            // TLY fully dormant: no Harmony effects, no HUD, no reset loop. _metaLoaded stays false
            // so OnSaving never persists empty defaults over the player's real save data.
            bool isLongestYearSave = _isNewGame || _meta.State.IsLongestYearRun || _meta.LoadedExistingData;
            _isNewGame = false; // consume — only the load right after SaveCreating counts as new
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
            RunActivation.Activate();
            _metaLoaded = true;
            // Inject the tly_intro_done mail flag now if the player has already seen the intro
            // on a prior loop — that's what suppresses both intro events for years 2+.
            _introInjector?.ApplyMailFlagsForRun();
            UpgradeChecker.HasUpgrade = id => _meta.State.HasUpgrade(id);
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
            var farmerReset = new FarmerReset(this.Monitor);
            var professionPicker = new ProfessionPickerScheduler(this.Monitor);
            _stashService = new JunimoStashService(this.Monitor, _meta.State, _config);
            JunimoStashService.SetTextureLoader(
                () => this.Helper.ModContent.Load<Microsoft.Xna.Framework.Graphics.Texture2D>("assets/junimo_stash.png"));
            _meta.AttachStashService(_stashService);
            JunimoStashCapPatch.Connect(this.Monitor, _meta.State);
            JunimoStashCapacityPatch.Connect(_meta.State);
            XpMultiplierPatch.Connect(_meta.State);
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
                itemSeasonPins, bundleQuotas);

            _seasonResolver = new SeasonResolver();
            var builder = new BundleCatalogBuilder(
                _config.RarityThresholds, _seasonResolver, this.Monitor,
                themeOverrides, itemSeasonPins, bundleQuotas);
            _catalog = builder.Build();
            _requirements = ResolveRequirements(builder, itemSeasonPins, bundleQuotas);
            DonationService.Active = new DonationService(this.Monitor, _meta, _config);

            _questService = new WeeklyThemeQuestService(
                this.Monitor, _meta, _config,
                slotStateForBundle: RunController.SlotStateForBundle);
            // Wire the post-donation callback so each CC deposit refreshes the quest's progress
            // text (and auto-completes when every goal slot this week is complete).
            DonationService.Active.AfterDonation = _questService.OnItemDonated;

            _runController = new RunController(this.Monitor, _meta, _config, _reset, _catalog, _requirements);
            _runController.AttachQuestService(_questService);
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
            _purchases = new UpgradePurchaseService(this.Monitor, _meta);
            _launcher = new MenuLauncher(this.Monitor, _config, _meta, _runController, _purchases);
            _runController.AttachLauncher(_launcher);
            _bookFurniture.AttachLauncher(() => _launcher);
            _planningShrine.AttachState(() => _meta.State);
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
        /// Year run — remember it so the OnSaveLoaded that follows stamps the per-save marker and
        /// activates the mod. SaveCreating runs before save data is writable, so the actual stamp
        /// happens in OnSaveLoaded. Loading an existing save never fires this, which is what keeps TLY
        /// dormant on non-TLY saves.</summary>
        private void OnSaveCreating(object sender, SaveCreatingEventArgs e)
        {
            if (_config.Enabled)
                _isNewGame = true;
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
            ActiveEffectsProvider.Clear();
            TheLongestYear.Loop.UpgradeChecker.HasUpgrade = null;
            DonationService.Active = null;
            TheLongestYear.Loop.ReplayableEventScan.Clear();
            // The peak-mine-floor tracker is only subscribed/unsubscribed on the proceed path of
            // OnSaveLoaded; the dormant bail returns before that, so detach here too or a tracker
            // left over from a prior TLY save keeps firing on the non-TLY save's warps.
            if (_peakMineFloorTracker != null)
                this.Helper.Events.Player.Warped -= _peakMineFloorTracker.OnWarped;
        }

        /// <summary>Commit meta-state as part of the game's save — never eagerly, to prevent save-scumming.</summary>
        private void OnSaving(object sender, SavingEventArgs e)
        {
            // If this save opened without TLY setup (disabled in config, or a non-Standard farm),
            // _meta.Load() never ran and State/Run are empty defaults — persisting them would wipe
            // the player's banked progression. Skip the save entirely in that case.
            if (!_metaLoaded)
                return;

            // Promote per-run tly_intro_cc_seen mail to cross-run MetaState.HasSeenIntro BEFORE
            // we persist, so a save+reset can't lose the flag (mailReceived gets wiped by
            // FarmerReset.loadForNewGame, MetaState doesn't).
            _introInjector?.MarkIntroSeenIfApplicable();
            RecordSeenEvents();
            _meta.Save();
            this.Monitor.Log($"Meta-state saved with the game. JP banked: {_meta.State.JunimoPoints}.", LogLevel.Trace);
        }

        /// <summary>Merge the run's seen vanilla events into the cross-loop SeenEventsEver memory so a
        /// scene watched in any run stays suppressed on later loops (event-gating Phase 1). Called
        /// from OnSaving before the meta-state persists; FarmerReset re-seeds eventsSeen from it.</summary>
        /// <summary>Scan Data/Events for the events whose scripts grant the Furnace recipe or run the
        /// cave (bats/mushrooms) choice, logging their real ids + a snippet. The ids live in compiled
        /// content (not in code), so this audit is how the EventGatingTables get real ids rather than
        /// guesses. Loadable at the title or in-game.</summary>
        private void CmdDumpEvents(string command, string[] args)
        {
            string[] locations =
            {
                "Farm", "FarmHouse", "Town", "Mountain", "Beach", "Forest", "BusStop", "Backwoods",
                "Railroad", "Saloon", "SeedShop", "Blacksmith", "AnimalShop", "Hospital", "ScienceHouse",
                "JoshHouse", "HaleyHouse", "SamHouse", "Tent", "Trailer", "ManorHouse", "WizardHouse",
                "Sewer", "Mine", "Tunnel", "Woods", "CommunityCenter", "ArchaeologyHouse", "FishShop",
                "Sunroom", "AdventureGuild", "Greenhouse", "Cellar", "Desert", "Summit",
            };
            string[] tokens = { "Furnace", "cave", "mushroom", "fruitBat", "caveChoice" };

            int total = 0, hits = 0;
            foreach (string loc in locations)
            {
                System.Collections.Generic.Dictionary<string, string> data;
                try
                {
                    data = this.Helper.GameContent.Load<System.Collections.Generic.Dictionary<string, string>>($"Data/Events/{loc}");
                }
                catch (System.Exception)
                {
                    continue; // location has no event data file
                }
                if (data == null) continue;

                foreach (System.Collections.Generic.KeyValuePair<string, string> kv in data)
                {
                    total++;
                    string script = kv.Value ?? "";
                    int slash = kv.Key.IndexOf('/');
                    string id = slash < 0 ? kv.Key : kv.Key.Substring(0, slash);
                    foreach (string tok in tokens)
                    {
                        if (script.IndexOf(tok, System.StringComparison.OrdinalIgnoreCase) < 0)
                            continue;
                        hits++;
                        string snippet = script.Length > 140 ? script.Substring(0, 140) : script;
                        this.Monitor.Log($"[dumpevents] {loc} id={id} match='{tok}' :: {snippet}", LogLevel.Info);
                        break;
                    }
                }
            }
            this.Monitor.Log(
                $"[dumpevents] scanned {total} events across {locations.Length} locations; {hits} candidate(s) matched.",
                LogLevel.Info);
        }

        /// <summary>Audit the replayable-cutscene detection: scan the live save's events, log every
        /// unlock-granting cutscene with the matched grant command + whether the exclusion set drops it,
        /// then the resulting flagged-id set. Requires a loaded save (reads Game1.locations).</summary>
        private void CmdDumpReplayable(string command, string[] args)
        {
            if (!Context.IsWorldReady)
            {
                this.Monitor.Log("Load a save first.", LogLevel.Warn);
                return;
            }

            System.Collections.Generic.HashSet<string> exclude = BuildReplayableExclude();
            int total = 0, grants = 0, excluded = 0;

            foreach (GameLocation loc in Game1.locations)
            {
                if (string.IsNullOrEmpty(loc?.Name)) continue;

                System.Collections.Generic.Dictionary<string, string> data;
                try
                {
                    data = this.Helper.GameContent.Load<System.Collections.Generic.Dictionary<string, string>>($"Data/Events/{loc.Name}");
                }
                catch (System.Exception)
                {
                    continue;
                }
                if (data == null) continue;

                foreach (System.Collections.Generic.KeyValuePair<string, string> kv in data)
                {
                    total++;
                    string script = kv.Value ?? "";
                    string token = EventGatingTables.MatchedGrantToken(script);
                    if (token == null) continue;

                    grants++;
                    int slash = kv.Key.IndexOf('/');
                    string id = slash < 0 ? kv.Key : kv.Key.Substring(0, slash);
                    bool isExcluded = exclude.Contains(id);
                    if (isExcluded) excluded++;
                    string snippet = script.Length > 120 ? script.Substring(0, 120) : script;
                    this.Monitor.Log(
                        $"[dumpreplayable] {loc.Name} id={id} grant='{token}' excluded={isExcluded} :: {snippet}",
                        LogLevel.Info);
                }
            }

            this.Monitor.Log(
                $"[dumpreplayable] scanned {total} events; {grants} grant-cutscene(s), {excluded} excluded, " +
                $"{grants - excluded} flagged replayable (config enabled={_config.AutoDetectReplayableUnlockCutscenes}). " +
                $"Exclusion set has {exclude.Count} id(s). Vanilla base always-replayable: " +
                $"[{string.Join(",", EventGatingTables.Default.ReplayableEventIds)}].",
                LogLevel.Info);
        }

        /// <summary>Merge the run's seen vanilla events into the cross-loop SeenEventsEver memory so a
        /// scene watched in any run stays suppressed on later loops (event-gating Phase 1). Called
        /// from OnSaving before the meta-state persists; FarmerReset re-seeds eventsSeen from it.</summary>
        private void RecordSeenEvents()
        {
            if (!Context.IsWorldReady || Game1.player?.eventsSeen == null)
                return;

            System.Collections.Generic.List<string> seen = _meta.State.SeenEventsEver;
            var known = new System.Collections.Generic.HashSet<string>(seen, System.StringComparer.Ordinal);
            int added = 0;
            foreach (string id in Game1.player.eventsSeen)
                if (known.Add(id)) { seen.Add(id); added++; }

            if (added > 0)
                this.Monitor.Log(
                    $"Recorded {added} newly-seen event id(s) to SeenEventsEver (total {seen.Count}).",
                    LogLevel.Trace);
        }

        /// <summary>The exclusion seed for the replayable-cutscene scan: events we explicitly suppress
        /// (<see cref="TheLongestYear.Loop.EventSuppressionPatch.SuppressedEventIds"/>, e.g. the Lewis
        /// CC intro) plus relationship/heart events (which re-fire via their own reseed skip). An event
        /// in this set is never auto-flagged as a wipe-able unlock grant.</summary>
        private static System.Collections.Generic.HashSet<string> BuildReplayableExclude()
        {
            var exclude = new System.Collections.Generic.HashSet<string>(
                TheLongestYear.Loop.EventSuppressionPatch.SuppressedEventIds,
                System.StringComparer.Ordinal);
            exclude.UnionWith(TheLongestYear.Loop.RelationshipEventIndex.Ids);
            // Demetrius cave (65): plays once, then stays seen — the per-loop re-choice is
            // CaveChoicePrompt's job now, so the scan must never re-flag it as replayable.
            exclude.Add("65");
            return exclude;
        }

        /// <summary>Load a save from the title screen by folder name — the same
        /// <c>SaveGame.Load(slotName)</c> + <c>Game1.exitActiveMenu()</c> pair LoadGameMenu's slot
        /// click makes (LoadGameMenu.cs:85-86). Both calls are required: without the menu exit the
        /// TitleMenu stays active after the loader finishes, keeps drawing the title screen, and the
        /// world never proceeds (no SaveLoaded, frozen log).
        /// Debug/automation tool: lets an unattended session load a save via console injection to
        /// read the SaveLoaded diagnostics (e.g. the remixed-bundle classification lines) without
        /// clicking through the title menu. Refuses while a save is already loaded.</summary>
        private void CmdLoadSave(string command, string[] args)
        {
            if (Context.IsWorldReady)
            {
                this.Monitor.Log("A save is already loaded — return to title first (tly_loadsave is title-screen-only).", LogLevel.Warn);
                return;
            }
            if (args.Length < 1 || string.IsNullOrWhiteSpace(args[0]))
            {
                this.Monitor.Log("Usage: tly_loadsave <saveFolderName>  (e.g. tly_loadsave None_123456789)", LogLevel.Info);
                return;
            }

            this.Monitor.Log($"tly_loadsave: loading '{args[0]}'.", LogLevel.Info);
            StardewValley.SaveGame.Load(args[0]);
            Game1.exitActiveMenu();
        }

        private void PrintMeta(string command, string[] args)
        {
            if (!Context.IsWorldReady)
            {
                this.Monitor.Log("Load a save first.", LogLevel.Warn);
                return;
            }

            MetaState s = _meta.State;
            int stashSlots = s.StashSlotCount;
            int stashItems = s.StashItems.Count;
            string stashTile = (_config.StashTileX == 0 && _config.StashTileY == 0)
                ? "auto (relative to farmhouse entry)"
                : $"({_config.StashTileX}, {_config.StashTileY})";

            this.Monitor.Log(
                $"JP={s.JunimoPoints}, " +
                $"StashTier={s.HighestKeptTier("stash_", 3)} ({stashSlots} slots, {stashItems} items banked, tile {stashTile}), " +
                $"Upgrades=[{string.Join(", ", s.OwnedUpgrades)}]",
                LogLevel.Info);
        }

        private void AddJp(string command, string[] args)
        {
            if (!Context.IsWorldReady)
            {
                this.Monitor.Log("Load a save first.", LogLevel.Warn);
                return;
            }

            if (args.Length < 1 || !long.TryParse(args[0], out long amount))
            {
                this.Monitor.Log("Usage: tly_addjp <amount>", LogLevel.Warn);
                return;
            }

            _meta.State.JunimoPoints += amount;
            this.Monitor.Log($"JP is now {_meta.State.JunimoPoints} (in memory — persists on next save).", LogLevel.Info);
        }

        /// <summary>Debug: jump to a given day of the current season (console alias for the file-bridge
        /// <c>tly_setday</c>). Sleep afterward to trigger that day's gate. Usage: tly_setday &lt;day&gt;
        /// (defaults to 28).</summary>
        private void CmdSetDay(string command, string[] args)
        {
            if (!Context.IsWorldReady)
            {
                this.Monitor.Log("Load a save first.", LogLevel.Warn);
                return;
            }

            int day = args.Length > 0 && int.TryParse(args[0], out int d) ? d : 28;
            _runController?.DebugSetDay(day);
        }

        /// <summary>Debug: add gold to the loaded farmer. Mirrors <see cref="AddJp"/>; used for
        /// playtest setup (e.g. enough to upgrade the farmhouse). Usage: tly_addmoney &lt;amount&gt;.</summary>
        private void AddMoney(string command, string[] args)
        {
            if (!Context.IsWorldReady)
            {
                this.Monitor.Log("Load a save first.", LogLevel.Warn);
                return;
            }

            if (args.Length < 1 || !int.TryParse(args[0], out int amount))
            {
                this.Monitor.Log("Usage: tly_addmoney <amount>", LogLevel.Warn);
                return;
            }

            Game1.player.Money += amount;
            this.Monitor.Log($"Gold is now {Game1.player.Money}.", LogLevel.Info);
        }

        /// <summary>Debug: grant an item to the farmer (overflow goes to the item-grab menu so
        /// nothing is lost). Usage: <c>tly_additem &lt;qualifiedId&gt; [count]</c> — e.g.
        /// <c>tly_additem (O)709 100</c> for the 100 Hardwood a Stable build needs.</summary>
        private void CmdAddItem(string command, string[] args)
        {
            if (!Context.IsWorldReady) { this.Monitor.Log("Load a save first.", LogLevel.Warn); return; }
            if (args.Length < 1) { this.Monitor.Log("Usage: tly_additem <qualifiedId> [count]", LogLevel.Warn); return; }

            int count = args.Length > 1 && int.TryParse(args[1], out int c) ? c : 1;
            Item item;
            try { item = ItemRegistry.Create(args[0], count); }
            catch (Exception ex)
            {
                this.Monitor.Log($"tly_additem: couldn't create '{args[0]}': {ex.Message}", LogLevel.Warn);
                return;
            }

            Game1.player.addItemByMenuIfNecessary(item);
            this.Monitor.Log($"tly_additem: granted {count}x {args[0]} ({item.DisplayName}).", LogLevel.Info);
        }

        /// <summary>Debug: clean slate for a Keep-Horse carryover test. Demolishes every Stable on
        /// the Farm (removing its horse), clears <see cref="MetaState.HorseState"/> so the snapshot
        /// isn't restored, and drops the <c>early_horse</c> upgrade so the shrine shop re-offers
        /// "Keep Horse". Buy it again + build a stable to test carryover with a real, named horse.</summary>
        private void CmdRemoveHorse(string command, string[] args)
        {
            if (!Context.IsWorldReady) { this.Monitor.Log("Load a save first.", LogLevel.Warn); return; }

            int removed = 0;
            StardewValley.Farm farm = Game1.getFarm();
            if (farm != null)
            {
                foreach (StardewValley.Buildings.Stable stable in farm.buildings.OfType<StardewValley.Buildings.Stable>().ToList())
                {
                    StardewValley.Characters.Horse horse = stable.getStableHorse();
                    if (horse != null)
                        farm.characters.Remove(horse);
                    farm.buildings.Remove(stable);
                    removed++;
                }
            }

            _meta.State.HorseState = null;
            bool hadUpgrade = _meta.State.OwnedUpgrades.Remove(TheLongestYear.Loop.HorseCarryoverService.UpgradeId);
            this.Monitor.Log(
                $"tly_removehorse: demolished {removed} stable(s), cleared HorseState, " +
                $"Keep Horse upgrade {(hadUpgrade ? "removed (re-buyable)" : "was not owned")}. " +
                "Persists on next save.",
                LogLevel.Info);
        }

        /// <summary>Reset only if the loaded save's farmer name matches the argument. Used by the
        /// debug-command-file bridge to queue a "reset on next load of save X" without affecting
        /// other saves (e.g. write 'tly_resetif puffpuff' before exit, then loading puffpuff
        /// resets it but loading any other save is a no-op).</summary>
        private void ResetIfNameMatches(string command, string[] args)
        {
            if (!Context.IsWorldReady)
            {
                this.Monitor.Log("Load a save first.", LogLevel.Warn);
                return;
            }

            if (args.Length < 1)
            {
                this.Monitor.Log("Usage: tly_resetif <farmerName>", LogLevel.Warn);
                return;
            }

            string target = args[0];
            string current = Game1.player?.Name ?? "";
            if (!string.Equals(current, target, StringComparison.OrdinalIgnoreCase))
            {
                this.Monitor.Log(
                    $"tly_resetif: current save is '{current}', not '{target}'. Skipping reset.",
                    LogLevel.Info);
                return;
            }

            this.Monitor.Log($"tly_resetif: name matches '{target}', resetting.", LogLevel.Info);
            FullResetAndPresentOffer();
        }

        /// <summary>Print the player's current tile coordinate. Used for tuning interactable
        /// tile coords (e.g. finding the fireplace before running tly_setboard).</summary>
        private void CmdHere(string command, string[] args)
        {
            if (!Context.IsWorldReady)
            {
                this.Monitor.Log("Load a save first.", LogLevel.Warn);
                return;
            }

            int x = (int)Game1.player.Tile.X;
            int y = (int)Game1.player.Tile.Y;
            string loc = Game1.currentLocation?.Name ?? "?";
            this.Monitor.Log($"Player at tile ({x}, {y}) in '{loc}'.", LogLevel.Info);
        }

        private void CmdOpenCookbook(string command, string[] args)
        {
            if (!Context.IsWorldReady) { this.Monitor.Log("Load a save first.", LogLevel.Warn); return; }
            _launcher?.OpenCookbook();
        }

        private void CmdOpenCraftbook(string command, string[] args)
        {
            if (!Context.IsWorldReady) { this.Monitor.Log("Load a save first.", LogLevel.Warn); return; }
            _launcher?.OpenCraftbook();
        }

        private void CmdSetStash(string command, string[] args)
        {
            if (!Context.IsWorldReady) { this.Monitor.Log("Load a save first.", LogLevel.Warn); return; }
            if (Game1.currentLocation is not Farm)
            {
                this.Monitor.Log("tly_setstash: stand on the Farm first.", LogLevel.Warn);
                return;
            }
            int dx = Game1.player.FacingDirection == 1 ? 1 : Game1.player.FacingDirection == 3 ? -1 : 0;
            int dy = Game1.player.FacingDirection == 2 ? 1 : Game1.player.FacingDirection == 0 ? -1 : 0;
            _config.StashTileX = (int)Game1.player.Tile.X + dx;
            _config.StashTileY = (int)Game1.player.Tile.Y + dy;
            this.Helper.WriteConfig(_config);
            this.Monitor.Log(
                $"Junimo Stash anchored to ({_config.StashTileX}, {_config.StashTileY}). Saved to config.json.",
                LogLevel.Info);
            // Immediately re-place the chest at the new tile.
            _stashService?.PlaceChest();
            _stashService?.PopulateFromMeta();
        }

        private void CmdOpenStash(string command, string[] args)
        {
            if (!Context.IsWorldReady) { this.Monitor.Log("Load a save first.", LogLevel.Warn); return; }
            var chest = _stashService?.FindStashChest();
            if (chest == null)
            {
                this.Monitor.Log("No stash chest found. Own stash_1 and run tly_setstash first.", LogLevel.Warn);
                return;
            }
            chest.ShowMenu();
        }

        private void CmdStashClear(string command, string[] args)
        {
            if (!Context.IsWorldReady) { this.Monitor.Log("Load a save first.", LogLevel.Warn); return; }
            _meta.State.StashItems.Clear();
            var chest = _stashService?.FindStashChest();
            if (chest != null)
                chest.Items.Clear();
            this.Monitor.Log("Junimo Stash MetaState cleared (in memory — persists on next save).", LogLevel.Warn);
        }

        /// <summary>
        /// Wipe MetaState (JP, owned upgrades, stash items, dismissed indicators, kept tools/skills/
        /// buildings, completed-resets counter) without deleting the save file. Persisted
        /// immediately so a save reload picks up the clean slate. Intended for playtest iteration —
        /// "I want to test a fresh-save run without redoing character creation."
        /// </summary>
        private void CmdWipeMeta(string command, string[] args)
        {
            if (!Context.IsWorldReady) { this.Monitor.Log("Load a save first.", LogLevel.Warn); return; }

            long oldJp = _meta.State.JunimoPoints;
            int oldUpgrades = _meta.State.OwnedUpgrades.Count;
            int oldStashItems = _meta.State.StashItems.Count;

            _meta.WipeMeta();

            this.Monitor.Log(
                $"tly_wipemeta: MetaState wiped (was JP={oldJp}, upgrades={oldUpgrades}, " +
                $"stash items={oldStashItems}). Persisted to save. " +
                "Reload the save (or run tly_reset) to apply — some services hold the old " +
                "MetaState reference until OnSaveLoaded re-attaches them.",
                LogLevel.Warn);
        }

        private void CmdReplayIntro(string command, string[] args)
        {
            if (!Context.IsWorldReady) { this.Monitor.Log("Load a save first.", LogLevel.Warn); return; }
            _introInjector?.ClearIntroState();
        }

        private void CmdActiveEffects(string command, string[] args)
        {
            string bonus = TheLongestYear.Core.ActiveEffectsProvider.BonusId ?? "(none)";
            string liability = TheLongestYear.Core.ActiveEffectsProvider.LiabilityId ?? "(none)";
            this.Monitor.Log(
                $"Active effects: bonus={bonus}, liability={liability}. " +
                $"Selection={_meta?.Run.CurrentSelection?.ToString() ?? "none"}.",
                LogLevel.Info);
        }

        private void ForceReset(string command, string[] args)
        {
            if (!Context.IsWorldReady)
            {
                this.Monitor.Log("Load a save first.", LogLevel.Warn);
                return;
            }

            FullResetAndPresentOffer();
        }

        /// <summary>Debug: simulate a day-28 gate-miss reset (shrine-spend → reset → persist),
        /// the natural loop-boundary path. See <see cref="RunController.DebugForceFailReset"/>.</summary>
        private void CmdFailReset(string command, string[] args)
        {
            if (!Context.IsWorldReady)
            {
                this.Monitor.Log("Load a save first.", LogLevel.Warn);
                return;
            }

            _runController?.DebugForceFailReset();
        }

        /// <summary>Debug: open the basic win screen → JP shrine → keep-playing choice, the real
        /// win-path flow. See <see cref="RunController.DebugForceWin"/>.</summary>
        private void CmdForceWin(string command, string[] args)
        {
            if (!Context.IsWorldReady)
            {
                this.Monitor.Log("Load a save first.", LogLevel.Warn);
                return;
            }

            _runController?.DebugForceWin();
        }

        /// <summary>Full reset: rebuild the world (PerformReset), wipe RunState (BeginNewRun),
        /// and fire the Spring 1 hub. Used by both <see cref="ForceReset"/> and
        /// <see cref="ResetIfNameMatches"/>.
        ///
        /// 2026-05-26 round-2 bug: log showed a deferred SaveLoaded event firing AFTER this
        /// method returned, which called <c>_meta.Load()</c> and overwrote our in-memory
        /// BeginNewRun with the stale on-disk state ("the reset didn't remove the foraging
        /// items I had donated"). Fix: commit the cleared state to disk immediately after
        /// BeginNewRun so the subsequent SaveLoaded's Load reads the post-reset state.</summary>
        private void FullResetAndPresentOffer()
        {
            // Tech-debt consolidation (2026-06-10): the debug reset is now a thin alias for THE
            // shared finalizer (RunController.FinalizeReset) instead of a hand-copied subset. That
            // makes tly_reset a faithful stand-in for the real fail-day-28 reset (it previously
            // skipped ActiveEffectsProvider.Clear — leaking the old theme's effects — and
            // ForceFullSave, and presented the offer via PresentOffer(1) instead of the real
            // day-start flow). Cross-cutting reset fixes now land once, in FinalizeReset.
            if (_runController == null)
            {
                this.Monitor.Log("Reset unavailable: no run controller (load a save first).", LogLevel.Warn);
                return;
            }
            _runController.FinalizeReset("debug tly_reset");
        }

        private void LeakTest(string command, string[] args)
        {
            if (!Context.IsWorldReady)
            {
                this.Monitor.Log("Load a save first.", LogLevel.Warn);
                return;
            }

            _reset.PerformReset();
            _reset.ProfessionPicker.DrainOnDayStart();
            var first = WorldStateProbe.Capture();

            _reset.PerformReset();
            _reset.ProfessionPicker.DrainOnDayStart();
            var second = WorldStateProbe.Capture();

            this.Monitor.Log(
                $"Leak test object counts (informational, non-deterministic world-gen): {first.PlacedObjectCount} vs {second.PlacedObjectCount}.",
                LogLevel.Info);

            var diff = first.Diff(second);
            if (diff.Count == 0)
            {
                this.Monitor.Log("Leak test PASSED: two consecutive resets produced an identical baseline.", LogLevel.Info);
            }
            else
            {
                this.Monitor.Log($"Leak test FAILED: {diff.Count} field(s) leaked between runs:", LogLevel.Error);
                foreach (string d in diff)
                    this.Monitor.Log($"  - {d}", LogLevel.Error);
            }
        }

        /// <summary>Re-draw the clock/date/money HUD during festivals + draw the always-on JP
        /// HUD. Vanilla's drawHUD short-circuits on eventUp (Game1.cs:15410) so the festival
        /// re-draw is needed for the clock when FestivalTimeFlow is active; the JP HUD piggy-
        /// backs on the same event hook with its own visibility gating.</summary>
        private void OnRenderedHud(object sender, StardewModdingAPI.Events.RenderingHudEventArgs e)
        {
            if (!RunActivation.IsActive)
                return;
            if (Game1.isFestival() && Game1.dayTimeMoneyBox != null)
                Game1.dayTimeMoneyBox.draw(e.SpriteBatch);

            DrawJpHud(e.SpriteBatch);
        }

        /// <summary>
        /// Always-on top-right HUD showing banked JP + the current week's theme. Two lines max:
        /// <c>JP: 123</c> on top, <c>Mining (1.5x)</c> (or <c>Mining (1.5x, lifted)</c> when the
        /// weekly theme quest is complete and the drawback is suppressed) on the bottom.
        /// Positioned directly below the vanilla day/time/money box so it doesn't fight other
        /// HUD elements for screen space. Hidden when the player has toggled the HUD off
        /// (<c>Game1.displayHUD</c>), during cutscenes (<c>Game1.eventUp</c>), or when the
        /// mod-side toggle <see cref="GameplayConfig.ShowJpHud"/> is off.
        /// </summary>
        private void DrawJpHud(Microsoft.Xna.Framework.Graphics.SpriteBatch b)
        {
            if (_meta == null) return;
            if (!Context.IsWorldReady) return;
            if (!_config.ShowJpHud) return;
            if (!Game1.displayHUD) return;
            if (Game1.eventUp) return;

            long jp = _meta.State.JunimoPoints;
            // 2026-05-29 playtest: theme line removed. The current theme + lifted/active state
            // already shows on the WeeklyThemeQuest entry in the player's quest log, so the
            // HUD echoing it was redundant and made the box too tall after the dialogueFont
            // bump. Keep this minimal — just the banked JP count.
            var lines = new System.Collections.Generic.List<string> { $"JP: {jp}" };

            const int Padding = 14;
            const int LineGap = 6;
            // dialogueFont scaled to 0.95 — the unscaled version was "about 5% too big" per
            // the 2026-05-29 playtest. Padding also pulled back from 16 → 14 to match the
            // tighter text bounds.
            var font = Game1.dialogueFont;
            const float TextScale = 0.95f;

            float maxWidth = 0f;
            float totalHeight = 0f;
            foreach (string line in lines)
            {
                Microsoft.Xna.Framework.Vector2 size = font.MeasureString(line) * TextScale;
                if (size.X > maxWidth) maxWidth = size.X;
                totalHeight += size.Y;
            }
            if (lines.Count > 1) totalHeight += LineGap * (lines.Count - 1);

            int boxWidth = (int)maxWidth + Padding * 2;
            int boxHeight = (int)totalHeight + Padding * 2;

            // Position: top-right, BELOW the vanilla day/time/money box. 2026-05-28 round 4:
            // user reported the HUD sat "a little too low" — dropped the spacer from 80px to
            // 24px so it nests just under the box without leaving a visible gap. Read the
            // box's height via reflection (DayTimeMoneyBox.height is a static on PC, instance
            // on Android — same field name, different shape).
            int x = Game1.uiViewport.Width - boxWidth - 8;
            int boxTopY = Game1.dayTimeMoneyBox?.yPositionOnScreen ?? 0;
            int hudBoxHeight = 228;
            var hf = typeof(StardewValley.Menus.DayTimeMoneyBox).GetField("height",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic
                | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static
                | System.Reflection.BindingFlags.FlattenHierarchy);
            if (hf != null)
            {
                object hv = hf.IsStatic ? hf.GetValue(null) : hf.GetValue(Game1.dayTimeMoneyBox);
                if (hv is int hi && hi > 0) hudBoxHeight = hi;
            }
            int y = boxTopY + hudBoxHeight + 24;

            StardewValley.Menus.IClickableMenu.drawTextureBox(b, x, y, boxWidth, boxHeight,
                Microsoft.Xna.Framework.Color.White);

            int textY = y + Padding;
            foreach (string line in lines)
            {
                StardewValley.Utility.drawTextWithShadow(b, line, font,
                    new Microsoft.Xna.Framework.Vector2(x + Padding, textY), Game1.textColor,
                    scale: TextScale);
                textY += (int)(font.MeasureString(line).Y * TextScale) + LineGap;
            }
        }

        private void OnGameLaunched(object sender, StardewModdingAPI.Events.GameLaunchedEventArgs e)
        {
            // Cart Whisperer extends to every day when the standalone Cart Catalog mod is installed.
            TheLongestYear.Loop.CartCatalogIntegration.ModLoaded =
                this.Helper.ModRegistry.IsLoaded(TheLongestYear.Loop.CartCatalogIntegration.ModId);

            this.ApplyWindowSize();

            var gmcm = this.Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
            if (gmcm == null)
            {
                this.Monitor.Log("GMCM not installed — config edits via config.json only.", LogLevel.Trace);
                return;
            }

            gmcm.Register(this.ModManifest,
                reset: () => _config = new GameplayConfig(),
                save: () => this.Helper.WriteConfig(_config));

            gmcm.AddSectionTitle(this.ModManifest, () => Strings.Get("gmcm.section"));
            gmcm.AddParagraph(this.ModManifest,
                () => Strings.Get("gmcm.master-blurb"));
            gmcm.AddBoolOption(this.ModManifest,
                getValue: () => _config.Enabled,
                setValue: v => _config.Enabled = v,
                name: () => Strings.Get("gmcm.enabled.name"));

            gmcm.AddBoolOption(this.ModManifest,
                getValue: () => _config.ShowJpHud,
                setValue: v => _config.ShowJpHud = v,
                name: () => Strings.Get("gmcm.jp-hud.name"),
                tooltip: () => Strings.Get("gmcm.jp-hud.tooltip"));

            gmcm.AddBoolOption(this.ModManifest,
                getValue: () => _config.AutoDetectReplayableUnlockCutscenes,
                setValue: v => _config.AutoDetectReplayableUnlockCutscenes = v,
                name: () => Strings.Get("gmcm.auto-detect.name"),
                tooltip: () => Strings.Get("gmcm.auto-detect.tooltip"));

            this.Monitor.Log("Registered GMCM options.", LogLevel.Info);
        }

        /// <summary>SDV doesn't persist a windowed width/height (it always boots at 1280×720 in
        /// windowed mode), and the dev redeploy loop force-kills the game so it never saves one on
        /// exit. When <see cref="GameplayConfig.WindowWidth"/>/<c>Height</c> are positive and the
        /// game is NOT in fullscreen, nudge the window to that size once the game is up — the game's
        /// own ClientSizeChanged handler then re-derives the viewport. 0 (either dim) = leave alone.</summary>
        private void ApplyWindowSize()
        {
            int w = _config.WindowWidth, h = _config.WindowHeight;
            if (w <= 0 || h <= 0)
                return;
            if (Game1.graphics == null || Game1.graphics.IsFullScreen)
                return;
            if (Game1.graphics.PreferredBackBufferWidth == w && Game1.graphics.PreferredBackBufferHeight == h)
                return;

            Game1.graphics.PreferredBackBufferWidth = w;
            Game1.graphics.PreferredBackBufferHeight = h;
            Game1.graphics.ApplyChanges();
            this.Monitor.Log($"Window: set to {w}x{h} (config dial).", LogLevel.Info);
        }

        private void OnDayStarted(object sender, StardewModdingAPI.Events.DayStartedEventArgs e)
        {
            if (!RunActivation.IsActive) return;
            _onboardingMail?.OnDayStarted();
            _runController?.OnDayStarted(sender, e);
        }

        private void OnDayEnding(object sender, StardewModdingAPI.Events.DayEndingEventArgs e)
        {
            if (!RunActivation.IsActive) return;
            _runController?.OnDayEnding(sender, e);
        }


        /// <summary>
        /// Poll the debug command file (mod folder) and execute any queued lines once. Lets the developer
        /// drive tly_ actions by writing the file while the player only plays — no in-game console needed.
        /// </summary>
        private void OnUpdateTicked(object sender, UpdateTickedEventArgs e)
        {
            if (Context.IsWorldReady)
            {
                // Dormant on non-TLY saves: no deferred-offer retry, no festival auto-eject, no debug
                // bridge (the bridge must never run commands against a save the mod is dormant on).
                if (!RunActivation.IsActive)
                    return;

                // Re-attempt a planning-hub open that was deferred because the menu surface was busy
                // (the post-win keep-playing dialogue still closing when the new loop's reset fired).
                // Gate on a clear surface so the retry opens cleanly and doesn't re-log every tick.
                if (Game1.activeClickableMenu == null && !Game1.eventUp)
                    _runController?.TryDrainDeferredOffer();

                // Festival auto-eject runs every tick (cheap conditional — most ticks bail in the first check).
                // Has to be every tick, not just on the DebugPollTicks cadence, so we eject right at the
                // festival's end time rather than up to 30 ticks (~500ms) later.
                if (FestivalTimeFlow.ShouldAutoEnd())
                    FestivalTimeFlow.ForceEnd(this.Monitor);
            }
            // No world loaded (title screen): fall through to the bridge poll so tly_loadsave can
            // start an unattended session. There is no save to be dormant on, and every
            // world-touching command guards on Context.IsWorldReady itself ("Load a save first").

            // The file bridge is developer-only and off by default — a shipped build must not watch
            // the filesystem or run queued tly_ commands (some destructive) the player never typed.
            if (!_config.EnableDebugCommandBridge)
                return;

            if (!e.IsMultipleOf(DebugPollTicks))
                return;
            if (string.IsNullOrEmpty(_commandFilePath) || !File.Exists(_commandFilePath))
                return;

            string[] lines;
            try
            {
                lines = File.ReadAllLines(_commandFilePath);
                File.Delete(_commandFilePath); // consume once; the file may be re-written for the next batch
            }
            catch (IOException)
            {
                return; // file is mid-write — retry on the next poll
            }

            foreach (string raw in lines)
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#"))
                    continue;

                this.Monitor.Log($"Debug bridge: executing '{line}'.", LogLevel.Info);
                this.ExecuteDebugLine(line);
            }
        }

        /// <summary>Parse one "tly_command arg1 arg2" line and route it to the matching command handler.</summary>
        private void ExecuteDebugLine(string line)
        {
            string[] parts = line.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            string command = parts[0].ToLowerInvariant();
            string[] args = parts.Skip(1).ToArray();

            switch (command)
            {
                case "tly_meta": this.PrintMeta(command, args); break;
                case "tly_loadsave": this.CmdLoadSave(command, args); break;
                case "tly_addjp": this.AddJp(command, args); break;
                case "tly_addmoney": this.AddMoney(command, args); break;
                case "tly_additem": this.CmdAddItem(command, args); break;
                case "tly_removehorse": this.CmdRemoveHorse(command, args); break;
                case "tly_reset": this.ForceReset(command, args); break;
                case "tly_win":
                    if (!Context.IsWorldReady) { this.Monitor.Log("Load a save first.", LogLevel.Warn); break; }
                    _runController?.DebugForceWin(); break;
                case "tly_failreset":
                    if (!Context.IsWorldReady) { this.Monitor.Log("Load a save first.", LogLevel.Warn); break; }
                    _runController?.DebugForceFailReset(); break;
                case "tly_day28continue":
                    if (!Context.IsWorldReady) { this.Monitor.Log("Load a save first.", LogLevel.Warn); break; }
                    _runController?.DebugForceContinueCutscene(); break;
                case "tly_setday":
                    if (!Context.IsWorldReady) { this.Monitor.Log("Load a save first.", LogLevel.Warn); break; }
                    _runController?.DebugSetDay(args.Length > 0 && int.TryParse(args[0], out int d) ? d : 28);
                    break;
                case "tly_resetif": this.ResetIfNameMatches(command, args); break;
                case "tly_leaktest": this.LeakTest(command, args); break;
                case "tly_select": this.CmdSelect(command, args); break;
                case "tly_offer": this.CmdOffer(command, args); break;
                case "tly_donate": this.CmdDonate(command, args); break;
                case "tly_runstate": this.CmdRunState(command, args); break;
                case "tly_catalog": this.CmdCatalog(command, args); break;
                case "tly_classify": this.CmdClassify(command, args); break;
                case "tly_testdonate": this.CmdTestDonate(command, args); break;
                case "tly_openhub": this.CmdOpenHub(command, args); break;
                case "tly_openshop": this.CmdOpenShop(command, args); break;
                case "tly_listupgrades": this.CmdListUpgrades(command, args); break;
                case "tly_buyupgrade": this.CmdBuyUpgrade(command, args); break;
                case "tly_payvault": this.CmdPayVault(command, args); break;
                case "tly_here": this.CmdHere(command, args); break;
                case "tly_opencookbook":  this.CmdOpenCookbook(command, args); break;
                case "tly_opencraftbook": this.CmdOpenCraftbook(command, args); break;
                case "tly_activeeffects": this.CmdActiveEffects(command, args); break;
                case "tly_setstash":  this.CmdSetStash(command, args); break;
                case "tly_openstash": this.CmdOpenStash(command, args); break;
                case "tly_stashclear": this.CmdStashClear(command, args); break;
                case "tly_wipemeta":   this.CmdWipeMeta(command, args); break;
                case "tly_replayintro": this.CmdReplayIntro(command, args); break;
                default:
                    this.Monitor.Log($"Debug bridge: unknown command '{command}'.", LogLevel.Warn);
                    break;
            }
        }

        private void CmdSelect(string command, string[] args)
        {
            if (!Context.IsWorldReady) { this.Monitor.Log("Load a save first.", LogLevel.Warn); return; }
            if (args.Length < 1) { this.Monitor.Log("Usage: tly_select <theme>", LogLevel.Warn); return; }
            // skipOfferCheck: this is a debug/playtest command — let it force any theme, not just
            // the seeded pair. The SelectedThemesThisMonth dedupe inside Select still applies.
            _runController.SelectByName(args[0], skipOfferCheck: true);
        }

        private void CmdOffer(string command, string[] args)
        {
            if (!Context.IsWorldReady) { this.Monitor.Log("Load a save first.", LogLevel.Warn); return; }
            _runController.PresentOffer();
        }

        private void CmdDonate(string command, string[] args)
        {
            if (!Context.IsWorldReady) { this.Monitor.Log("Load a save first.", LogLevel.Warn); return; }
            if (args.Length < 1) { this.Monitor.Log("Usage: tly_donate <itemId>", LogLevel.Warn); return; }
            _runController.Donate(args[0]);
        }

        private void CmdRunState(string command, string[] args)
        {
            if (!Context.IsWorldReady) { this.Monitor.Log("Load a save first.", LogLevel.Warn); return; }
            _runController.PrintRunState();
        }

        private void CmdCatalog(string command, string[] args)
        {
            if (!Context.IsWorldReady) { this.Monitor.Log("Load a save first.", LogLevel.Warn); return; }

            var byTheme = new Dictionary<TheLongestYear.Core.Theme, int>();
            foreach (CcItem item in _catalog)
                byTheme[item.Theme] = byTheme.TryGetValue(item.Theme, out int n) ? n + 1 : 1;

            this.Monitor.Log($"CC catalog: {_catalog.Count} items.", LogLevel.Info);
            foreach (var kvp in byTheme)
                this.Monitor.Log($"  {kvp.Key}: {kvp.Value}", LogLevel.Info);
        }

        /// <summary>Re-run the bundle catalog + requirement classification over whatever is in
        /// <c>Game1.netWorldState.Value.BundleData</c> RIGHT NOW and log the usual summary lines.
        /// Results go into locals only — the active run's catalog/requirements are untouched, so
        /// this is safe on a live save. Exists so an unattended session can verify remixed-bundle
        /// classification: 'debug ShuffleBundles' regenerates the bundles as Remixed in memory
        /// (never persisted unless the game saves), then this command classifies them.</summary>
        private void CmdClassify(string command, string[] args)
        {
            if (!Context.IsWorldReady) { this.Monitor.Log("Load a save first.", LogLevel.Warn); return; }

            var builder = new BundleCatalogBuilder(
                _config.RarityThresholds, _seasonResolver, this.Monitor,
                ParseThemeOverrides(),
                ParseItemSeasonPins(),
                ParseBundleQuotas());
            IReadOnlyList<CcItem> catalog = builder.Build();
            IReadOnlyList<BundleRequirement> requirements = builder.BuildRequirements();
            this.Monitor.Log($"tly_classify: {catalog.Count} catalog items, {requirements.Count} requirements (diagnostics only — active run unchanged).", LogLevel.Info);
        }

        private void CmdTestDonate(string command, string[] args)
        {
            if (!Context.IsWorldReady) { this.Monitor.Log("Load a save first.", LogLevel.Warn); return; }
            if (args.Length < 1) { this.Monitor.Log("Usage: tly_testdonate <qualifiedId> [count]", LogLevel.Warn); return; }

            int count = args.Length > 1 && int.TryParse(args[1], out int c) ? c : 1;
            DonationService.Active?.OnItemDonated(args[0], count);
        }

        private void CmdOpenHub(string command, string[] args)
        {
            if (!Context.IsWorldReady) { this.Monitor.Log("Load a save first.", LogLevel.Warn); return; }
            _launcher?.OpenWeeklyHub();
        }

        private void CmdOpenShop(string command, string[] args)
        {
            if (!Context.IsWorldReady) { this.Monitor.Log("Load a save first.", LogLevel.Warn); return; }
            _launcher?.OpenShrineShop();
        }

        private void CmdListUpgrades(string command, string[] args)
        {
            this.Monitor.Log($"Upgrade catalog: {UpgradeCatalog.All.Count} entries.", LogLevel.Info);
            foreach (UpgradeCategory cat in Enum.GetValues(typeof(UpgradeCategory)))
            {
                var rows = UpgradeCatalog.ByCategory(cat);
                this.Monitor.Log($"  {cat} ({rows.Count}):", LogLevel.Info);
                foreach (var u in rows)
                {
                    string owned = _meta != null && _meta.State.HasUpgrade(u.Id) ? " [OWNED]" : "";
                    string prereq = u.PrerequisiteId != null ? $" (req {u.PrerequisiteId})" : "";
                    this.Monitor.Log($"    - {u.Id}: {u.DisplayName} — {u.Cost} JP{prereq}{owned}", LogLevel.Info);
                }
            }
        }

        private void CmdBuyUpgrade(string command, string[] args)
        {
            if (!Context.IsWorldReady) { this.Monitor.Log("Load a save first.", LogLevel.Warn); return; }
            if (args.Length < 1) { this.Monitor.Log("Usage: tly_buyupgrade <id>", LogLevel.Warn); return; }
            _purchases?.TryPurchase(args[0]);
        }

        private void CmdPayVault(string command, string[] args)
        {
            if (!Context.IsWorldReady) { this.Monitor.Log("Load a save first.", LogLevel.Warn); return; }
            if (args.Length < 1)
            {
                this.Monitor.Log("Usage: tly_payvault <Spring|Summer|Fall|Winter|index>", LogLevel.Warn);
                return;
            }

            int bundleIndex;
            if (int.TryParse(args[0], out bundleIndex))
            {
                // direct index
            }
            else if (System.Enum.TryParse(args[0], ignoreCase: true, out TheLongestYear.Core.Season s))
            {
                // Resolve against THIS save's actual vault indices (remix-aware), not the vanilla 34–37.
                bundleIndex = TheLongestYear.Integration.VaultBundleMap.IndexForSeason(s);
                if (bundleIndex < 0)
                {
                    this.Monitor.Log("No vault bundle data available for that season on this save.", LogLevel.Warn);
                    return;
                }
            }
            else
            {
                this.Monitor.Log($"Unknown argument '{args[0]}'.", LogLevel.Warn);
                return;
            }

            if (!_meta.Run.VaultBundlesPaid.Contains(bundleIndex))
                _meta.Run.VaultBundlesPaid.Add(bundleIndex);
            this.Monitor.Log(
                $"Vault bundle {bundleIndex} marked paid. Paid this run: [{string.Join(", ", _meta.Run.VaultBundlesPaid)}]",
                LogLevel.Info);
        }

        /// <summary>Decide where this save's bundle requirement manifest comes from (owned-bundle
        /// engine wiring, Task 6). Three cases, in order:
        /// <list type="number">
        ///   <item>Engine mode -- <c>BundlesGeneratedForReset == CompletedResets</c>: the live
        ///   save's bundles were engine-written for THIS exact loop. Regenerate the manifest
        ///   deterministically from the seed (Generate() is pure given (UniqueMultiplayerID,
        ///   CompletedResets), so this reproduces the same set WriteToWorld wrote without a
        ///   second write) and defensively verify every generated key still exists in the live
        ///   BundleData before trusting it -- a mismatch (mod list changed, save edited by hand,
        ///   etc.) logs a WARN and falls through to case 3 instead of silently serving a manifest
        ///   that disagrees with what's actually on the CC board.</item>
        ///   <item>Fresh engine-era run-create -- no prior reset (<c>CompletedResets == 0</c>), no
        ///   legacy marker (<c>BundlesGeneratedForReset == -1</c>), and the CC is untouched (no
        ///   completed slot -- see <see cref="AnyBundleSlotComplete"/>): a brand-new save the
        ///   engine gets to author from day 1. Generates, WRITES the bundles into the world (the
        ///   only branch here that does), and stamps the marker.</item>
        ///   <item>Legacy -- everything else: an in-flight pre-engine loop finishing out on its
        ///   existing bundles. Read-and-classify off live BundleData, unchanged from before this
        ///   task.</item>
        /// </list>
        /// The CcItem catalog (<paramref name="builder"/>.Build(), called by the caller) always
        /// reads live BundleData regardless of which case fires here -- it reflects whatever
        /// bundles are actually live, engine-written or not.</summary>
        private IReadOnlyList<BundleRequirement> ResolveRequirements(
            BundleCatalogBuilder builder,
            System.Collections.Generic.IReadOnlyDictionary<string, TheLongestYear.Core.Season> itemSeasonPins,
            System.Collections.Generic.IReadOnlyDictionary<string, int[]> bundleQuotas)
        {
            MetaState state = _meta.State;
            // See WorldResetService.PerformReset step 11a for why this is the seed basis (not
            // Game1.uniqueIDForThisGame, which our own reset re-seeds every loop and is time-based
            // to begin with) -- it must match exactly what generated whatever is currently live.
            ulong seedBasis = unchecked((ulong)Game1.player.UniqueMultiplayerID);

            if (state.BundlesGeneratedForReset == state.CompletedResets)
            {
                var engine = new TheLongestYear.Loop.BundleEngine(this.Monitor);
                GeneratedBundleSet set = engine.Generate(BundleEngineSeed.For(seedBasis, state.CompletedResets));

                Dictionary<string, string> liveData = Game1.netWorldState.Value.BundleData;
                bool mismatch = set.Bundles.Any(spec => !liveData.ContainsKey(BundleDataWriter.Key(spec)));
                if (!mismatch)
                    return set.BuildRequirements(itemSeasonPins, bundleQuotas);

                this.Monitor.Log(
                    "ResolveRequirements: engine manifest mismatch against live BundleData — " +
                    "falling back to read path.",
                    LogLevel.Warn);
                // fall through to the legacy read-and-classify path below.
            }
            else if (state.CompletedResets == 0 && state.BundlesGeneratedForReset == -1 && !AnyBundleSlotComplete())
            {
                var engine = new TheLongestYear.Loop.BundleEngine(this.Monitor);
                GeneratedBundleSet set = engine.Generate(BundleEngineSeed.For(seedBasis, 0));
                engine.WriteToWorld(set, this.Monitor);
                state.BundlesGeneratedForReset = 0;
                return set.BuildRequirements(itemSeasonPins, bundleQuotas);
            }

            return builder.BuildRequirements();
        }

        /// <summary>True when any CC bundle completion slot is already marked complete. Same
        /// FieldDict-scan idiom as WorldResetService.PerformReset step 1a's defensive wipe, used
        /// read-only here to detect whether a fresh save's Community Center has been touched yet
        /// (gates the run-create branch of <see cref="ResolveRequirements"/>).</summary>
        private static bool AnyBundleSlotComplete()
        {
            foreach (KeyValuePair<int, Netcode.NetArray<bool, Netcode.NetBool>> kvp
                     in Game1.netWorldState.Value.Bundles.FieldDict)
            {
                Netcode.NetArray<bool, Netcode.NetBool> arr = kvp.Value;
                for (int i = 0; i < arr.Length; i++)
                    if (arr[i])
                        return true;
            }
            return false;
        }

        /// <summary>Merge GameplayConfig.DefaultItemSeasonPins + user ItemSeasonPins. User wins on conflict.
        /// Invalid season strings in user config are logged and skipped.</summary>
        private System.Collections.Generic.IReadOnlyDictionary<string, TheLongestYear.Core.Season> ParseItemSeasonPins()
        {
            var merged = new System.Collections.Generic.Dictionary<string, TheLongestYear.Core.Season>();

            foreach (var kv in TheLongestYear.Core.GameplayConfig.DefaultItemSeasonPins)
                if (System.Enum.TryParse(kv.Value, ignoreCase: true, out TheLongestYear.Core.Season s))
                    merged[kv.Key] = s;

            if (_config?.ItemSeasonPins != null)
            {
                foreach (var kv in _config.ItemSeasonPins)
                {
                    if (System.Enum.TryParse(kv.Value, ignoreCase: true, out TheLongestYear.Core.Season s))
                        merged[kv.Key] = s;
                    else
                        this.Monitor.Log(
                            $"ItemSeasonPins: '{kv.Value}' is not a valid season for id '{kv.Key}' — ignoring.",
                            LogLevel.Warn);
                }
            }

            return merged;
        }

        /// <summary>Merge GameplayConfig.DefaultBundleQuotas + user BundleQuotas. User wins on conflict.
        /// Malformed user arrays (wrong length, negative values) are logged and skipped.</summary>
        private System.Collections.Generic.IReadOnlyDictionary<string, int[]> ParseBundleQuotas()
        {
            var merged = new System.Collections.Generic.Dictionary<string, int[]>();

            foreach (var kv in TheLongestYear.Core.GameplayConfig.DefaultBundleQuotas)
                merged[kv.Key] = (int[])kv.Value.Clone();

            if (_config?.BundleQuotas != null)
            {
                foreach (var kv in _config.BundleQuotas)
                {
                    if (kv.Value == null || kv.Value.Length != TheLongestYear.Core.Calendar.MonthsPerYear)
                    {
                        this.Monitor.Log(
                            $"BundleQuotas: '{kv.Key}' needs a 4-int cumulative array; got length " +
                            $"{kv.Value?.Length ?? 0} — ignoring.",
                            LogLevel.Warn);
                        continue;
                    }
                    merged[kv.Key] = (int[])kv.Value.Clone();
                }
            }

            return merged;
        }

        /// <summary>Merge GameplayConfig.DefaultThemeOverrides + user ThemeOverrides for the catalog builder.</summary>
        private System.Collections.Generic.IReadOnlyDictionary<string, TheLongestYear.Core.Theme> ParseThemeOverrides()
        {
            var merged = new System.Collections.Generic.Dictionary<string, TheLongestYear.Core.Theme>();

            foreach (var kv in TheLongestYear.Core.GameplayConfig.DefaultThemeOverrides)
                if (System.Enum.TryParse(kv.Value, ignoreCase: true, out TheLongestYear.Core.Theme t))
                    merged[kv.Key] = t;

            if (_config?.ThemeOverrides != null)
            {
                foreach (var kv in _config.ThemeOverrides)
                {
                    if (System.Enum.TryParse(kv.Value, ignoreCase: true, out TheLongestYear.Core.Theme t))
                        merged[kv.Key] = t;
                    else
                        this.Monitor.Log(
                            $"ThemeOverrides: '{kv.Value}' is not a valid theme for id '{kv.Key}' — ignoring.",
                            LogLevel.Warn);
                }
            }

            return merged;
        }
    }
}
