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
    public sealed partial class ModEntry : Mod
    {
        private GameplayConfig _config;
        private MetaStore _meta;
        private CommunityCenterUnlock _ccUnlock;
        private MountainUnlock _mountainUnlock;
        private CharacterCreationWatcher _characterCreationWatcher;
        private WorldResetService _reset;
        private RunController _runController;
        private UpgradePurchaseService _purchases;
        private BoostPurchaseService _boostPurchases;
        private TheLongestYear.Loop.BoostEffectsService _boostEffects;
        private TheLongestYear.Loop.WitnessDialogueService _witness;
        private TheLongestYear.Loop.SabotageService _sabotage;
        private TheLongestYear.Loop.CircleOfWardingService _circles;
        /// <summary>Debug only: plays one overnight strike scene mid-day (tly_sabotage scene crows).</summary>
        private TheLongestYear.Scenes.ScenePreview _scenePreview;

        /// <summary>True once <c>tly_newgame</c> has made a farm in this session, so the save now
        /// loaded is a throwaway. <c>tly_sabotage fixture</c> rewrites the ground it stands on and
        /// will not do that to a save it did not create.</summary>
        private bool _saveMadeThisSession;
        private MenuLauncher _launcher;
        private SeasonResolver _seasonResolver;
        private IReadOnlyList<CcItem> _catalog = new List<CcItem>();
        private IReadOnlyList<BundleRequirement> _requirements = new List<BundleRequirement>();
        // Vanilla-mode board tracking: a bundle mod (Challenging CC Bundles) can rewrite BundleData
        // values on DayStarted; when the fingerprint moves we re-classify from the live data.
        private string _boardFingerprint;
        private BundleCatalogBuilder _boardBuilder;
        /// <summary>Derived per-item availability (earliest season + effort), built from the live
        /// engine pools once a save is loaded and handed to every path that classifies bundles so
        /// PerItem due dates come from the model instead of the 40-entry curated pin table.
        /// Null before a save is loaded, so every reader must tolerate null.</summary>
        private TheLongestYear.Core.ItemAvailabilityModel _availability;
        /// <summary>The effort tables (Phase 2 of the model) and the pools they were built with,
        /// kept for tly_dumpeffort and tly_itemmodel. Null before a save is loaded.</summary>
        private TheLongestYear.Core.Availability.EffortData _effortData;
        private TheLongestYear.Core.ItemPools _enginePools;
        /// <summary>Phase 1 item obtainability model (spec 2026-09-14): built blind from game data at
        /// save load. Nothing reads it for gameplay; tly_obtain is its only reader. Null before a save
        /// is loaded or when the build failed.</summary>
        private TheLongestYear.Core.Obtainability.ObtainabilityModel _obtainability;
        /// <summary>Session cache: a save load whose game data is unchanged (the reload after a loop
        /// reset) reuses the last build instead of paying for it again.</summary>
        private readonly TheLongestYear.Core.Obtainability.ObtainabilityCache _obtainabilityCache = new();
        /// <summary>The year 1 / year 2 sort's own model: the same data without the cart's random
        /// stock (YearTwoItems.WithoutCartRandomStock). Null when the build failed.</summary>
        private TheLongestYear.Core.Obtainability.ObtainabilityModel _yearSortModel;
        private readonly TheLongestYear.Core.Obtainability.ObtainabilityCache _yearSortCache = new();
        /// <summary>Sources the obtainability build could not read (unsupported item queries, machine
        /// output methods), for tly_obtain compare.</summary>
        private IReadOnlyList<string> _obtainabilityUnresolved = System.Array.Empty<string>();
        /// <summary>The message from an <see cref="TheLongestYear.Core.Obtainability.ObtainabilityReadException"/>
        /// when the last build refused to publish a model (a data section failed to read). Null when
        /// the model built cleanly or has not been built yet.</summary>
        private string _obtainabilityFailure;
        /// <summary>The inputs the last obtainability build read from the live game data, for
        /// tly_lexicon dumpinputs. Null until a build has read them.</summary>
        private TheLongestYear.Core.Obtainability.ObtainabilityInputs _lastObtainabilityInputs;
        /// <summary>The path lexicon (shipped answers, disk cache, build), made at the first save load.</summary>
        private TheLongestYear.Loop.LexiconService _lexicon;
        /// <summary>The obtainability build the lexicon ran during the last load, and whether the
        /// session memo reused it; null when the answers came from the shipped file or the cache.</summary>
        private TheLongestYear.Core.Obtainability.ObtainabilityBuild _lastLexiconBuild;
        private bool _lastLexiconBuildReused;
        /// <summary>The last obtainability build's stage times (read, build, year sort), for the load timing line.</summary>
        private long _obtainabilityReadMs, _obtainabilityBuildMs, _yearSortMs;
        /// <summary>The reachability verdicts <see cref="_enginePools"/> were built from, for tly_dumpmodel.</summary>
        private TheLongestYear.Core.Availability.SourceReachability _engineReachability;
        /// <summary>The curated season pins the availability model was last built with, kept so a
        /// difficulty-driven rebuild (<see cref="BuildAvailabilityModelFor"/>) does not need to
        /// re-parse config.json. Null before a save is loaded.</summary>
        private System.Collections.Generic.IReadOnlyDictionary<string, TheLongestYear.Core.Season> _itemSeasonPins;
        private DonationObserver _donationObserver;
        private CartStallIntro _cartStallIntro;
        private CaveChoicePrompt _caveChoicePrompt;
        private PeakMineFloorTracker _peakMineFloorTracker;
        private JunimoStashService _stashService;
        private WeeklyThemeQuestService _questService;
        private TheLongestYear.Loop.WildcardDayService _wildcardDays;
        private IntroEventInjector _introInjector;
        private OpeningStringsEditor _openingStrings;
        private OpeningEventInjector _openingEvent;
        private IntroSequenceDriver _introDriver;
        private Day28CutsceneDriver _day28Driver;
        private Integration.SeasonTurnDriver _seasonTurnDriver;
        private EndingEventDriver _endingDriver;
        private BookFurniture _bookFurniture;
        private UI.PlanningShrineService _planningShrine;
        private TheLongestYear.Loop.OnboardingMailService _onboardingMail;
        private TheLongestYear.Loop.JojaLetterService _jojaLetters;
        private Integration.JojaOfferDriver _jojaOffer;
        private TheLongestYear.Loop.PierreYear2SeedsService _pierreSeeds;
        private Integration.MorrisDarkSprite _morrisDark;
        private Integration.MorrisDarkPortrait _morrisDarkPortrait;
        private Integration.JunimoPortrait _junimoPortrait;
        private TheLongestYear.Loop.PastSeasonSpawnsService _pastSeasonSpawns;
        private TheLongestYear.Loop.AnimalPowersService _animalPowers;
        private TheLongestYear.Loop.SneakPeekChannelService _sneakPeekChannel;

        /// <summary>Whether the Sneak Peek Boost was active at the last cache check. The Wednesday
        /// channel label is an asset edit, so it has to be invalidated when the Boost starts (the
        /// purchase path does that directly) and when it expires at the season roll (this does).</summary>
        private bool _sneakPeekLabelActive;

        // Debug command-file bridge: lets the developer trigger tly_ actions by writing lines into a file
        // in the mod folder, so PC in-game testing needs no console typing (the mod polls + executes them).
        private const string DebugCommandFileName = "tly_commands.txt";
        private const int DebugPollTicks = 30;
        private string _commandFilePath;

        /// <summary>Season-start ledger snapshot that <c>tly_playseason quarter &lt;k&gt;</c> plans against, so
        /// the four quarter calls share one plan and land cumulatively where a plain call would.</summary>
        private (TheLongestYear.Core.Season Season, List<DonatedSlot> Donated)? _playSeasonBaseline;

        /// <summary>Slots the quarter calls have actually flipped since the baseline was taken, so the
        /// log can report the real running total for the season and each quarter can budget the steps
        /// it still owes (the plan position alone counts steps, not donations).</summary>
        private int _playSeasonDonatedThisSeason;

        // True only once OnSaveLoaded has actually called _meta.Load() for the current save. Guards
        // OnSaving: when a save opens with TLY disabled we skip Load (the
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
            // Vanilla item display names for catalog rows that use the item: token (Keep <book>).
            TheLongestYear.Core.Strings.InitItemNames(id => ItemRegistry.GetDataOrErrorItem(id).DisplayName);

            _config = this.ReadConfigWithBackup(helper);
            _config.Randomizer ??= new RandomizerSettings();
            CartSlotLimitPatch.Enabled = _config.LimitTravelingCartStock;
            TheLongestYear.Loop.FestivalTimeFlow.Enabled = _config.FestivalTimeFlows;
            TheLongestYear.Loop.FestivalMainEventOncePatch.Enabled = _config.FestivalMainEventOncePerDay;

            // One-shot config migration.
            bool stashTileMigrated = false;
            // 2026-05-28 second-pass migration for the stash tile:
            // The first migration set (72,12) as a hardcoded default, but the 2026-05-27 playtest
            // showed that tile is invisible on the Standard farm (under the farmhouse roof on
            // the user's save). Reset to (0,0) so JunimoStashService.PlaceChest auto-picks
            // relative to the FarmHouse entry instead.
            if (_config.StashTileX == 72 && _config.StashTileY == 12)
            {
                _config.StashTileX = 0; _config.StashTileY = 0; stashTileMigrated = true;
            }
            if (RandomizerMigration.Apply(_config))
            {
                this.Monitor.Log("Migrated config.json: theme reroll switch moved to Randomizer > Rerolls = Free.", LogLevel.Info);
            }
            if (stashTileMigrated)
                this.Monitor.Log("Migrated config.json: applied new default tile coords.", LogLevel.Info);
            // Darkness dial (spec 2026-09-15 Part B, 2.3): a config from before the dial sets it,
            // and the overall lever, to the lowest of the nine existing dials (Jeff, 2026-09-14).
            // The && is the quiet path for a FRESH config: every dial Normal migrates to Normal, which
            // changes nothing the player would recognise, so it says nothing.
            if (_config.Difficulty.MigrateDarkness() && !_config.Difficulty.IsAllNormal())
            {
                this.Monitor.Log($"Migrated config.json: Darkness dial set to {_config.Difficulty.Darkness} (the lowest existing dial).", LogLevel.Info);
            }

            // Always write the config back on Entry so any newly-added fields (Enabled, new
            // tile defaults, future tuning knobs) become visible in config.json for the
            // player to edit. Existing customizations were already deserialized into _config
            // and are preserved by the write. SMAPI's WriteConfig is idempotent for
            // unchanged values.
            this.WriteConfigWithBackup();

            _meta = new MetaStore(helper.Data);
            GrandpaCandleCommand.Register(this.Monitor);
            Integration.EndingEventCommands.Register(this.Monitor, helper);
            Integration.OpeningEventCommands.Register(this.Monitor, helper);
            Integration.JojaBadEndingCommands.Register(this.Monitor, helper);
            // The stash's Junimo-chest sprite. Wired at Entry, not on save load: the opening tour
            // places the stash before a new game's save exists, and without the loader the draw
            // patch fell back to a plain purple-tinted chest (Jeff, 2026-09-25).
            JunimoStashService.SetTextureLoader(
                () => this.Helper.ModContent.Load<Microsoft.Xna.Framework.Graphics.Texture2D>("assets/junimo_stash.png"));
            // Rewind cutscene (spec 2026-09-11-rewind-cutscene, task 7): REQUIRED WIRING for all
            // scenes the day-28 FAIL branch chains together. Without these, the menu-steal teardown
            // shared by RewindBedroomScene and RewindMorningScene is dead code, RewindPanScene's own
            // UpdateTicked never subscribes (the pan would start and then never advance), and
            // RewindSpringPaint's per-tick hold never subscribes either (the paint would apply once
            // and immediately go stale, and never release).
            UI.RewindJunimoScene.Register(this.Monitor, helper);
            Integration.RewindPanScene.Register(this.Monitor, helper, _meta, _config);
            Integration.RewindSkip.Register(this.Monitor, helper, _meta);
            Integration.RewindSpringPaint.Register(this.Monitor, helper);
            Integration.RewindNightPaint.Register(this.Monitor, helper);
            Integration.RewindNightLight.Register(this.Monitor, helper);
            Integration.RewindBlackout.Register(this.Monitor, helper);
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
            _seasonTurnDriver = new Integration.SeasonTurnDriver(this.Monitor, _meta);
            _seasonTurnDriver.Attach(helper);
            _day28Driver = new Day28CutsceneDriver(this.Monitor);
            _day28Driver.Attach(helper, () => _runController, () => _seasonTurnDriver);
            // Year One Ending: starts the ending event on the first step outside on an armed
            // morning, waits for the seen mail, then hands off to RunController. _runController
            // isn't built until OnSaveLoaded, so resolve it lazily like the driver above.
            _endingDriver = new EndingEventDriver(this.Monitor, _meta, _config);
            _endingDriver.Attach(helper, () => _runController);
            // Skip the overnight FarmEvent on nights whose morning rewinds (Fail or a voluntary restart): its end-of-event warp orphans the Fail
            // scene and drops the reset (see FarmEventSuppressionPatch). _runController is built on
            // save load, so resolve it lazily like the driver does.
            FarmEventSuppressionPatch.SuppressTonight =
                () => TheLongestYear.Core.Day28.VoluntaryRestart.IsRewind(
                    _runController?.PendingCutscene ?? TheLongestYear.Core.Day28.Day28Branch.None);
            FarmEventSuppressionPatch.Monitor = this.Monitor;
            // The overnight slot (spec 2026-09-21): tonight's strike scene takes it from a random
            // vanilla event, and anything scripted keeps it while the strike is postponed, effect
            // and scene both (Jeff, 2026-10-07). _sabotage is built on save load, so resolve it
            // lazily the way the driver above does.
            StrikeScenePatch.Monitor = this.Monitor;
            StrikeScenePatch.FailNight = () => FarmEventSuppressionPatch.SuppressTonight?.Invoke() == true;
            StrikeScenePatch.Postpone = why => _sabotage?.PostponePendingIfAny(why, TheLongestYear.Core.Sabotage.PostponeCause.SlotTaken);
            StrikeScenePatch.StrikeWaiting = () => _sabotage?.Pending is { Applied: false };
            StrikeScenePatch.SceneFor = () =>
            {
                PendingStrike strike = _sabotage?.Pending;
                if (strike == null || strike.Applied) return null;
                bool skippable = TheLongestYear.Core.Sabotage.StrikeScenes.IsSkippable(
                    strike.Event, _meta.State.StrikeScenesSeen ??= new());
                var scene = TheLongestYear.Scenes.StrikeSceneFactory.Create(
                    strike, skippable, this.Monitor,
                    onFinished: shown =>
                    {
                        if (!shown) return;
                        TheLongestYear.Core.Sabotage.StrikeScenes.MarkPlayed(strike.Event, _meta.Run, _meta.State);
                        // Shown covers finished and skipped; a scene that could not be staged
                        // returned above (its strike landed bare), so nobody saw anything, no
                        // witness is recorded and the scene stays due.
                        _witness?.OnScenePlayed(strike.Event);
                    });
                // The postfix hands this scene the slot straight after. If its setUp then never
                // runs, another mod replaced it: a conflict, queued by the net (Jeff, 2026-10-08).
                if (scene != null) strike.OnHandedSlot();
                return scene;
            };
            TheLongestYear.Loop.WildcardNightEventPatch.Monitor = this.Monitor;
            WeatherScheduleWriterPatch.Monitor = this.Monitor;
            // Placeable book furniture (Cookbook/Craftbook/Bundle-log) — registers via asset edit.
            _bookFurniture = new BookFurniture(this.Monitor, helper);
            // View-only planning shrine — registers its furniture + auto-places near the stash.
            _planningShrine = new UI.PlanningShrineService(this.Monitor, helper);
            // The opening tour talks about the stash and the shrine, so they must exist before its
            // dialogue. SaveCreating fires too late for that (alongside SaveLoaded), so the script
            // places them itself: tlyPlaceGifts runs right after the tour's "changeLocation Farm".
            StardewValley.Event.RegisterCommand("tlyPlaceGifts", (evt, args, context) =>
            {
                try
                {
                    var openingStash = new JunimoStashService(this.Monitor, _meta.State, _config);
                    openingStash.PlaceChest();
                    _planningShrine.Place(openingStash.LastPlacedTile);
                    this.Monitor.Log("Opening: placed the stash and shrine for the tour.", LogLevel.Info);
                }
                catch (Exception ex)
                {
                    this.Monitor.Log(
                        $"tlyPlaceGifts: could not place the stash/shrine ({ex.GetType().Name}: {ex.Message}); OnSaveLoaded will place them after the tour instead.",
                        LogLevel.Warn);
                }
                evt.CurrentCommand++;
            });
            // First-loop Spring-1 onboarding letter. Constructed at Entry so AssetRequested is
            // hooked before the first asset load (same reason as _introInjector above).
            _onboardingMail = new TheLongestYear.Loop.OnboardingMailService(this.Monitor, _meta);
            helper.Events.Content.AssetRequested += _onboardingMail.OnAssetRequested;
            // Morris's letters (spec 2026-09-25-joja-offer-design): "come see me" then "make a decision" mail.
            _jojaLetters = new TheLongestYear.Loop.JojaLetterService(this.Monitor, _meta);
            helper.Events.Content.AssetRequested += _jojaLetters.OnAssetRequested;
            // Morris's offer scene on the first visit to JojaMart each loop (same spec).
            _jojaOffer = new Integration.JojaOfferDriver(this.Monitor, _meta);
            _jojaOffer.Attach(helper);
            // The opening's deathbed and letter text (spec 2026-09-16). Hooked at Entry so the very first
            // Strings/StringsFromCSFiles load already carries it; the minigame reads it before any save exists.
            _openingStrings = new OpeningStringsEditor(this.Monitor, () => _config.Enabled);
            helper.Events.Content.AssetRequested += _openingStrings.OnAssetRequested;
            _openingEvent = new OpeningEventInjector(this.Monitor, () => _config.Enabled);
            helper.Events.Content.AssetRequested += _openingEvent.OnAssetRequested;
            // Circle of Warding furniture (Data/Furniture row + texture), granted per loop below.
            _circles = new TheLongestYear.Loop.CircleOfWardingService(this.Monitor, _meta, helper);
            // pierre_year2_seeds: Data/Shops edit gated on ownership (UpgradeChecker, per save).
            _pierreSeeds = new TheLongestYear.Loop.PierreYear2SeedsService(this.Monitor);
            helper.Events.Content.AssetRequested += _pierreSeeds.OnAssetRequested;
            // Year One Ending scene 4: changeSprite Morris Dark loads Characters/Morris_Dark, built
            // here at load time with the irises recoloured red (spec 2026-09-06 §4).
            _morrisDark = new Integration.MorrisDarkSprite(this.Monitor);
            helper.Events.Content.AssetRequested += _morrisDark.OnAssetRequested;
            _morrisDarkPortrait = new Integration.MorrisDarkPortrait(this.Monitor);
            helper.Events.Content.AssetRequested += _morrisDarkPortrait.OnAssetRequested;
            // Junimo temporary actors speak in the ending and intro; with no Portraits/Junimo* asset the
            // dialogue box retried the failed portrait load every frame (live run 2026-09-06).
            _junimoPortrait = new Integration.JunimoPortrait(this.Monitor);
            helper.Events.Content.AssetRequested += _junimoPortrait.OnAssetRequested;
            // Spring/Summer/Fall Returns: Data/Locations copies of a past season's fish and forage.
            _pastSeasonSpawns = new TheLongestYear.Loop.PastSeasonSpawnsService(this.Monitor, helper);
            helper.Events.Content.AssetRequested += _pastSeasonSpawns.OnAssetRequested;
            // Animal powers (spec 2026-10-09): Morning Rounds at day start, Loyal Pet's Data/Pets edit.
            _animalPowers = new TheLongestYear.Loop.AnimalPowersService(this.Monitor, helper);
            helper.Events.Content.AssetRequested += _animalPowers.OnAssetRequested;
            helper.Events.GameLoop.DayStarted += _animalPowers.OnDayStarted;
            // Sneak Peek: relabel the Wednesday TV channel while the Boost has taken the rerun slot.
            _sneakPeekChannel = new TheLongestYear.Loop.SneakPeekChannelService(this.Monitor);
            helper.Events.Content.AssetRequested += _sneakPeekChannel.OnAssetRequested;
            // Two registrations, one runs: low priority when Tech's Cross-Mod Bundles is loaded
            // (TLY's board has to win over the one it writes at load), normal otherwise.
            helper.Events.GameLoop.SaveLoaded += this.OnSaveLoadedNormal;
            helper.Events.GameLoop.SaveLoaded += this.OnSaveLoadedLate;
            helper.Events.Player.Warped += this.OnWarpedForFarmArrivalScene;
            // Festival memories: the golden pumpkin is seen as it lands in the inventory (no chest patch).
            helper.Events.Player.InventoryChanged += TheLongestYear.Loop.FestivalMemoryRecorder.OnInventoryChanged;
            helper.Events.GameLoop.SaveCreating += this.OnSaveCreating;
            helper.Events.GameLoop.ReturnedToTitle += this.OnReturnedToTitle;
            helper.Events.GameLoop.Saving += this.OnSaving;
            // Settles the post-reset save (ResetSave) before the DayStarted SMAPI raises with Saved.
            helper.Events.GameLoop.Saving += (_, _) => TheLongestYear.Loop.ResetSave.OnSaving();
            helper.Events.GameLoop.Saved += (_, _) => TheLongestYear.Loop.ResetSave.OnSaved();
            helper.Events.Specialized.LoadStageChanged += TheLongestYear.Loop.ResetSave.OnLoadStageChanged;
            // A foreign object the save cannot write is set aside for the write instead of failing it.
            TheLongestYear.Loop.UnsaveableObjectGuard.Init(this.Monitor);
            helper.Events.GameLoop.Saving += TheLongestYear.Loop.UnsaveableObjectGuard.OnSaving;
            helper.Events.GameLoop.Saved += TheLongestYear.Loop.UnsaveableObjectGuard.OnSaved;
            helper.Events.GameLoop.ReturnedToTitle += TheLongestYear.Loop.UnsaveableObjectGuard.OnReturnedToTitle;
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
                TheLongestYear.UI.FocusCardStrip.ClearFaces();
            };

            // Watch the character-creation screen (Skip intro notice). Wired here (not in
            // OnSaveLoaded) because it fires on the title screen, before any save is loaded.
            _characterCreationWatcher = new CharacterCreationWatcher(this.Monitor, _config);
            // Same gate: the new-game "Community Center Bundles" dropdown becomes a single "TLY Custom"
            // entry while the mod is enabled (the engine owns the board; the vanilla choice is moot).
            BundleOptionPatch.Enabled = () => _config.Enabled;
            BundleOptionPatch.Monitor = this.Monitor;
            // "Skip intro" on character creation skips TLY's opening cutscene (vanilla's stays skipped).
            SkipIntroChoicePatch.Enabled = () => _config.Enabled;
            SkipIntroChoicePatch.Monitor = this.Monitor;
            BundleOptionPatch.ConfiguredSource = () => _config.BundleSource;
            _characterCreationWatcher.Attach(helper);

            // 2026-05-29 round 11: PatchAll iterates [HarmonyPatch] classes in assembly order,
            // and a SINGLE bad attribute (e.g. ambiguous method match) throws and aborts the
            // rest of the iteration — that's how the round-8 EventSuppressionPatch silently
            // killed every later patch including the bonus-drop and stash-capacity ones. Walk
            // the patch classes ourselves and isolate each one so a single failure logs +
            // continues instead of cratering the whole pass.
            var harmony = new Harmony(this.ModManifest.UniqueID);
            // Snow day animals: other mods' helpers the transpilers found get hooked once every
            // mod's Entry has patched (see WildcardAnimalPatch).
            TheLongestYear.Loop.WildcardAnimalPatch.Monitor = this.Monitor;
            TheLongestYear.Loop.GearReach.Monitor = this.Monitor;
            TheLongestYear.Integration.BundleLogSideTab.Monitor = this.Monitor;
            helper.Events.GameLoop.GameLaunched += (_, _) => TheLongestYear.Loop.WildcardAnimalPatch.HookOtherModHelpers(harmony);
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
            // The dark aura on tainted items: manual patch so a signature mismatch fails loudly.
            var taintedItems = new TheLongestYear.Core.Sabotage.TaintedItems();
            TheLongestYear.Loop.TaintedAuraPatch.Tainted = () =>
            {
                taintedItems.Refresh(_meta?.Run?.Tampers);
                return taintedItems;
            };
            TheLongestYear.Loop.TaintedAuraPatch.Apply(harmony);
            // The post-reset save's extra DayStarted (Farm Type Manager double spawns): manual, SMAPI-internal target.
            TheLongestYear.Loop.ResetSaveDayStartPatch.Apply(harmony, this.Monitor);
            // The same glow marks a bundle slot the darkness emptied, until it is filled again.
            TheLongestYear.Loop.DarkenedSlotMarkPatch.Marks = () => _meta?.Run?.DarkenedSlots;
            this.Monitor.Log(
                $"Harmony: {patched} patch class(es) applied, {failed} failed.",
                failed > 0 ? LogLevel.Warn : LogLevel.Info);

            // Wire the static monitor/config the weapon/hat donation patch cluster reads
            // (kill-switched via GameplayConfig.EnableNonObjectDonations + the RunActivation
            // gate — see BundleDonationPatches). _config is a single stable instance for the
            // whole session, so Connect only needs to run once here.
            TheLongestYear.Patches.BundleDonationPatches.Connect(this.Monitor, _config);
            TheLongestYear.Focus.FocusBuildSession.Monitor = this.Monitor;

            // Observation-based donation detector. See DonationObserver.cs for why we can't rely
            // on a Harmony patch of Bundle.tryToDepositThisItem alone (the 2026-05-26 playtest
            // showed it didn't fire on real CC deposits).
            _donationObserver = new DonationObserver(helper, this.Monitor);
            // One-time in-fiction explanation for the one-item Traveling Cart (Cart Stall cap).
            _cartStallIntro = new CartStallIntro(helper, this.Monitor, () => _meta?.State, () => _config);

            // Per-loop mushrooms-vs-bats re-choice on cave entry — replaces the replaying
            // Demetrius cutscene (event-hygiene pass; see CaveChoicePrompt).
            _caveChoicePrompt = new CaveChoicePrompt(helper, this.Monitor);

            // The Cookbook, Craftbook, and Bundle-log are placeable book furniture now
            // (see BookFurniture) — no tile-anchored interactables.

            _commandFilePath = Path.Combine(helper.DirectoryPath, DebugCommandFileName);

            RegisterDebugCommands(helper);

            this.Monitor.Log("The Longest Year loaded.", LogLevel.Info);
        }

        /// <summary>Merge the run's seen vanilla events into the cross-loop SeenEventsEver memory so a
        /// scene watched in any run stays suppressed on later loops (event-gating Phase 1). Called
        /// from OnSaving before the meta-state persists; FarmerReset re-seeds eventsSeen from it.
        /// <para>Current-run hand-off ids (<see cref="TheLongestYear.Core.Ending.PostCompletionEvents"/>)
        /// are skipped: the ending's Keep-playing branch writes the CC ceremony id into eventsSeen so
        /// vanilla flips its post-completion world for THIS year, and recording it here would carry the
        /// destroyed JojaMart / Pierre Wednesdays / storm cutscene into every later loop.</para></summary>
        private void RecordSeenEvents()
        {
            if (!Context.IsWorldReady || Game1.player?.eventsSeen == null)
                return;

            System.Collections.Generic.List<string> seen = _meta.State.SeenEventsEver;
            var known = new System.Collections.Generic.HashSet<string>(seen, System.StringComparer.Ordinal);
            int added = 0;
            foreach (string id in Game1.player.eventsSeen)
            {
                if (TheLongestYear.Core.Ending.PostCompletionEvents.IsHandedOffOnly(id)) continue;
                if (known.Add(id)) { seen.Add(id); added++; }
            }

            if (added > 0)
                this.Monitor.Log(
                    $"Recorded {added} newly-seen event id(s) to SeenEventsEver (total {seen.Count}).",
                    LogLevel.Trace);
        }

        /// <summary>Heal a save whose SeenEventsEver already banked a current-run hand-off id. Builds
        /// before the RecordSeenEvents skip existed banked the CC ceremony id the first time a player
        /// took "Keep playing", which would then be re-seeded into eventsSeen on every later reset.
        /// Called from OnSaveLoaded right after MetaStore.Load.</summary>
        private void PurgeHandedOffEvents()
        {
            System.Collections.Generic.List<string> seen = _meta.State.SeenEventsEver;
            if (seen == null) return;
            int removed = seen.RemoveAll(TheLongestYear.Core.Ending.PostCompletionEvents.IsHandedOffOnly);
            if (removed > 0)
                this.Monitor.Log(
                    $"Removed {removed} current-run hand-off event id(s) from SeenEventsEver " +
                    "(the CC ceremony flag must not survive a loop).",
                    LogLevel.Info);
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

        /// <summary>Today as a 1..112 day of year from the game's own date (the run calendar syncs
        /// later on cutscene mornings); the run calendar only before a save is loaded.</summary>
        /// <summary>Debug: dump a Characters/&lt;Name&gt; sprite sheet to test-output as a PNG so its
        /// pixel colours can be read with an image viewer (used to find Morris_Dark's iris colour).</summary>
        private void CmdDumpSprite(string command, string[] args)
        {
            if (args.Length < 1) { this.Monitor.Log("Usage: tly_dumpsprite <Name>", LogLevel.Warn); return; }
            try
            {
                var tex = Game1.content.Load<Microsoft.Xna.Framework.Graphics.Texture2D>("Characters/" + args[0]);
                string dir = System.IO.Path.Combine(this.Helper.DirectoryPath, "test-output");
                System.IO.Directory.CreateDirectory(dir);
                string path = System.IO.Path.Combine(dir, $"sprite-{args[0]}.png");
                using var fs = System.IO.File.Create(path);
                tex.SaveAsPng(fs, tex.Width, tex.Height);
                this.Monitor.Log($"Wrote {path} ({tex.Width}x{tex.Height}).", LogLevel.Info);
            }
            catch (Microsoft.Xna.Framework.Content.ContentLoadException ex)
            {
                this.Monitor.Log($"No sprite named {args[0]}: {ex.Message}", LogLevel.Warn);
            }
        }

        private void CmdHud(string command, string[] args)
        {
            string menu = Game1.activeClickableMenu?.GetType().Name ?? "none";
            this.Monitor.Log(
                $"displayHUD={Game1.displayHUD} eventUp={Game1.eventUp} freezeControls={Game1.freezeControls} " +
                $"fadeToBlack={Game1.fadeToBlack} fadeAlpha={Game1.fadeToBlackAlpha:0.00} " +
                $"menu={menu} time={Game1.timeOfDay} day={Game1.dayOfMonth}",
                LogLevel.Info);
        }

        private int TodayDayOfYear() => Context.IsWorldReady
            ? TheLongestYear.Core.Calendar.DayOfYear((int)Game1.season, Game1.dayOfMonth)
            : TheLongestYear.Core.Calendar.DayOfYear((int)_meta.Run.Season, _meta.Run.DayOfMonth);

        private void OnGameLaunched(object sender, StardewModdingAPI.Events.GameLaunchedEventArgs e)
        {
            // Cart Whisperer extends to every day when the standalone Cart Catalog mod is installed.
            TheLongestYear.Loop.CartCatalogIntegration.ModLoaded =
                this.Helper.ModRegistry.IsLoaded(TheLongestYear.Loop.CartCatalogIntegration.ModId);

            // The stash menu dodges Better Chests / Unlimited Storage only when one is installed,
            // so Chests Anywhere keeps working against it otherwise (see JunimoStashService).
            foreach (string modId in TheLongestYear.Loop.JunimoStashService.StorageOverhaulModIds)
            {
                if (this.Helper.ModRegistry.IsLoaded(modId))
                {
                    TheLongestYear.Loop.JunimoStashService.StorageOverhaulLoaded = true;
                    this.Monitor.Log($"Storage-overhaul mod {modId} detected: the Junimo stash menu opens with a null context and is hidden from Chests Anywhere.", LogLevel.Info);
                }
            }

            this.ApplyWindowSize();
            this.KeepRunningUnfocused("launch");
            this.RegisterGmcm();
        }

        // Low: runs after other mods' DayStarted, so a bundle mod that swaps its board in each
        // morning (Challenging CC Bundles) has done so before ReclassifyIfBoardChanged reads it.
        [EventPriority(EventPriority.Low)]
        private void OnDayStarted(object sender, StardewModdingAPI.Events.DayStartedEventArgs e)
        {
            if (!RunActivation.IsActive) return;
            TheLongestYear.Loop.AnimalSpeciesRecorder.Record(_meta.State, this.Monitor);
            ReclassifyIfBoardChanged();
            _onboardingMail?.OnDayStarted();
            // Before the run controller (it reads the game's date, see the method), but told about a
            // pending rewind so the old loop's last morning sends nothing, and about a repeat win's
            // choice so a won year's morning sends nothing either.
            _jojaLetters?.OnDayStarted(rewindPending: _runController?.IsRewindChainRunning == true,
                winPending: _runController?.IsRepeatWinChoicePending == true);
            _runController?.OnDayStarted(sender, e);
            // A basement focus card's Cellar, once the night's house upgrade has landed.
            _focus?.Delivery.OnDayStarted();
            // After the run controller: it syncs Run.Season/DayOfMonth to the new day, and the
            // boosts' "today" (expiry, lucky day, buffs) is read from the run's calendar.
            _boostEffects?.OnDayStarted();
            _witness?.OnDayStarted();
            // A darkened slot the player filled again loses its mark (designer, 2026-10-08).
            TheLongestYear.Core.Sabotage.DarkenedSlots.ClearFilled(_meta.Run?.DarkenedSlots, (bundle, slot) =>
            {
                bool[] state = TheLongestYear.Loop.RunController.SlotStateForBundle(bundle);
                return state != null && slot < state.Length && state[slot];
            });
            _pastSeasonSpawns?.Refresh(TodayDayOfYear());
            // Catches Sneak Peek expiring at the season roll: the Wednesday channel goes back to
            // being a rerun, so the label has to go back with it.
            this.RefreshSneakPeekChannelLabel();
        }

        /// <summary>Re-run the Wednesday channel-label edit if the Sneak Peek Boost has started or
        /// ended since the last check. Invalidating an asset forces a reload, so this only pays
        /// that cost on the two days a season when the answer actually changes.</summary>
        private void RefreshSneakPeekChannelLabel()
        {
            bool active = TheLongestYear.Loop.BoostChecker.SneakPeekActive?.Invoke() == true;
            if (active == _sneakPeekLabelActive) return;
            _sneakPeekLabelActive = active;
            this.Helper.GameContent.InvalidateCache(TheLongestYear.Loop.SneakPeekChannelService.StringsAssetName);
        }

        // High: the day-28 gate must read the board the player filled today. Challenging CC Bundles
        // swaps the morning board back out on DayEnding, and a gate running after that counted only
        // the slots the smaller board has (ozzy2540, 2026-09-25: 47 slots filled, 32 counted).
        [EventPriority(EventPriority.High)]
        private void OnDayEnding(object sender, StardewModdingAPI.Events.DayEndingEventArgs e)
        {
            // Today's morning boxes do not carry into tomorrow.
            TheLongestYear.Loop.MorningBox.Clear();
            if (!RunActivation.IsActive) return;
            // The rewind's ground drops are ordinary ground items from the first night on.
            TheLongestYear.Core.ResetGroundDrops.ForgetAtNight(_meta.State);
            _runController?.OnDayEnding(sender, e);
            // After the run controller (a rewind clears the target): steps a focus-card basement up
            // to the level below it, so vanilla's own overnight upgrade lands the last one.
            _focus?.Delivery.OnDayEnding();
            // Vanilla spawns tomorrow's forage overnight, before DayStarted: prepare for tomorrow now.
            _pastSeasonSpawns?.Refresh(TodayDayOfYear() + 1);
        }

        /// <summary>
        /// Poll the debug command file (mod folder) and execute any queued lines once. Lets the developer
        /// drive tly_ actions by writing the file while the player only plays — no in-game console needed.
        /// </summary>
        private void OnUpdateTicked(object sender, UpdateTickedEventArgs e)
        {
            // A focus building placement settles the tick its screen closes (placed or backed out).
            TheLongestYear.Focus.FocusBuildSession.Tick();

            if (Context.IsWorldReady)
            {
                // Dormant on non-TLY saves: no deferred-offer retry, no festival auto-eject, no debug
                // bridge (the bridge must never run commands against a save the mod is dormant on).
                if (!RunActivation.IsActive)
                    return;

                // Open a post-reset save that waited for a clear screen, or settle one that
                // closed without a Saved event.
                TheLongestYear.Loop.ResetSave.Tick();

                // Re-attempt a planning-hub open that was deferred because the menu surface was busy
                // (the post-win keep-playing dialogue still closing when the new loop's reset fired).
                // Gate on a clear surface so the retry opens cleanly and doesn't re-log every tick.
                if (Game1.activeClickableMenu == null && !Game1.eventUp)
                    _runController?.TryDrainDeferredOffer();

                // Herd Book "waiting" HUD lines from the reset, held until the save and the planning
                // hub are gone so they don't expire unseen. Runs after the deferred offer so a hub
                // that opens this tick keeps them waiting.
                Loop.HerdBookService.ShowWaitingHud(this.Monitor);

                // The darkness's morning boxes, one at a time, held while anything holds the screen.
                TheLongestYear.Loop.MorningBox.Tick();

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

        /// <summary>Parse one "tly_command arg1 arg2" line and run it from the shared command table
        /// (<see cref="DebugCommandTable"/>), the same handlers the SMAPI console calls.</summary>
        private void ExecuteDebugLine(string line)
        {
            string[] parts = line.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            string command = parts[0].ToLowerInvariant();
            string[] args = parts.Skip(1).ToArray();

            if (_debugCommands != null && _debugCommands.TryGetValue(command, out Action<string, string[]> run))
                run(command, args);
            else
                this.Monitor.Log($"Debug bridge: unknown command '{command}'.", LogLevel.Warn);
        }

        /// <summary>Builds the derived item availability model from the live engine pools for one
        /// difficulty step, in the mode that step maps to (<see cref="TheLongestYear.Core.WeekModes"/>).
        /// Used both at SaveLoaded (the first build) and by <c>WorldResetService.RebuildAvailabilityModel</c>
        /// (a reset that changed the step).
        ///
        /// Updates <see cref="_availability"/> as a side effect AND pushes the new instance into the
        /// two holders that cached the old reference: <see cref="RunController.Availability"/> (set
        /// once at SaveLoaded) and the board builder's catalog Availability. Both hold the model by
        /// reference, so a reset that rebuilds it would otherwise leave them answering from the
        /// pre-reset model for the rest of the session.</summary>
        private TheLongestYear.Core.ItemAvailabilityModel BuildAvailabilityModelFor(TheLongestYear.Core.DifficultyStep step)
        {
            // Served from the path lexicon when it holds answers for this step and kitchen state made
            // from the same inputs (see AvailabilityAnswers.InputsHash); otherwise built and stored.
            TheLongestYear.Core.WeekMode mode = TheLongestYear.Core.WeekModes.For(step);
            bool kitchen = _meta.State.HasUpgrade("keep_kitchen");
            string key = TheLongestYear.Core.Lexicon.AvailabilityAnswers.Key(step, kitchen);
            var timer = System.Diagnostics.Stopwatch.StartNew();
            string inputs = AvailabilityInputsHash();
            long hashMs = timer.ElapsedMilliseconds;
            string storedInputs = _lexicon?.StoredInputs(key) ?? "none";
            TheLongestYear.Core.Lexicon.AvailabilityAnswers stored = inputs == null ? null : _lexicon?.AvailabilityFor(key, inputs);
            if (stored != null)
                _availability = TheLongestYear.Core.Availability.ItemAvailabilityBuilder.FromAnswers(
                    stored, _itemSeasonPins, AvailabilityWeekOverrides(), mode, step);
            else
            {
                var (model, answers) = AvailabilityBuild(step, kitchen);
                _availability = model;
                if (inputs != null) _lexicon?.StoreAvailability(key, answers with { Inputs = inputs });
            }
            this.Monitor.Log(
                $"Availability model {key}: {(stored != null ? "served from the lexicon" : "built")} in {timer.ElapsedMilliseconds} ms (inputs {inputs}, hashed in {hashMs} ms; stored {storedInputs}).",
                LogLevel.Trace);
            if (_runController != null) _runController.Availability = _availability;
            if (_boardBuilder != null) _boardBuilder.Availability = _availability;
            return _availability;
        }

        /// <summary>The cold availability build for one step and kitchen state, with its answers.</summary>
        private (TheLongestYear.Core.ItemAvailabilityModel Model, TheLongestYear.Core.Lexicon.AvailabilityAnswers Answers) AvailabilityBuild(
            TheLongestYear.Core.DifficultyStep step, bool kitchen)
            => TheLongestYear.Core.Availability.ItemAvailabilityBuilder.BuildWithAnswers(
                _enginePools, seasonOverrides: _itemSeasonPins, effortData: _effortData, hasKitchen: kitchen,
                weekOverrides: AvailabilityWeekOverrides(), mode: TheLongestYear.Core.WeekModes.For(step), step: step,
                hasUpgrade: _meta.State.HasUpgrade);

        /// <summary><see cref="TheLongestYear.Core.Lexicon.AvailabilityAnswers.InputsHash"/> of the live
        /// availability inputs, or null (nothing is served or stored) when they cannot be serialised.</summary>
        private string AvailabilityInputsHash()
        {
            if (_enginePools == null) return null;
            try
            {
                return AvailabilityInputsNow().Hash();
            }
            catch (Exception ex) when (ex is NotSupportedException or System.Text.Json.JsonException or ArgumentException)
            {
                this.Monitor.Log($"Availability inputs could not be hashed ({ex.GetType().Name}: {ex.Message}); building without the lexicon.", LogLevel.Trace);
                return null;
            }
        }

        /// <summary>The live availability inputs (<see cref="TheLongestYear.Core.Lexicon.AvailabilityInputs"/>:
        /// what <see cref="AvailabilityInputsHash"/> hashes and <c>tly_lexicon dumpinputs</c> writes), or
        /// null before the engine pools are read.</summary>
        private TheLongestYear.Core.Lexicon.AvailabilityInputs AvailabilityInputsNow()
            => _enginePools == null
                ? null
                : TheLongestYear.Core.Lexicon.AvailabilityInputs.From(
                    _enginePools, _effortData, _itemSeasonPins, null, AvailabilityWeekOverrides(),
                    TheLongestYear.Core.YearTwoCrops.IsCapped(_meta.State.HasUpgrade));

        /// <summary>The availability week overrides in force: the supported-mod layers' week rulings, with
        /// the config's <c>AvailabilityWeekOverrides</c> laid over them (the config wins per id). Used for
        /// the build, the served answers and their inputs hash alike, so a layer ruling counts in all three.</summary>
        private Dictionary<string, int> AvailabilityWeekOverrides()
        {
            // No layer rulings: the config's own dictionary, so the inputs hash is the one it always was.
            if (_lexicon?.Loaded.Set.WeekRulings is not { Count: > 0 } layers) return _config.AvailabilityWeekOverrides;
            var merged = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var (id, week) in layers) merged[id] = week;
            if (_config.AvailabilityWeekOverrides != null)
                foreach (var (id, week) in _config.AvailabilityWeekOverrides) merged[id] = week;
            return merged;
        }

        /// <summary>Every step's availability answers under both kitchen states, by
        /// <see cref="TheLongestYear.Core.Lexicon.AvailabilityAnswers.Key"/>, for <c>tly_lexicon write vanilla</c>.</summary>
        private IReadOnlyDictionary<string, TheLongestYear.Core.Lexicon.AvailabilityAnswers> AllAvailabilityAnswers()
        {
            string inputs = AvailabilityInputsHash();
            var all = new Dictionary<string, TheLongestYear.Core.Lexicon.AvailabilityAnswers>(StringComparer.Ordinal);
            if (inputs == null) return all;
            foreach (TheLongestYear.Core.DifficultyStep step in Enum.GetValues<TheLongestYear.Core.DifficultyStep>())
                foreach (bool kitchen in new[] { false, true })
                    all[TheLongestYear.Core.Lexicon.AvailabilityAnswers.Key(step, kitchen)] = AvailabilityBuild(step, kitchen).Answers with { Inputs = inputs };
            return all;
        }

        private static string DisplayName(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return "(none)";
            if (BundleParsing.IsCategoryRef(itemId)) return $"any item in category {itemId}";
            try
            {
                var data = StardewValley.ItemRegistry.GetData(BundleParsing.NormalizeItemId(itemId));
                return data != null ? data.DisplayName : itemId;
            }
            catch (Exception ex)
            {
                PatchLog.TraceOnce("name:" + itemId, $"No display name for '{itemId}' ({ex.GetType().Name}); using the id.");
                return itemId;
            }
        }
    }
}
