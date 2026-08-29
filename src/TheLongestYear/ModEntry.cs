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
        private BoostPurchaseService _boostPurchases;
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
        private IntroEventInjector _introInjector;
        private IntroSequenceDriver _introDriver;
        private Day28CutsceneDriver _day28Driver;
        private BookFurniture _bookFurniture;
        private UI.PlanningShrineService _planningShrine;
        private TheLongestYear.Loop.OnboardingMailService _onboardingMail;
        private TheLongestYear.Loop.PierreYear2SeedsService _pierreSeeds;

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
            // Vanilla item display names for catalog rows that use the item: token (Keep <book>).
            TheLongestYear.Core.Strings.InitItemNames(id => ItemRegistry.GetDataOrErrorItem(id).DisplayName);

            _config = helper.ReadConfig<GameplayConfig>();
            CartSlotLimitPatch.Enabled = _config.LimitTravelingCartStock;
            TheLongestYear.Loop.FestivalTimeFlow.Enabled = _config.FestivalTimeFlows;
            TheLongestYear.Loop.FestivalMainEventOncePatch.Enabled = _config.FestivalMainEventOncePerDay;

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
            // Skip the overnight FarmEvent on FAIL nights — its end-of-event warp orphans the Fail
            // scene and drops the reset (see FarmEventSuppressionPatch). _runController is built on
            // save load, so resolve it lazily like the driver does.
            FarmEventSuppressionPatch.SuppressTonight =
                () => _runController?.PendingCutscene == TheLongestYear.Core.Day28.Day28Branch.Fail;
            FarmEventSuppressionPatch.Monitor = this.Monitor;
            WeatherScheduleWriterPatch.Monitor = this.Monitor;
            // Placeable book furniture (Cookbook/Craftbook/Bundle-log) — registers via asset edit.
            _bookFurniture = new BookFurniture(this.Monitor, helper);
            // View-only planning shrine — registers its furniture + auto-places near the stash.
            _planningShrine = new UI.PlanningShrineService(this.Monitor, helper);
            // First-loop Spring-1 onboarding letter. Constructed at Entry so AssetRequested is
            // hooked before the first asset load (same reason as _introInjector above).
            _onboardingMail = new TheLongestYear.Loop.OnboardingMailService(this.Monitor, _meta);
            helper.Events.Content.AssetRequested += _onboardingMail.OnAssetRequested;
            // pierre_year2_seeds: Data/Shops edit gated on ownership (UpgradeChecker, per save).
            _pierreSeeds = new TheLongestYear.Loop.PierreYear2SeedsService(this.Monitor);
            helper.Events.Content.AssetRequested += _pierreSeeds.OnAssetRequested;
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
            // Same gate: the new-game "Community Center Bundles" dropdown becomes a single "TLY Custom"
            // entry while the mod is enabled (the engine owns the board; the vanilla choice is moot).
            BundleOptionPatch.Enabled = () => _config.Enabled;
            BundleOptionPatch.Monitor = this.Monitor;
            BundleOptionPatch.ConfiguredSource = () => _config.BundleSource;
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

            // Wire the static monitor/config the weapon/hat donation patch cluster reads
            // (kill-switched via GameplayConfig.EnableNonObjectDonations + the RunActivation
            // gate — see BundleDonationPatches). _config is a single stable instance for the
            // whole session, so Connect only needs to run once here.
            TheLongestYear.Patches.BundleDonationPatches.Connect(this.Monitor, _config);

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

            helper.ConsoleCommands.Add("tly_meta", "Print The Longest Year meta-state (requires a loaded save).", this.PrintMeta);
            helper.ConsoleCommands.Add("tly_loadsave", "Load a save by folder name from the title screen (debug/automation). Usage: tly_loadsave <saveFolderName>", this.CmdLoadSave);
            helper.ConsoleCommands.Add("tly_addjp", "Add Junimo Points in memory; persists on the next save. Usage: tly_addjp <amount>", this.AddJp);
            helper.ConsoleCommands.Add("tly_addmoney", "Add gold to the loaded farmer (debug). Usage: tly_addmoney <amount>", this.AddMoney);
            helper.ConsoleCommands.Add("tly_additem", "Grant an item to the farmer (debug). Usage: tly_additem <qualifiedId> [count]", this.CmdAddItem);
            helper.ConsoleCommands.Add("tly_removehorse", "Remove the stable + horse, clear the carryover snapshot, and drop the Keep Horse upgrade so it's re-buyable (debug — clean slate for a Keep-Horse carryover test).", this.CmdRemoveHorse);
            helper.ConsoleCommands.Add("tly_reset", "Force an in-place reset to Spring 1 (debug). An optional seed loop pins the board the new run generates (same number tly_genbundles takes), so two runs can be played on the same board. Usage: tly_reset [seedLoop]", this.ForceReset);
            helper.ConsoleCommands.Add("tly_setday", "Jump the in-game date to <day> of the current season so you can sleep straight into that day's gate (e.g. day 28) without grinding a month. Sleep to trigger it. Usage: tly_setday <day>", this.CmdSetDay);
            helper.ConsoleCommands.Add("tly_failreset", "Simulate a day-28 gate-miss reset: opens the JP shrine, then resets to Spring 1 on close (debug — exercises the natural loop-reset path the JP-refund bug lived in).", this.CmdFailReset);
            helper.ConsoleCommands.Add("tly_win", "Open the basic win screen, then the JP shrine + keep-playing choice (debug — bypasses the first-win-only gate, re-runnable).", this.CmdForceWin);
            helper.ConsoleCommands.Add("tly_resetif", "Reset only if the loaded farmer's name matches. Usage: tly_resetif <name>", this.ResetIfNameMatches);
            helper.ConsoleCommands.Add("tly_leaktest", "Reset twice and report any state that leaks between runs (debug).", this.LeakTest);
            helper.ConsoleCommands.Add("tly_select", "Select a theme. With the planning hub open this is the card click (any theme, hub closes); otherwise it forces the theme for the current week. Usage: tly_select <theme>", this.CmdSelect);
            helper.ConsoleCommands.Add("tly_offer", "Show this week's selection offer.", this.CmdOffer);
            helper.ConsoleCommands.Add("tly_skipscene", "Finish the open day-28 Junimo scene as if clicked through (debug/automation).", this.CmdSkipScene);
            helper.ConsoleCommands.Add("tly_donate", "Simulate a CC donation. Usage: tly_donate <itemId>", this.CmdDonate);
            helper.ConsoleCommands.Add("tly_runstate", "Print the current run state.", this.CmdRunState);
            helper.ConsoleCommands.Add("tly_netstate", "Print the NetWorldState fields the keep/wipe audit rules, for smoking a reset.", this.CmdNetState);
            helper.ConsoleCommands.Add("tly_gatecheck", "Audit the live board's season gates: for every bundle and every season, what the gate demands against what is actually obtainable by then. Flags anything IMPOSSIBLE (would brick the run) and anything FREE (gate demands nothing). Read-only.", this.CmdGateCheck);
            helper.ConsoleCommands.Add("tly_gateneeds", "Print, per bundle, what the current season's day-28 gate still needs (the same numbers the Season Goals page shows) plus the vault. Read-only.", this.CmdGateNeeds);
            helper.ConsoleCommands.Add("tly_playseason", "Debug: simulate a minimal compliant player for the current season (donate exactly what every gate demands by day 28, pay the vault; 'goals' also deposits this week's goal slots; 'goalsonly' deposits only the goal slots; 'quarter <k>' donates only the first k/4 of the season's share, cumulative across k=1..4, and pays the vault on k=4). Real CC slot flips. Follow with tly_setday 28 and a sleep. Usage: tly_playseason [goals|goalsonly|quarter <1-4>]", this.CmdPlaySeason);
            helper.ConsoleCommands.Add("tly_goals", "Log the weekly goals every theme would offer on the LIVE board for a season (the same sample the planning hub shows). Read-only. Usage: tly_goals [spring|summer|fall|winter] [weekOfYear]", this.CmdGoals);
            helper.ConsoleCommands.Add("tly_themepool", "Print each theme's askable weekly-goal count for the current week (rule C's number), or, with a theme, every candidate line with due/filler, effort, tier and weight. Read-only. Usage: tly_themepool [theme]", this.CmdThemePool);
            helper.ConsoleCommands.Add("tly_dumpbundles", "Write a Markdown catalogue of every bundle the engine can produce, with every item each one can ask for and how its quantity is decided. Reads LIVE game data, so it covers whatever content mods are installed. Usage: tly_dumpbundles [fileName]", this.CmdDumpBundles);
            helper.ConsoleCommands.Add("tly_dumpavailability", "Write a Markdown listing of every item in every bundle on the LIVE board with the earliest season the engine says it can exist, why, and the season its gate demands it. Usage: tly_dumpavailability [fileName]", this.CmdDumpAvailability);
            helper.ConsoleCommands.Add("tly_itemmodel", "Print the derived availability model for one item id or every ingredient of a bundle. Usage: tly_itemmodel <itemId|bundleName>", this.CmdItemModel);
            helper.ConsoleCommands.Add("tly_dumpeffort", "Write a Markdown review of the derived item effort model: every pool item by theme with its effort, tier (quartile within the theme's pool), source and game-data basis. Usage: tly_dumpeffort [fileName]", this.CmdDumpEffort);
            helper.ConsoleCommands.Add("tly_difficulty", "Read-only: print the ten configured difficulty steps, the ten this loop is actually running under, and every resolved value. Attach this to any balance report.", this.CmdDifficulty);
            helper.ConsoleCommands.Add("tly_catalog", "Print the bundle-derived CC catalog summary.", this.CmdCatalog);
            helper.ConsoleCommands.Add("tly_classify", "Re-run bundle classification over the live BundleData and log the summary (diagnostics only — does not touch the active run). Pairs with 'debug ShuffleBundles' to exercise remixed classification in memory.", this.CmdClassify);
            helper.ConsoleCommands.Add("tly_genbundles", "Generate (diagnostics only) the engine bundle set for a loop: nothing written or persisted. Logs each room's picked bundles + slot counts, the manifest classification summary, and a determinism self-check (regenerates off the same seed and diffs). Requires a loaded save (the seed uses Game1.player.UniqueMultiplayerID). Usage: tly_genbundles [seedLoop] [custom|standard|remixed] (default: the current board's seed loop, custom = the TLY engine set; standard/remixed audit the board vanilla would build for that Advanced Options choice)", this.CmdGenBundles);
            helper.ConsoleCommands.Add("tly_trophytest", "Diagnostics-only proof that the weapon/hat donation patches accept (W)13/(H)8/(O)520 as valid Gil's Trophies ingredients. Builds ephemeral items + a detached synthetic Bundle (never touches the real CC board) and logs PASS/FAIL per id. Requires a loaded save.", this.CmdTrophyTest);
            helper.ConsoleCommands.Add("tly_testdonate", "Simulate a CC donation through the JP service. Usage: tly_testdonate <qualifiedId> [count]", this.CmdTestDonate);
            helper.ConsoleCommands.Add("tly_openhub", "Open the weekly planning hub menu (debug).", this.CmdOpenHub);
            helper.ConsoleCommands.Add("tly_seasongoals", "Open the Season Goals page, the same one the Bundle Log book opens (debug).", this.CmdSeasonGoals);
            helper.ConsoleCommands.Add("tly_bundlesource", "Diagnostics: show or set the loaded save's bundle source / vanilla type in memory (persists on the next save). Usage: tly_bundlesource [Engine|Vanilla] [Default|Remixed] — also sets the config's BundleSource so the next reset honours it.", this.CmdBundleSource);
            helper.ConsoleCommands.Add("tly_jpbudget", "Diagnostics only: log the maximum JP the CURRENT loop's board can pay out, per season + total (earliest-obtainable-season model) and a hoard-for-Winter ceiling. Baseline economy, no jp_boost. Usage: tly_jpbudget [verbose]", this.CmdJpBudget);
            helper.ConsoleCommands.Add("tly_openshop", "Open the Junimo Shrine upgrade shop (debug).", this.CmdOpenShop);
            helper.ConsoleCommands.Add("tly_listupgrades", "List the upgrade catalog grouped by category.", this.CmdListUpgrades);
            helper.ConsoleCommands.Add("tly_dumpevents", "Audit Data/Events for furnace/cave/early-scene ids (debug — logs candidates so the event-gating tables use real ids, not guesses).", this.CmdDumpEvents);
            helper.ConsoleCommands.Add("tly_dumpreplayable", "Audit which Data/Events cutscenes the loop treats as REPLAYABLE (re-fire each loop): logs each unlock-granting event id, the matched grant command, whether it's excluded, and the active exclusion set (debug — diagnoses 'an event keeps replaying').", this.CmdDumpReplayable);
            helper.ConsoleCommands.Add("tly_buyupgrade", "Buy an upgrade by id (debug). Usage: tly_buyupgrade <id>", this.CmdBuyUpgrade);
            helper.ConsoleCommands.Add("tly_boost", "Buy a shrine boost for the current week (debug, the same purchase the shrine's Buy button makes). Usage: tly_boost <yeartwoseeds|sneakpeek>", this.CmdBoost);
            helper.ConsoleCommands.Add("tly_tv", "Debug: run the Queen of Sauce weekly-recipe lookup the TV uses (no mouse needed) and log the returned dialogue plus whether the recipe landed in cookingRecipes. Exercises the Sneak Peek boost patch. NOT read-only: this is the real grant path, so it teaches the player that episode's recipe exactly as watching the TV would.", this.CmdTv);
            helper.ConsoleCommands.Add("tly_dejavu", "Deja-vu dialogue debug. Usage: tly_dejavu [status | set <npc> <n> | force <npc> | reset]", this.CmdDejaVu);
            helper.ConsoleCommands.Add("tly_readbook","Debug: mark a power book as read (sets its Book_* stat). No args lists every Book_* stat. Usage: tly_readbook [Book_Id]", this.CmdReadBook);
            helper.ConsoleCommands.Add("tly_wallet", TheLongestYear.DebugCommands.WalletDebugCommand.Usage,
                (cmd, a) => TheLongestYear.DebugCommands.WalletDebugCommand.Run(this.Monitor, a));
            helper.ConsoleCommands.Add("tly_payvault", "Mark a Vault bundle as paid this run (debug — Harmony hookup is Plan 06). Usage: tly_payvault <season|index>", this.CmdPayVault);
            helper.ConsoleCommands.Add("tly_hold", "Debug: apply the Fail-night hold choice in memory without a fail night. Usage: tly_hold keep|reshuffle|status. keep deducts JP per the config curve; the next reset (tly_reset) then honours it. Must be followed by tly_reset before sleeping; a real Fail night after tly_hold keep charges the next tier again.", this.CmdHold);
            helper.ConsoleCommands.Add("tly_pity", "Debug: season pity counters and the Fail-night offer. Usage: tly_pity status | tly_pity set <spring|summer|fall|winter> <fails> | tly_pity accept|decline (after tly_hold keep|reshuffle, before tly_reset).", this.CmdPity);
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
            helper.ConsoleCommands.Add("tly_addpet",
                "Debug: add a pet to the Farm, or list every pet with its location and bowl. " +
                "Usage: tly_addpet <Cat|Dog> [name] [breed] | tly_addpet check",
                this.CmdAddPet);
            helper.ConsoleCommands.Add("tly_fixbridge",
                "Debug: mark the beach bridge repaired (Beach.bridgeFixed), or report the flag + the " +
                "bridge tiles so a reset can be checked to un-fix it. Usage: tly_fixbridge | tly_fixbridge check",
                this.CmdFixBridge);
            helper.ConsoleCommands.Add("tly_stashrod",
                "Debug: drop an Iridium Rod with bait, a spinner and an Auto-Hook enchantment into the " +
                "Junimo Stash chest, or print every stashed tool's slots + enchantments. " +
                "Usage: tly_stashrod | tly_stashrod check",
                this.CmdStashRod);

            this.Monitor.Log("The Longest Year loaded.", LogLevel.Info);
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
            bool wasNewGame = _isNewGame;
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
            if (wasNewGame)
            {
                // Per-save bundle source from the Advanced Options dropdown (BundleOptionPatch):
                // TLY Custom (default) → Engine; Normal/Remixed → Vanilla + the vanilla type so
                // every reset regenerates the same kind of board (Nexus bug 1108030 root cause:
                // Game1.bundleType is never persisted by the game).
                BundleOptionPatch.Choice choice = BundleOptionPatch.ConsumeLastChoice();
                string chosenSource = choice switch
                {
                    BundleOptionPatch.Choice.VanillaRemixed => BundleSourceNames.Remixed,
                    BundleOptionPatch.Choice.VanillaStandard => BundleSourceNames.Normal,
                    _ => BundleSourceNames.Engine,
                };
                _meta.State.BundleSource = BundleSourceNames.IsVanilla(chosenSource)
                    ? BundleSourceNames.LegacyVanilla : BundleSourceNames.Engine;
                _meta.State.VanillaBundleType =
                    BundleSourceNames.VanillaTypeFor(chosenSource) ?? Game1.BundleType.Default.ToString();

                // Mirror the Advanced Options pick into the config, which is the ONE setting that
                // owns this from now on. Without this the first reset would re-stamp from a config
                // the player never touched and silently undo the choice he just made.
                if (!string.Equals(_config.BundleSource, chosenSource, StringComparison.OrdinalIgnoreCase))
                {
                    _config.BundleSource = chosenSource;
                    this.Helper.WriteConfig(_config);
                }

                this.Monitor.Log(
                    $"New game: bundle source={chosenSource} (Advanced Options choice {choice}, vanilla type {_meta.State.VanillaBundleType}).",
                    LogLevel.Info);
            }
            RunActivation.Activate();
            _metaLoaded = true;
            // Inject the tly_intro_done mail flag now if the player has already seen the intro
            // on a prior loop — that's what suppresses both intro events for years 2+.
            _introInjector?.ApplyMailFlagsForRun();
            UpgradeChecker.HasUpgrade = id => _meta.State.HasUpgrade(id);
            BoostChecker.YearTwoSeedsActive = () => TheLongestYear.Core.BoostState.YearTwoSeedsActive(_meta.Run, _meta.Run.WeekOfYear);
            BoostChecker.SneakPeekActive = () => TheLongestYear.Core.BoostState.SneakPeekActive(_meta.Run, _meta.Run.Season);
            CartSlotLimitPatch.RunProvider = () => _meta.Run;
            CartSlotLimitPatch.StartingSlotsProvider = () => _meta.State.EffectiveDifficulty(_config).StartingCartSlots;
            // Once-per-day guard for festival main events (Egg Hunt and friends): TLY festivals do
            // not end the day, so the map stays re-entrant and vanilla would offer the hunt again.
            TheLongestYear.Loop.FestivalMainEventOncePatch.RunProvider = () => _meta.Run;
            TheLongestYear.Loop.FestivalMainEventOncePatch.Monitor = this.Monitor;
            // Ownership is per save: re-evaluate the Pierre year-2-seeds shop edit for this save.
            this.Helper.GameContent.InvalidateCache(TheLongestYear.Loop.PierreYear2SeedsService.ShopAssetName);
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

            // Engine pools double as season ground truth: fish/crab-pot spawn seasons feed
            // the SeasonResolver (so weekly themes can't ask for out-of-season fish, Nexus
            // 1122423) and DerivedSeasonPins feed the obtainability clamp below.
            TheLongestYear.Core.ItemPools enginePools =
                new TheLongestYear.Loop.GameDataPools(this.Monitor).Build(_config.PoolTuning,
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

            _runController = new RunController(this.Monitor, _meta, _config, _reset, _catalog, _requirements);
            _runController.GoalCaps = new[]
            {
                new GoalGroupCap(enginePools.FruitTreeFruitIds, 1),
                new GoalGroupCap(enginePools.TrapFishIds, 1),
            };
            _runController.Availability = _availability;
            _runController.ItemKindOf = id =>
            {
                string bare = BundleParsing.StripQualifier(id);
                return Game1.objectData != null && Game1.objectData.TryGetValue(bare, out var objectData)
                    ? ItemKindClassifier.From(objectData.Category, objectData.Type)
                    : ItemKind.Other;
            };
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
            _purchases = new UpgradePurchaseService(this.Monitor, _meta, _config);
            _purchases.Purchased = id =>
            {
                if (id == TheLongestYear.Loop.PierreYear2SeedsService.UpgradeId)
                    this.Helper.GameContent.InvalidateCache(TheLongestYear.Loop.PierreYear2SeedsService.ShopAssetName);
            };
            _launcher = new MenuLauncher(this.Monitor, _config, _meta, _runController, _purchases);
            _runController.AttachLauncher(_launcher);
            _bookFurniture.AttachLauncher(() => _launcher);
            _planningShrine.AttachState(() => _meta.State);
            _planningShrine.AttachPriceFactor(() => _meta.State.EffectiveDifficulty(_config).ShrinePriceFactor);
            _boostPurchases = new BoostPurchaseService(this.Monitor, _meta);
            _planningShrine.AttachBoosts(() => _meta.Run, id => _boostPurchases.TryBuy(id));
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
            // The quarter baseline belongs to one save's season; carrying it to the title screen would
            // let the next save's quarter 2 plan against the previous save's ledger.
            _playSeasonBaseline = null;
            _playSeasonDonatedThisSeason = 0;
            TheLongestYear.Patches.BundleDonationPatches.LiveBoardHasNonObjectSlots = false;
            ActiveEffectsProvider.Clear();
            TheLongestYear.Loop.UpgradeChecker.HasUpgrade = null;
            TheLongestYear.Loop.BoostChecker.YearTwoSeedsActive = null;
            TheLongestYear.Loop.BoostChecker.SneakPeekActive = null;
            TheLongestYear.Loop.CartSlotLimitPatch.RunProvider = null;
            TheLongestYear.Loop.CartSlotLimitPatch.StartingSlotsProvider = null;
            TheLongestYear.Loop.FestivalMainEventOncePatch.RunProvider = null;
            BundleOptionPatch.ResetChoice();
            _boardBuilder = null;
            _boardFingerprint = null;
            this.Helper.GameContent.InvalidateCache(TheLongestYear.Loop.PierreYear2SeedsService.ShopAssetName);
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

            // SaveGame.Load on a folder that does not exist fails SILENTLY: the game drops to the
            // title screen and simply never finishes loading, which reads exactly like a hang.
            // That is easy to hit because a TLY reset RENAMES the save folder (it re-seeds
            // uniqueIDForThisGame, and the folder name embeds it), so yesterday's folder name is
            // stale after any loop. Check first and list what is actually there.
            string savesDir = System.IO.Path.Combine(
                StardewModdingAPI.Constants.DataPath ?? "", "Saves");
            string target = System.IO.Path.Combine(savesDir, args[0]);
            if (System.IO.Directory.Exists(savesDir) && !System.IO.Directory.Exists(target))
            {
                string[] available = System.IO.Directory.GetDirectories(savesDir)
                    .Select(System.IO.Path.GetFileName).OrderBy(n => n, System.StringComparer.Ordinal).ToArray();
                this.Monitor.Log(
                    $"tly_loadsave: no save folder named '{args[0]}'. A TLY reset renames the folder " +
                    $"(the name embeds uniqueIDForThisGame), so an older name goes stale. Available: " +
                    $"{(available.Length > 0 ? string.Join(", ", available) : "(none)")}",
                    LogLevel.Warn);
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

        /// <summary>Debug: add a pet (or list them). Smoke scaffolding for Keep Pet with several
        /// pets (Nexus bug 1122901): the throwaway save has none, and vanilla adoption needs Marnie.</summary>
        private void CmdAddPet(string command, string[] args)
        {
            if (!Context.IsWorldReady) { this.Monitor.Log("Load a save first.", LogLevel.Warn); return; }
            if (args.Length == 0 || args[0] == "check")
            {
                foreach (StardewValley.Characters.Pet pet in Utility.getAllPets())
                {
                    var bowl = pet.GetPetBowl();
                    this.Monitor.Log(
                        $"tly_addpet: '{pet.Name}' ({pet.petType.Value}) in {pet.currentLocation?.Name ?? "?"} at " +
                        $"({pet.Tile.X},{pet.Tile.Y}), friendship {pet.friendshipTowardFarmer.Value}, " +
                        $"bowl={(bowl == null ? "NONE" : $"({bowl.tileX.Value},{bowl.tileY.Value})")}.", LogLevel.Info);
                }
                this.Monitor.Log($"tly_addpet: {Utility.getAllPets().Count} pet(s), " +
                    $"{Game1.getFarm().buildings.OfType<StardewValley.Buildings.PetBowl>().Count()} bowl(s) on the Farm.", LogLevel.Info);
                return;
            }
            string type = args[0];
            string name = args.Length > 1 ? args[1] : type;
            string breed = args.Length > 2 ? args[2] : "0";
            var farm = Game1.getFarm();
            var added = new StardewValley.Characters.Pet(54, 8, breed, type) { Name = name, displayName = name };
            farm.characters.Add(added);
            this.Monitor.Log($"tly_addpet: added {type} '{name}' (breed {breed}) on the Farm.", LogLevel.Info);
        }

        /// <summary>Debug: repair the beach bridge in place, or report its state. Smoke scaffolding
        /// for Nexus bug 1124076 (the rewind must put the broken bridge back).</summary>
        private void CmdFixBridge(string command, string[] args)
        {
            if (!Context.IsWorldReady) { this.Monitor.Log("Load a save first.", LogLevel.Warn); return; }
            if (Game1.getLocationFromName("Beach") is not StardewValley.Locations.Beach beach)
            {
                this.Monitor.Log("tly_fixbridge: no Beach location loaded.", LogLevel.Warn);
                return;
            }
            if (args.Length == 0)
            {
                beach.bridgeFixed.Value = true;   // fieldChangeEvent runs Beach.fixBridge on the live map
                this.Monitor.Log("tly_fixbridge: bridgeFixed set; vanilla edited the Beach map in place.", LogLevel.Info);
            }
            int tile = beach.getTileIndexAt(58, 13, "Buildings");
            bool hasAction = beach.doesTileHaveProperty(58, 13, "Action", "Buildings") != null;
            this.Monitor.Log(
                $"tly_fixbridge: bridgeFixed={beach.bridgeFixed.Value}, Buildings tile (58,13)={tile} " +
                $"(284 = broken, 301 = repaired), Action property {(hasAction ? "present" : "MISSING")}, " +
                $"walkable={beach.isTilePassable(new Microsoft.Xna.Framework.Vector2(59, 13))}.", LogLevel.Info);
        }

        /// <summary>Debug: put a fully loaded rod in the stash, or list stashed tools' state. Smoke
        /// scaffolding for the 0.16.1/0.16.2 stash fixes.</summary>
        private void CmdStashRod(string command, string[] args)
        {
            if (!Context.IsWorldReady) { this.Monitor.Log("Load a save first.", LogLevel.Warn); return; }
            var chest = _stashService?.FindStashChest();
            if (chest == null)
            {
                this.Monitor.Log("tly_stashrod: no stash chest found. Own stash_1 and reset/reload first.", LogLevel.Warn);
                return;
            }
            if (args.Length > 0 && args[0] == "check")
            {
                foreach (Item item in chest.Items)
                {
                    if (item is not Tool tool) continue;
                    string slots = string.Join(", ", tool.attachments.Select((a, i) => $"[{i}]={(a == null ? "empty" : $"{a.QualifiedItemId} x{a.Stack}")}"));
                    string ench = string.Join(", ", tool.enchantments.Select(e => $"{e.GetType().Name} L{e.GetLevel()}"));
                    string stats = tool is StardewValley.Tools.MeleeWeapon w
                        ? $"; damage {w.minDamage.Value}-{w.maxDamage.Value}, defense {w.addedDefense.Value}, speed {w.speed.Value}"
                        : "";
                    this.Monitor.Log($"tly_stashrod: {tool.QualifiedItemId} slots {slots}; enchantments [{ench}]{stats}.", LogLevel.Info);
                }
                this.Monitor.Log($"tly_stashrod: {chest.Items.Count(i => i != null)} item(s) in the stash chest.", LogLevel.Info);
                return;
            }
            if (args.Length > 0 && args[0] == "weapon")
            {
                // Nexus bug (Bumblewyn, 2026-08-28): a weapon's innate enchantment + forged gem
                // vanish across the loop. Stash a Galaxy Sword with Attack II (innate, secondary)
                // and a Ruby forge (IsForge) the way vanilla adds them, then compare `check`
                // before and after tly_reset.
                var weapon = ItemRegistry.Create("(W)4") as StardewValley.Tools.MeleeWeapon;
                if (weapon == null) { this.Monitor.Log("tly_stashrod: could not create (W)4.", LogLevel.Warn); return; }
                weapon.AddEnchantment(new StardewValley.Enchantments.AttackEnchantment());
                weapon.AddEnchantment(new StardewValley.Enchantments.AttackEnchantment());
                weapon.AddEnchantment(new StardewValley.Enchantments.RubyEnchantment());
                chest.Items.Add(weapon);
                string wench = string.Join(", ", weapon.enchantments.Select(e => $"{e.GetType().Name} L{e.GetLevel()}"));
                this.Monitor.Log(
                    $"tly_stashrod: stashed a Galaxy Sword with [{wench}]; damage {weapon.minDamage.Value}-{weapon.maxDamage.Value}.",
                    LogLevel.Info);
                return;
            }
            var rod = ItemRegistry.Create("(T)IridiumRod") as Tool;
            if (rod == null) { this.Monitor.Log("tly_stashrod: could not create (T)IridiumRod.", LogLevel.Warn); return; }
            if (rod.attachments.Count > 0) rod.attachments[0] = ItemRegistry.Create<StardewValley.Object>("(O)685", 20);
            if (rod.attachments.Count > 1) rod.attachments[1] = ItemRegistry.Create<StardewValley.Object>("(O)686");
            var hook = new StardewValley.Enchantments.AutoHookEnchantment();
            rod.enchantments.Add(hook);
            hook.ApplyTo(rod);
            chest.Items.Add(rod);
            this.Monitor.Log($"tly_stashrod: stashed an Iridium Rod with {rod.attachments.Count} slot(s) filled and Auto-Hook.", LogLevel.Info);
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

            int? pin = null;
            if (args.Length > 0)
            {
                if (!int.TryParse(args[0], out int seedLoop) || seedLoop < 0)
                {
                    this.Monitor.Log($"tly_reset: '{args[0]}' is not a seed loop. Usage: tly_reset [seedLoop]", LogLevel.Warn);
                    return;
                }
                pin = seedLoop;
            }

            // The pin mutates MetaState, so it must not run when the reset itself cannot: a missing
            // run controller used to leave the save carrying a pinned seed loop for a reset that
            // never happened.
            if (_runController == null)
            {
                this.Monitor.Log("Reset unavailable: no run controller (load a save first).", LogLevel.Warn);
                return;
            }

            if (pin.HasValue) PinSeedLoopForNextReset(pin.Value);

            FullResetAndPresentOffer();
        }

        /// <summary>Debug: force the NEXT reset onto a chosen bundle seed loop, so two runs can
        /// be played on the same board (same number <c>tly_genbundles</c> takes).
        ///
        /// The pin has to survive <see cref="BundleHold.ConsumeChoiceAtReset"/>, which otherwise
        /// snaps BundleSeedLoop back to the post-bump CompletedResets for any reset that skipped
        /// the Fail-night hold question, which is exactly the console path.  Stamping
        /// HoldChoiceMadeForReset makes that call a no-op, so the pin stands; the pity trim/ease
        /// that the same call would normally clear is cleared here instead, since a board that
        /// carried a leftover trim would not match the same seed loop generated elsewhere.
        /// ConsecutiveHolds is zeroed too: this is a debug pin, not a paid hold.</summary>
        private void PinSeedLoopForNextReset(int seedLoop)
        {
            TheLongestYear.Core.MetaState state = _meta.State;
            state.BundleSeedLoop = seedLoop;
            state.ConsecutiveHolds = 0;
            TheLongestYear.Core.SeasonPity.ClearBoardTrim(state);
            TheLongestYear.Core.SeasonPity.ClearBoardEase(state);
            state.HoldChoiceMadeForReset = true;
            this.Monitor.Log(
                $"tly_reset: pinned bundle seed loop {seedLoop} for this reset (pity trim/ease cleared, consecutive holds zeroed).",
                LogLevel.Info);
            if (TheLongestYear.Core.BundleSourceNames.IsVanilla(state.BundleSource))
                this.Monitor.Log(
                    "tly_reset: this save runs a vanilla board, which regenerates through loadForNewGame and never reads the seed loop, so the pin will not change the board.",
                    LogLevel.Warn);
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
            // A planning hub still up when tly_reset fires survives the in-place reset and then
            // blocks the new run's week-1 offer for good: the hub only opens over a clear
            // activeClickableMenu, and a stale hub never closes itself (readyToClose waits for a
            // pick). Drop it here; the reset re-presents week 1 from the real day-start flow.
            if (Game1.activeClickableMenu is TheLongestYear.UI.WeeklyHubMenu)
            {
                this.Monitor.Log("tly_reset: closing the open planning hub so the new run's week-1 offer can open.", LogLevel.Info);
                Game1.exitActiveMenu();
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

        /// <summary>Developer bridge only: the game pauses its update loop whenever its window is not
        /// the foreground window (Game1.cs:4693, unless options.pauseWhenOutOfFocus is off), which is
        /// why every earlier unattended run had to steal focus to make a queued command execute. With
        /// the bridge on, the vanilla "pause when inactive" option is switched off so tly_* commands
        /// keep flowing while Jeff works in another window. A shipped build never touches it.</summary>
        private void KeepRunningUnfocused(string when)
        {
            if (!_config.EnableDebugCommandBridge || Game1.options == null) return;
            if (!Game1.options.pauseWhenOutOfFocus) return;
            Game1.options.pauseWhenOutOfFocus = false;
            this.Monitor.Log(
                $"Debug bridge: 'pause when window is inactive' switched off at {when} so queued commands run without focus.",
                LogLevel.Info);
        }

        private void OnGameLaunched(object sender, StardewModdingAPI.Events.GameLaunchedEventArgs e)
        {
            // Cart Whisperer extends to every day when the standalone Cart Catalog mod is installed.
            TheLongestYear.Loop.CartCatalogIntegration.ModLoaded =
                this.Helper.ModRegistry.IsLoaded(TheLongestYear.Loop.CartCatalogIntegration.ModId);

            this.ApplyWindowSize();
            this.KeepRunningUnfocused("launch");

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
                getValue: () => _config.LimitTravelingCartStock,
                setValue: v => { _config.LimitTravelingCartStock = v; CartSlotLimitPatch.Enabled = v; },
                name: () => Strings.Get("gmcm.cart-limit.name"),
                tooltip: () => Strings.Get("gmcm.cart-limit.tooltip"));

            gmcm.AddBoolOption(this.ModManifest,
                getValue: () => _config.ResendBetterStartGift,
                setValue: v => _config.ResendBetterStartGift = v,
                name: () => Strings.Get("gmcm.resend-better-start.name"),
                tooltip: () => Strings.Get("gmcm.resend-better-start.tooltip"));

            gmcm.AddTextOption(this.ModManifest,
                // One setting, three choices. A config written before this change says the legacy
                // "Vanilla", which names no layout, so show it as whichever layout the loaded save
                // is actually on rather than defaulting a remixed save to Normal.
                getValue: () =>
                {
                    string stored = BundleSourceNames.Normalize(_config.BundleSource);
                    return stored == BundleSourceNames.LegacyVanilla
                        ? BundleSourceNames.ForVanillaType(_meta?.State?.VanillaBundleType)
                        : stored;
                },
                setValue: v => _config.BundleSource = BundleSourceNames.Normalize(v),
                name: () => Strings.Get("gmcm.bundle-source.name"),
                tooltip: () => Strings.Get("gmcm.bundle-source.tooltip"),
                allowedValues: BundleSourceNames.All,
                formatAllowedValue: FormatBundleSource);

            gmcm.AddBoolOption(this.ModManifest,
                getValue: () => _config.AutoDetectReplayableUnlockCutscenes,
                setValue: v => _config.AutoDetectReplayableUnlockCutscenes = v,
                name: () => Strings.Get("gmcm.auto-detect.name"),
                tooltip: () => Strings.Get("gmcm.auto-detect.tooltip"));

            gmcm.AddSectionTitle(this.ModManifest, () => Strings.Get("gmcm.features.section"));
            gmcm.AddParagraph(this.ModManifest, () => Strings.Get("gmcm.features.blurb"));

            gmcm.AddBoolOption(this.ModManifest,
                getValue: () => _config.FestivalTimeFlows,
                setValue: v => { _config.FestivalTimeFlows = v; TheLongestYear.Loop.FestivalTimeFlow.Enabled = v; },
                name: () => Strings.Get("gmcm.festival-time.name"),
                tooltip: () => Strings.Get("gmcm.festival-time.tooltip"));

            gmcm.AddBoolOption(this.ModManifest,
                getValue: () => _config.FestivalMainEventOncePerDay,
                setValue: v => { _config.FestivalMainEventOncePerDay = v; TheLongestYear.Loop.FestivalMainEventOncePatch.Enabled = v; },
                name: () => Strings.Get("gmcm.festival-once.name"),
                tooltip: () => Strings.Get("gmcm.festival-once.tooltip"));

            gmcm.AddBoolOption(this.ModManifest,
                getValue: () => _config.EnableDejaVuDialogue,
                setValue: v => { _config.EnableDejaVuDialogue = v; TheLongestYear.Loop.DejaVuDialoguePatch.Enabled = v; },
                name: () => Strings.Get("gmcm.dejavu.name"),
                tooltip: () => Strings.Get("gmcm.dejavu.tooltip"));

            gmcm.AddBoolOption(this.ModManifest,
                getValue: () => _config.EnableThemeReroll,
                setValue: v => _config.EnableThemeReroll = v,
                name: () => Strings.Get("gmcm.theme-reroll.name"),
                tooltip: () => Strings.Get("gmcm.theme-reroll.tooltip"));

            gmcm.AddBoolOption(this.ModManifest,
                getValue: () => _config.EnableNonObjectDonations,
                setValue: v => _config.EnableNonObjectDonations = v,
                name: () => Strings.Get("gmcm.non-object.name"),
                tooltip: () => Strings.Get("gmcm.non-object.tooltip"));

            gmcm.AddNumberOption(this.ModManifest,
                getValue: () => (float)_config.SelectionBonusMultiplier,
                setValue: v => _config.SelectionBonusMultiplier = v,
                name: () => Strings.Get("gmcm.bonus-mult.name"),
                tooltip: () => Strings.Get("gmcm.bonus-mult.tooltip"),
                min: 1f, max: 3f, interval: 0.1f);

            gmcm.AddNumberOption(this.ModManifest,
                getValue: () => _config.StartingMoney,
                setValue: v => _config.StartingMoney = v,
                name: () => Strings.Get("gmcm.starting-money.name"),
                tooltip: () => Strings.Get("gmcm.starting-money.tooltip"),
                min: 0, max: 5000, interval: 100);

            // ---- Difficulty modifiers (spec 2026-08-26) ----
            // Ten independent dials, no overall tier. Everything defaults to Normal, which is the
            // shipping balance, and a change lands at the NEXT reset because WorldResetService
            // stamps the resolved profile onto the save and every consumer reads that stamp.
            gmcm.AddSectionTitle(this.ModManifest, () => Strings.Get("gmcm.difficulty.section"));
            gmcm.AddParagraph(this.ModManifest, () => Strings.Get("gmcm.difficulty.blurb"));

            void AddDifficultyOption(
                Func<DifficultyStep> get, Action<DifficultyStep> set,
                Func<string> name, Func<string> tooltip)
            {
                gmcm.AddTextOption(this.ModManifest,
                    getValue: () => get().ToString(),
                    setValue: v => set(DifficultySteps.Parse(v)),
                    name: name,
                    tooltip: tooltip,
                    allowedValues: DifficultySteps.AllNames,
                    formatAllowedValue: FormatDifficultyStep);
            }

            AddDifficultyOption(
                () => _config.Difficulty.StackSize, v => _config.Difficulty.StackSize = v,
                () => Strings.Get("gmcm.difficulty.stack-size.name"),
                () => Strings.Get("gmcm.difficulty.stack-size.tooltip"));
            AddDifficultyOption(
                () => _config.Difficulty.QualityAsks, v => _config.Difficulty.QualityAsks = v,
                () => Strings.Get("gmcm.difficulty.quality-asks.name"),
                () => Strings.Get("gmcm.difficulty.quality-asks.tooltip"));
            AddDifficultyOption(
                () => _config.Difficulty.RequiredSlots, v => _config.Difficulty.RequiredSlots = v,
                () => Strings.Get("gmcm.difficulty.required-slots.name"),
                () => Strings.Get("gmcm.difficulty.required-slots.tooltip"));
            AddDifficultyOption(
                () => _config.Difficulty.ItemRarity, v => _config.Difficulty.ItemRarity = v,
                () => Strings.Get("gmcm.difficulty.item-rarity.name"),
                () => Strings.Get("gmcm.difficulty.item-rarity.tooltip"));
            AddDifficultyOption(
                () => _config.Difficulty.JpEarned, v => _config.Difficulty.JpEarned = v,
                () => Strings.Get("gmcm.difficulty.jp-earned.name"),
                () => Strings.Get("gmcm.difficulty.jp-earned.tooltip"));
            AddDifficultyOption(
                () => _config.Difficulty.ShrinePrices, v => _config.Difficulty.ShrinePrices = v,
                () => Strings.Get("gmcm.difficulty.shrine-prices.name"),
                () => Strings.Get("gmcm.difficulty.shrine-prices.tooltip"));
            AddDifficultyOption(
                () => _config.Difficulty.StartingGold, v => _config.Difficulty.StartingGold = v,
                () => Strings.Get("gmcm.difficulty.starting-gold.name"),
                () => Strings.Get("gmcm.difficulty.starting-gold.tooltip"));
            AddDifficultyOption(
                () => _config.Difficulty.CartSlots, v => _config.Difficulty.CartSlots = v,
                () => Strings.Get("gmcm.difficulty.cart-slots.name"),
                () => Strings.Get("gmcm.difficulty.cart-slots.tooltip"));
            AddDifficultyOption(
                () => _config.Difficulty.HoldPrices, v => _config.Difficulty.HoldPrices = v,
                () => Strings.Get("gmcm.difficulty.hold-prices.name"),
                () => Strings.Get("gmcm.difficulty.hold-prices.tooltip"));
            AddDifficultyOption(
                () => _config.Difficulty.SeasonPity, v => _config.Difficulty.SeasonPity = v,
                () => Strings.Get("gmcm.difficulty.season-pity.name"),
                () => Strings.Get("gmcm.difficulty.season-pity.tooltip"));

            gmcm.AddSectionTitle(this.ModManifest, () => Strings.Get("gmcm.pity.section"));
            gmcm.AddBoolOption(this.ModManifest,
                getValue: () => _config.PityEnabled,
                setValue: v => _config.PityEnabled = v,
                name: () => Strings.Get("gmcm.pity.enabled.name"),
                tooltip: () => Strings.Get("gmcm.pity.enabled.tooltip"));
            gmcm.AddNumberOption(this.ModManifest,
                getValue: () => _config.PityThreshold,
                setValue: v => _config.PityThreshold = v,
                name: () => Strings.Get("gmcm.pity.threshold.name"),
                tooltip: () => Strings.Get("gmcm.pity.threshold.tooltip"),
                min: 0, max: 20, interval: 1);
            gmcm.AddNumberOption(this.ModManifest,
                getValue: () => (float)_config.PityQuotaStep,
                setValue: v => _config.PityQuotaStep = v,
                name: () => Strings.Get("gmcm.pity.quota-step.name"),
                tooltip: () => Strings.Get("gmcm.pity.quota-step.tooltip"),
                min: 0f, max: 0.5f, interval: 0.05f);
            gmcm.AddNumberOption(this.ModManifest,
                getValue: () => (float)_config.PityQuotaFloor,
                setValue: v => _config.PityQuotaFloor = v,
                name: () => Strings.Get("gmcm.pity.quota-floor.name"),
                tooltip: () => Strings.Get("gmcm.pity.quota-floor.tooltip"),
                min: 0.1f, max: 1f, interval: 0.05f);
            gmcm.AddNumberOption(this.ModManifest,
                getValue: () => _config.PityTrimPerStep,
                setValue: v => _config.PityTrimPerStep = v,
                name: () => Strings.Get("gmcm.pity.trim.name"),
                tooltip: () => Strings.Get("gmcm.pity.trim.tooltip"),
                min: 0, max: 10, interval: 1);

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
            ReclassifyIfBoardChanged();
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
                case "tly_skipscene": this.CmdSkipScene(command, args); break;
                case "tly_dumpavailability": this.CmdDumpAvailability(command, args); break;
                case "tly_offer": this.CmdOffer(command, args); break;
                case "tly_donate": this.CmdDonate(command, args); break;
                case "tly_runstate": this.CmdRunState(command, args); break;
                case "tly_catalog": this.CmdCatalog(command, args); break;
                case "tly_classify": this.CmdClassify(command, args); break;
                case "tly_genbundles": this.CmdGenBundles(command, args); break;
                case "tly_trophytest": this.CmdTrophyTest(command, args); break;
                case "tly_testdonate": this.CmdTestDonate(command, args); break;
                case "tly_openhub": this.CmdOpenHub(command, args); break;
                case "tly_seasongoals": this.CmdSeasonGoals(command, args); break;
                case "tly_jpbudget": this.CmdJpBudget(command, args); break;
                case "tly_bundlesource": this.CmdBundleSource(command, args); break;
                case "tly_openshop": this.CmdOpenShop(command, args); break;
                case "tly_listupgrades": this.CmdListUpgrades(command, args); break;
                case "tly_buyupgrade": this.CmdBuyUpgrade(command, args); break;
                case "tly_boost": this.CmdBoost(command, args); break;
                case "tly_tv": this.CmdTv(command, args); break;
                case "tly_readbook": this.CmdReadBook(command, args); break;
                case "tly_wallet": TheLongestYear.DebugCommands.WalletDebugCommand.Run(this.Monitor, args); break;
                case "tly_dejavu": this.CmdDejaVu(command, args); break;
                case "tly_payvault": this.CmdPayVault(command, args); break;
                case "tly_hold": this.CmdHold(command, args); break;
                case "tly_difficulty": this.CmdDifficulty(command, args); break;
                case "tly_dumpbundles": this.CmdDumpBundles(command, args); break;
                case "tly_gatecheck": this.CmdGateCheck(command, args); break;
                case "tly_gateneeds": this.CmdGateNeeds(command, args); break;
                case "tly_goals": this.CmdGoals(command, args); break;
                case "tly_themepool": this.CmdThemePool(command, args); break;
                case "tly_playseason": this.CmdPlaySeason(command, args); break;
                case "tly_itemmodel": this.CmdItemModel(command, args); break;
                case "tly_dumpeffort": this.CmdDumpEffort(command, args); break;
                case "tly_pity": this.CmdPity(command, args); break;
                case "tly_here": this.CmdHere(command, args); break;
                case "tly_opencookbook":  this.CmdOpenCookbook(command, args); break;
                case "tly_opencraftbook": this.CmdOpenCraftbook(command, args); break;
                case "tly_activeeffects": this.CmdActiveEffects(command, args); break;
                case "tly_setstash":  this.CmdSetStash(command, args); break;
                case "tly_openstash": this.CmdOpenStash(command, args); break;
                case "tly_stashclear": this.CmdStashClear(command, args); break;
                case "tly_wipemeta":   this.CmdWipeMeta(command, args); break;
                case "tly_replayintro": this.CmdReplayIntro(command, args); break;
                case "tly_addpet":    this.CmdAddPet(command, args); break;
                case "tly_fixbridge": this.CmdFixBridge(command, args); break;
                case "tly_stashrod":  this.CmdStashRod(command, args); break;
                default:
                    this.Monitor.Log($"Debug bridge: unknown command '{command}'.", LogLevel.Warn);
                    break;
            }
        }

        private void CmdSelect(string command, string[] args)
        {
            if (!Context.IsWorldReady) { this.Monitor.Log("Load a save first.", LogLevel.Warn); return; }
            if (args.Length < 1) { this.Monitor.Log("Usage: tly_select <theme>", LogLevel.Warn); return; }
            // With the planning hub open this is the same as clicking the card, so an unattended
            // run never needs the mouse: the hub commits the pick (current week or the day-28
            // next-month pre-pick) and closes itself.
            if (Game1.activeClickableMenu is TheLongestYear.UI.WeeklyHubMenu hub)
            {
                if (hub.ConfirmByName(args[0]))
                    this.Monitor.Log($"tly_select: picked {args[0]} on the open planning hub.", LogLevel.Info);
                else
                    this.Monitor.Log($"tly_select: unknown theme '{args[0]}'. Options: {string.Join(", ", Enum.GetNames(typeof(TheLongestYear.Core.Theme)))}.", LogLevel.Warn);
                return;
            }
            // skipOfferCheck: this is a debug/playtest command; let it force any theme, not just
            // the seeded pair. The SelectedThemesThisMonth dedupe inside Select still applies.
            _runController.SelectByName(args[0], skipOfferCheck: true);
        }

        private void CmdSkipScene(string command, string[] args)
        {
            if (!Context.IsWorldReady) { this.Monitor.Log("Load a save first.", LogLevel.Warn); return; }
            if (Game1.activeClickableMenu is TheLongestYear.UI.Day28CutsceneMenu scene)
            {
                scene.SkipToEnd();
                this.Monitor.Log("tly_skipscene: finished the day-28 scene.", LogLevel.Info);
                return;
            }
            this.Monitor.Log($"tly_skipscene: no day-28 scene is open (activeClickableMenu={Game1.activeClickableMenu?.GetType().Name ?? "none"}).", LogLevel.Info);
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

        /// <summary>Dump the <c>NetWorldState</c> fields the 2026-08-26 keep/wipe audit ruled on
        /// (docs/superpowers/2026-08-26-networldstate-field-rulings.md). Read-only. Exists because
        /// the audit's fixes all live in WorldResetService over live Game1 statics, which the Core
        /// test project cannot construct — so the only way to verify a reset actually wiped what
        /// the table says is to print both sides of it and compare. Run before and after a
        /// tly_reset.</summary>
        /// <summary>Read-only difficulty probe. Prints the CONFIGURED steps next to the STAMPED
        /// ones, because those two disagree by design whenever the player has changed GMCM since
        /// the last reset, and a balance report is worthless without knowing which was in force.
        /// Same read-only shape as <see cref="CmdNetState"/>.</summary>
        /// <summary>Writes a Markdown catalogue of everything the engine can put on a board: every
        /// candidate bundle per room position, and for each one either its fixed item list or the
        /// pool it re-rolls from, plus the rules that decide quantities.
        ///
        /// Built from LIVE game data (Data/Bundles, Data/RandomBundles, Data/Crops, Data/Fish,
        /// Data/Locations, Data/Monsters ...) rather than a hand-written table, so it stays true
        /// for whatever content mods are installed and cannot drift from the generator.
        /// Diagnostics only: nothing is written to the save.</summary>
        /// <summary>Audits every season gate on the live board: for each bundle and each season,
        /// what the gate demands against how many of that bundle's ingredients can actually exist
        /// by that season's day 28.
        ///
        /// The question it answers is "hard but possible". IMPOSSIBLE means the gate demands more
        /// than the world can supply by then, which bricks the run; FREE means the gate demands
        /// nothing that season. Both are reported per bundle so a curated quota can be judged.
        ///
        /// LIMIT, stated plainly: this checks SEASON feasibility only. An item obtainable in Spring
        /// but needing a keg, a fish pond or a 10,000g tool upgrade counts as obtainable here.
        /// It proves nothing is impossible for calendar reasons; it does not prove anything is
        /// comfortable. Read-only.</summary>
        /// <summary>Prints the derived availability model for one item id, or for every
        /// ingredient of a named bundle. Diagnostics only, read-only.</summary>
        private void CmdItemModel(string command, string[] args)
        {
            if (_availability == null)
            {
                this.Monitor.Log("No availability model yet; load a save first.", LogLevel.Warn);
                return;
            }
            if (args.Length == 0)
            {
                this.Monitor.Log("Usage: tly_itemmodel <itemId|bundleName>", LogLevel.Info);
                return;
            }

            string target = string.Join(" ", args);
            // Read the live requirements the way tly_gatecheck does. The _requirements field is
            // not refreshed when a reset regenerates the board, so sourcing from it would report
            // due dates from the previous board and disagree with tly_gatecheck on the same save.
            var requirements = _runController?.Requirements ?? _requirements;
            BundleRequirement req = requirements?
                .FirstOrDefault(r => string.Equals(r.Name, target, StringComparison.OrdinalIgnoreCase));

            if (req != null)
            {
                this.Monitor.Log($"Bundle '{req.Name}' ({req.Kind}):", LogLevel.Info);
                foreach (string id in req.Ingredients)
                {
                    TheLongestYear.Core.ItemAvailability a = _availability.For(id);
                    string due = req.ItemSeasonPins != null
                        && req.ItemSeasonPins.TryGetValue(id, out TheLongestYear.Core.Season d)
                        ? d.ToString()
                        : "never";
                    this.Monitor.Log(
                        $"  {id}: due {due}; earliest {a.EarliestSeason}, effort {a.Effort} ({a.Source}), tier {TierLabel(id, a)} [{a.Basis}]",
                        LogLevel.Info);
                }
                return;
            }

            string itemId = target.StartsWith("(", StringComparison.Ordinal) ? target : $"(O){target}";
            TheLongestYear.Core.ItemAvailability single = _availability.For(itemId);
            this.Monitor.Log(
                $"{itemId}: earliest {single.EarliestSeason}, effort {single.Effort} ({single.Source}), tier {TierLabel(itemId, single)} [{single.Basis}]",
                LogLevel.Info);
        }

        /// <summary>The item's effort tier within the first theme pool that contains it, or
        /// "n/a" when no engine pool lists it (tiers are absolute effort bands).</summary>
        private string TierLabel(string itemId, TheLongestYear.Core.ItemAvailability availability)
        {
            if (_enginePools == null || _effortData == null) return "n/a";
            foreach (TheLongestYear.Core.Theme theme in Enum.GetValues(typeof(TheLongestYear.Core.Theme)))
            {
                IReadOnlyList<string> ids = ThemeEffortPools.IdsFor(theme, _enginePools, _effortData.Objects);
                if (!ids.Contains(itemId)) continue;
                return $"{EffortTiers.Tier(availability.Effort)} in {theme}";
            }
            return "n/a";
        }

        /// <summary>Reads every Data/Locations Fish row with a positive CatchLimit (the five
        /// legendaries in vanilla, but SVE-proof by construction like GameDataPools) and returns
        /// their qualified item ids, so FarmerReset can clear them from player.fishCaught on every
        /// reset. Degrades to an empty list on any read failure, same pattern as GameDataPools.</summary>
        private IReadOnlyList<string> ReadCatchLimitedFishIds()
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            try
            {
                foreach (var kv in Game1.content.Load<Dictionary<string, StardewValley.GameData.Locations.LocationData>>("Data/Locations"))
                {
                    StardewValley.GameData.Locations.LocationData loc = kv.Value;
                    foreach (StardewValley.GameData.Locations.SpawnFishData f in
                             loc?.Fish ?? (IList<StardewValley.GameData.Locations.SpawnFishData>)Array.Empty<StardewValley.GameData.Locations.SpawnFishData>())
                    {
                        if (f == null || f.CatchLimit <= 0) continue;
                        if (!string.IsNullOrEmpty(f.ItemId))
                            ids.Add(ItemRegistry.QualifyItemId(f.ItemId));
                        foreach (string randomId in f.RandomItemId ?? (IList<string>)Array.Empty<string>())
                            if (!string.IsNullOrEmpty(randomId))
                                ids.Add(ItemRegistry.QualifyItemId(randomId));
                    }
                }
            }
            catch (Exception ex)
            {
                this.Monitor.Log(
                    $"ReadCatchLimitedFishIds: data read failed ({ex.GetType().Name}: {ex.Message}), " +
                    "legendary fish will not be re-catchable across resets this session.",
                    LogLevel.Warn);
                return Array.Empty<string>();
            }
            return ids.ToList();
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
            TheLongestYear.Core.WeekMode mode = TheLongestYear.Core.WeekModes.For(step);
            _availability = TheLongestYear.Core.Availability.ItemAvailabilityBuilder.Build(
                _enginePools, seasonOverrides: _itemSeasonPins, effortData: _effortData,
                hasKitchen: _meta.State.HasUpgrade("keep_kitchen"),
                weekOverrides: _config.AvailabilityWeekOverrides, mode: mode, step: step);
            if (_runController != null) _runController.Availability = _availability;
            if (_boardBuilder != null) _boardBuilder.Availability = _availability;
            return _availability;
        }

        /// <summary>Jeff, 2026-08-28: "define can't exist; list all of the items in all of the bundles
        /// and when the first possible time you can get them is." One row per ingredient of every
        /// bundle on the live board: the model's earliest season (a hard floor only when the model
        /// derived it), the catalog's spawn seasons, the season the gate demands it, and the basis.</summary>
        private void CmdDumpAvailability(string command, string[] args)
        {
            if (!Context.IsWorldReady || _availability == null)
            {
                this.Monitor.Log("Load a save first (the availability model is derived from live game data).", LogLevel.Warn);
                return;
            }
            var requirements = _runController?.Requirements ?? _requirements;
            if (requirements == null || requirements.Count == 0) { this.Monitor.Log("No requirements on this save yet.", LogLevel.Warn); return; }

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("# The Longest Year: live board item availability");
            sb.AppendLine();
            sb.AppendLine($"Generated by `tly_dumpavailability` (mod version {this.ModManifest.Version}), loop seed {_meta?.Run?.Seed}.");
            sb.AppendLine();
            sb.AppendLine($"Week mode: **{_availability.Mode}** (spec 2026-08-28-obtainable-board, section 1).");
            sb.AppendLine();
            sb.AppendLine("**Week** is the pacing week (1 to 16): the week a normal player reasonably has the item. **Hard** is the first week the item can exist at all (facts: crop and forage seasons, fish seasons and locations, festival dates, Jeff's location rulings; falls back to Week when no hard week was placed). **Gate** the season a day-28 gate may first demand it under the current week mode (the deep mine and the Skull Cavern gate later than their goal week). **Placed** says who decided: `derived` (fish, crab-pot, metals from game data), `rule` (a Phase 2 rule: mines, geodes, monsters, artifacts, animals, machines, dishes, ponds, crops, forage, saplings), `judgement` (a Phase 2 row that is Jeff's own placement rather than a game-data fact -- a hand-ruled AvailabilityWeeks table entry or a note still awaiting his sign-off, listed again below so he can find every one), `override` (a pin or AvailabilityWeekOverrides), or `UNKNOWN` (nothing placed it: the gate treats it as Winter and it is listed at the end for Jeff to rule on). **Catalog seasons** are the spawn seasons the bundle catalog assigned (`any` = year-round). **Due** is the season the day-28 gate demands the item (per-item pin), the bundle's season (seasonal), or the quota ramp (pick-X-of-Y).");
            sb.AppendLine();
            var unknown = new List<string>();
            var judgement = new List<string>();
            foreach (BundleRequirement req in requirements)
            {
                string ramp = req.CumulativeRequiredBySeason != null ? $" ramp [{string.Join(", ", req.CumulativeRequiredBySeason)}]" : "";
                sb.AppendLine($"## {req.Name} ({req.Kind}, {req.NumberOfSlots} of {req.Ingredients.Count}{ramp})");
                sb.AppendLine();
                sb.AppendLine("| Item | Id | Week | Hard | Gate | Placed | Catalog seasons | Due | Effort | Basis |");
                sb.AppendLine("|---|---|---|---|---|---|---|---|---|---|");
                foreach (string id in req.Ingredients)
                {
                    TheLongestYear.Core.ItemAvailability a = _availability.For(id);
                    string placed = _availability.IsDerived(id) ? "derived"
                        : !_availability.IsPlaced(id) ? "UNKNOWN"
                        : a.Basis.Contains("override", StringComparison.Ordinal) ? "override"
                        : TheLongestYear.Core.AvailabilityWeeks.IsJudgementBasis(a.Basis) ? "judgement" : "rule";
                    if (placed == "UNKNOWN") unknown.Add($"- {DisplayName(id)} ({id}), in {req.Name}");
                    if (placed == "judgement") judgement.Add($"- {DisplayName(id)} ({id}), week {a.PacingWeek}, in {req.Name}");
                    string due = req.StretchLines.TryGetValue(id, out TheLongestYear.Core.Season stretchSeason) ? $"stretch ({stretchSeason})"
                        : req.Kind == BundleKind.Seasonal && req.SeasonalSeason.HasValue ? req.SeasonalSeason.Value.ToString()
                        : req.ItemSeasonPins != null && req.ItemSeasonPins.TryGetValue(id, out TheLongestYear.Core.Season d) ? d.ToString()
                        : req.Kind == BundleKind.Percentage ? "ramp" : "never";
                    string catalogSeasons = "any";
                    foreach (var cc in _catalog)
                        if (cc.Id == id) { catalogSeasons = cc.ObtainableSeasons == null || cc.ObtainableSeasons.Count == 4 ? "any" : string.Join("/", cc.ObtainableSeasons.OrderBy(x => (int)x)); break; }
                    sb.AppendLine($"| {DisplayName(id)} | {id} | {a.PacingWeek} | {a.HardWeekOrPacing} | {a.Gate} | {placed} | {catalogSeasons} | {due} | {a.Effort} ({a.Source}) | {a.Basis.Replace("|", "/")} |");
                }
                sb.AppendLine();
            }
            sb.AppendLine($"## Judgement rows ({judgement.Count})");
            sb.AppendLine();
            sb.AppendLine("Rows placed by Jeff's own ruling rather than a game-data fact: a hand-ruled AvailabilityWeeks table entry (\"table, ...\" basis) or a late-floor note still awaiting his sign-off (\"(for Jeff to confirm)\" basis). Not unknown -- they gate and appear on cards like any other rule -- but worth a second look.");
            sb.AppendLine();
            foreach (string line in judgement) sb.AppendLine(line);
            sb.AppendLine();
            sb.AppendLine($"## Unknown items ({unknown.Count})");
            sb.AppendLine();
            sb.AppendLine("Nothing placed these; the gate treats each as Winter. Jeff rules on every one (memory tly-sim-list-unknowns-each-run); a ruling becomes an AvailabilityWeeks row or an AvailabilityWeekOverrides default.");
            sb.AppendLine();
            foreach (string line in unknown) sb.AppendLine(line);
            sb.AppendLine();
            sb.AppendLine($"## Rejected overrides ({_availability.RejectedSeasonOverrides.Count})");
            sb.AppendLine();
            foreach (string id in _availability.RejectedSeasonOverrides) sb.AppendLine($"- {DisplayName(id)} ({id}): {_availability.For(id).Basis}");
            string fileName = args.Length > 0 && !string.IsNullOrWhiteSpace(args[0]) ? args[0] : "board-availability.md";
            string path = System.IO.Path.Combine(this.Helper.DirectoryPath, fileName);
            System.IO.File.WriteAllText(path, sb.ToString());
            this.Monitor.Log($"tly_dumpavailability: wrote {path} ({sb.Length:N0} chars, {requirements.Count} bundles, {judgement.Count} judgement row(s), {unknown.Count} unknown item(s)).", LogLevel.Info);
        }

        /// <summary><c>tly_gateneeds</c>: the season gate's remaining demand per bundle, from the
        /// same MissingForSeason the Season Goals page draws, after mirroring the ledger from the
        /// board. Read-only.</summary>
        private void CmdGateNeeds(string command, string[] args)
        {
            if (!Context.IsWorldReady || _runController == null) { this.Monitor.Log("Load a save first.", LogLevel.Warn); return; }
            RunState run = _meta.Run;
            TheLongestYear.Core.Season season = run.Season;
            TheLongestYear.Integration.ItemDonationSync.Reconcile(run);
            SlotLedger ledger = run.DonatedLedger();
            string nextSeason = season == TheLongestYear.Core.Season.Winter ? "the win" : $"{(TheLongestYear.Core.Season)((int)season + 1)} 1";
            int open = 0;
            foreach (BundleRequirement req in _runController.Requirements)
            {
                var (count, ids) = req.MissingForSeason(season, ledger);
                if (count == 0) continue;
                open++;
                string names = string.Join(", ", ids.Distinct().Select(id => $"{DisplayName(id)} ({id})"));
                this.Monitor.Log($"  {req.Name} ({req.Kind}, {ledger.FilledCount(req.BundleIndex)}/{req.NumberOfSlots} filled): needs {count} before {nextSeason}: {names}", LogLevel.Info);
            }
            bool vaultOk = VaultRules.IsVaultGateSatisfied(season, run, _meta.State);
            this.Monitor.Log($"  vault: paid {VaultRules.PaidCount(run)} of {VaultRules.SeasonOrdinal(season)} needed{(vaultOk ? " (satisfied)" : "")}", vaultOk ? LogLevel.Info : LogLevel.Warn);
            this.Monitor.Log($"tly_gateneeds: {season} day {run.DayOfMonth}: {open} bundle(s) still owed before {nextSeason}, {ledger.Count} slot(s) filled on the board.", LogLevel.Info);
        }

        /// <summary><c>tly_dumpeffort [fileName]</c>: the item effort review document, written to the
        /// mod folder like tly_dumpbundles (copy to docs/item-effort-model.md; gitignored).</summary>
        private void CmdDumpEffort(string command, string[] args)
        {
            if (!Context.IsWorldReady || _availability == null || _enginePools == null || _effortData == null)
            {
                this.Monitor.Log("Load a save first (the effort model is derived from live game data).", LogLevel.Warn);
                return;
            }
            string text = TheLongestYear.Debug.EffortDocWriter.Render(
                _enginePools, _effortData.Objects, _availability, this.ModManifest.Version.ToString());
            string fileName = args.Length > 0 && !string.IsNullOrWhiteSpace(args[0]) ? args[0] : "item-effort-model.md";
            string path = System.IO.Path.Combine(this.Helper.DirectoryPath, fileName);
            System.IO.File.WriteAllText(path, text);
            this.Monitor.Log($"tly_dumpeffort: wrote {path} ({text.Length:N0} chars).", LogLevel.Info);
        }

        private void CmdGateCheck(string command, string[] args)
        {
            if (!Context.IsWorldReady) { this.Monitor.Log("Load a save first.", LogLevel.Warn); return; }

            var requirements = _runController?.Requirements;
            if (requirements == null || requirements.Count == 0)
            {
                this.Monitor.Log("No requirements on this save yet.", LogLevel.Warn);
                return;
            }

            // Earliest season each item can exist: the curated pins merged over the pins derived
            // from live game data. Anything unpinned is treated as Spring-obtainable, which is the
            // same assumption the generator's own ramp clamp makes.
            var pins = new Dictionary<string, TheLongestYear.Core.Season>(StringComparer.Ordinal);
            MetaState state = _meta.State;
            try
            {
                TheLongestYear.Core.DifficultyProfile gateCheckDifficulty = state.BoardDifficulty(_config);
                BundleGenerationTuning tuning = TheLongestYear.Core.DifficultyTuning.Scale(
                    _config.PoolTuning, gateCheckDifficulty);
                foreach (var kv in new TheLongestYear.Loop.GameDataPools(this.Monitor)
                        .Build(tuning, TheLongestYear.Core.YearTwoCrops.ExcludedFor(
                            state.HasUpgrade, gateCheckDifficulty.Steps.ItemRarity))
                        .DerivedSeasonPins)
                    pins[kv.Key] = kv.Value;
            }
            catch (Exception ex)
            {
                this.Monitor.Log($"tly_gatecheck: could not derive season pins ({ex.Message}); using curated pins only.", LogLevel.Warn);
            }
            foreach (var kv in ParseItemSeasonPins())
                pins[kv.Key] = kv.Value;

            LogGateAudit(requirements, pins, "tly_gatecheck");
        }


        /// <summary><c>tly_playseason [goals]</c>: simulate a minimal compliant player for the CURRENT
        /// season (real-play audit, Jeff 2026-08-28: "I need REAL PLAY SIMULATION DATA"). Donates,
        /// through real CC slot flips plus the run ledger, exactly what every bundle's gate demands
        /// by this season's day 28 (nothing more), pays the vault bundles the season ordinal needs,
        /// and with <c>goals</c> also deposits the current week's goal slots. Then reports whether
        /// the season gate would pass. Follow with <c>tly_setday 28</c> and a sleep so the real gate
        /// runs and the next season's hub samples goals from what is actually left open.</summary>
        private void CmdPlaySeason(string command, string[] args)
        {
            if (!Context.IsWorldReady) { this.Monitor.Log("Load a save first.", LogLevel.Warn); return; }
            var requirements = _runController?.Requirements;
            if (requirements == null || requirements.Count == 0) { this.Monitor.Log("No requirements on this save.", LogLevel.Warn); return; }
            // "goalsonly": deposit this week's goal slots and nothing else (no gate work, no vault),
            // so a sim can play a goal-completing week without finishing the season's gate on day 1.
            bool goalsOnly = args.Length > 0 && string.Equals(args[0], "goalsonly", StringComparison.OrdinalIgnoreCase);
            bool chaseGoals = goalsOnly || (args.Length > 0 && string.Equals(args[0], "goals", StringComparison.OrdinalIgnoreCase));
            // "quarter <k>": donate only the first k/4 of this season's share, so a sim can spread the
            // season's donations across its four weeks. The four calls are cumulative: every call plans
            // from the same season-start baseline, so quarter 4 lands exactly where a plain call would.
            int quarter = 0;
            if (args.Length > 0 && string.Equals(args[0], "quarter", StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length < 2 || !int.TryParse(args[1], out quarter) || quarter < 1 || quarter > 4)
                {
                    this.Monitor.Log("Usage: tly_playseason quarter <1-4>", LogLevel.Warn);
                    return;
                }
            }
            bool quarterMode = quarter > 0;

            RunState run = _meta.Run;
            TheLongestYear.Core.Season season = run.Season;
            var worldState = Game1.netWorldState?.Value;
            if (worldState?.BundleData == null || worldState.Bundles?.FieldDict == null) { this.Monitor.Log("No bundle data.", LogLevel.Warn); return; }

            // Bundle name -> (index, concrete slots in board order, duplicates kept) from the live board.
            var lines = new Dictionary<string, (int Index, List<BundleSlot> Slots)>(StringComparer.Ordinal);
            foreach (var kvp in worldState.BundleData)
            {
                ParsedBundle parsed = BundleParsing.Parse(kvp.Key, kvp.Value);
                var slots = new List<BundleSlot>();
                for (int i = 0; i < parsed.Ingredients.Count; i++)
                {
                    string refId = parsed.Ingredients[i].ItemRef;
                    if (BundleParsing.IsCategoryRef(refId)) continue;
                    slots.Add(new BundleSlot(i, BundleParsing.NormalizeItemId(refId)));
                }
                if (!lines.ContainsKey(parsed.Name)) lines[parsed.Name] = (parsed.Index, slots);
            }

            static bool Flip(int bundleIndex, int ingredientIndex)
                => TheLongestYear.Integration.CcSlotWriter.TryFill(bundleIndex, ingredientIndex);

            TheLongestYear.Integration.ItemDonationSync.Reconcile(run);
            SlotLedger donated = run.DonatedLedger();
            int flipped = 0;
            var log = new List<string>();

            // Donation order for one bundle: due-now items first (PerItem), obtainable-by-now items
            // first (Percentage), anything undonated (Seasonal).
            IEnumerable<string> Candidates(BundleRequirement req) => req.Kind switch
            {
                BundleKind.PerItem => req.ItemSeasonPins
                    .Where(kv => (int)kv.Value <= (int)season).OrderBy(kv => (int)kv.Value).Select(kv => kv.Key),
                _ => req.Ingredients
                    .OrderBy(id => Enumerable.Range(0, (int)season + 1).Any(s => _runController.IsObtainableInSeason(id, (TheLongestYear.Core.Season)s)) ? 0 : 1),
            };

            // The first open slot of the bundle that wants one of the candidate ids, candidate order.
            static BundleSlot? FirstOpenSlot(IEnumerable<string> candidates, int bundleIndex, List<BundleSlot> slots, SlotLedger ledger)
            {
                foreach (string id in candidates)
                    foreach (BundleSlot s in slots)
                        if (s.ItemId == id && !ledger.IsFilled(bundleIndex, s.IngredientIndex)) return s;
                return null;
            }

            // The bundle's whole season share as slot picks against ONE simulated ledger shared by
            // every bundle (mirroring the plain mode's sequential loop). Slots are per bundle now, so
            // two bundles that both list Salmonberry each plan their own slot, and a doubled id in one
            // bundle plans both of its slots.
            List<BundleSlot> PlanShare(BundleRequirement req, int bundleIndex, List<BundleSlot> slots, SlotLedger sim)
            {
                var picks = new List<BundleSlot>();
                int guard = 0;
                while (!req.IsSatisfiedAtSeasonEnd(season, sim) && guard++ < 32)
                {
                    BundleSlot? pick = FirstOpenSlot(Candidates(req), bundleIndex, slots, sim);
                    if (pick == null) break;
                    sim.Add(bundleIndex, pick.Value.IngredientIndex, pick.Value.ItemId);
                    picks.Add(pick.Value);
                }
                return picks;
            }

            int cumulative = 0;
            int planCount = 0;
            if (quarterMode)
            {
                // Quarter 1 re-baselines (it marks the season start, and survives a tly_reset); later
                // quarters reuse that baseline so their shares match.
                if (quarter == 1 || _playSeasonBaseline?.Season != season || _playSeasonBaseline?.Donated == null)
                {
                    _playSeasonBaseline = (season, donated.Entries.Select(e => new DonatedSlot { BundleIndex = e.BundleIndex, IngredientIndex = e.IngredientIndex, ItemId = e.ItemId }).ToList());
                    _playSeasonDonatedThisSeason = 0;
                }
                // Every slot the season demands, planned bundle by bundle in the same order the plain
                // mode flips them, against ONE simulated ledger that starts at the season baseline and
                // grows as each bundle is planned (so a shared item is planned once, as before).
                var sim = new SlotLedger(_playSeasonBaseline.Value.Donated);
                var perBundle = new List<List<(BundleRequirement Req, int BundleIndex, int SlotIndex, string ItemId)>>();
                foreach (BundleRequirement req in requirements)
                {
                    if (!lines.TryGetValue(req.Name, out var bundle)) { log.Add($"  {req.Name}: not on the live board, skipped"); continue; }
                    var forBundle = new List<(BundleRequirement, int, int, string)>();
                    foreach (BundleSlot pick in PlanShare(req, bundle.Index, bundle.Slots, sim))
                        forBundle.Add((req, bundle.Index, pick.IngredientIndex, pick.ItemId));
                    perBundle.Add(forBundle);
                }
                // Flatten ROUND-ROBIN, not bundle by bundle: pass 1 takes each bundle's first planned
                // slot in board order, pass 2 each bundle's second, and so on. A flat bundle-by-bundle
                // list put the whole Boiler Room share inside quarter 1, which emptied Mining's weekly
                // goal pool in week 1 of every season; round-robin spreads every room across the four
                // quarters. The quarter is still a prefix of the whole list, not a share per bundle.
                var seasonPlan = new List<(BundleRequirement Req, int BundleIndex, int SlotIndex, string ItemId)>();
                int deepest = 0;
                foreach (var forBundle in perBundle) deepest = Math.Max(deepest, forBundle.Count);
                for (int pass = 0; pass < deepest; pass++)
                    foreach (var forBundle in perBundle)
                        if (pass < forBundle.Count) seasonPlan.Add(forBundle[pass]);

                planCount = seasonPlan.Count;
                cumulative = (int)Math.Ceiling(planCount * quarter / 4.0);
                // Budget the steps this quarter still owes, and spend that budget only on steps that
                // are actually undonated. A flat Take(cumulative) donated nothing in goals mode: goal
                // deposits land after the baseline is taken, so the quarter's whole prefix could
                // already be in the ledger and the quarter flipped zero slots while reporting a
                // cumulative position. Skipping a donated step without consuming the budget lets the
                // quarter reach further down the plan and still do its share of real work.
                int budget = Math.Max(0, cumulative - _playSeasonDonatedThisSeason);
                foreach (var step in seasonPlan)
                {
                    if (budget <= 0) break;
                    if (donated.IsFilled(step.BundleIndex, step.SlotIndex)) continue;
                    if (!Flip(step.BundleIndex, step.SlotIndex)) { log.Add($"  {step.Req.Name}: could not flip slot for {DisplayName(step.ItemId)}"); continue; }
                    run.RecordDonation(step.BundleIndex, step.SlotIndex, step.ItemId);
                    donated = run.DonatedLedger();
                    flipped++;
                    budget--;
                    _playSeasonDonatedThisSeason++;
                    log.Add($"  {step.Req.Name} ({step.Req.Kind}): donated {DisplayName(step.ItemId)} ({step.ItemId})");
                }
            }

            foreach (BundleRequirement req in requirements)
            {
                if (goalsOnly || quarterMode) break;
                if (!lines.TryGetValue(req.Name, out var bundle)) { log.Add($"  {req.Name}: not on the live board, skipped"); continue; }
                int guard = 0;
                while (!req.IsSatisfiedAtSeasonEnd(season, donated) && guard++ < 32)
                {
                    BundleSlot? pick = FirstOpenSlot(Candidates(req), bundle.Index, bundle.Slots, donated);
                    if (pick == null) { log.Add($"  {req.Name}: nothing left to donate but the gate is still open"); break; }
                    if (!Flip(bundle.Index, pick.Value.IngredientIndex)) { log.Add($"  {req.Name}: could not flip slot for {DisplayName(pick.Value.ItemId)}"); break; }
                    run.RecordDonation(bundle.Index, pick.Value.IngredientIndex, pick.Value.ItemId);
                    donated = run.DonatedLedger();
                    flipped++;
                    log.Add($"  {req.Name} ({req.Kind}): donated {DisplayName(pick.Value.ItemId)} ({pick.Value.ItemId}) slot {pick.Value.IngredientIndex}");
                }
            }

            if (chaseGoals && !quarterMode)
            {
                foreach (BonusSlot slot in run.CurrentWeekBonusSlots)
                {
                    if (Flip(slot.BundleIndex, slot.IngredientIndex))
                    {
                        run.RecordDonation(slot.BundleIndex, slot.IngredientIndex, slot.ItemId);
                        WeeklyGoalCredit.RecordDeposit(run.CurrentWeekBonusSlots, slot.BundleIndex, slot.IngredientIndex);
                        flipped++;
                        log.Add($"  goal: deposited {DisplayName(slot.ItemId)} into {slot.BundleName}");
                    }
                }
                _questService?.OnItemDonated();
            }

            // Vault: the season ordinal (1 by Spring, 2 by Summer ...), cheapest first.
            int needVault = VaultRules.SeasonOrdinal(season);
            foreach (int idx in TheLongestYear.Integration.VaultBundleMap.Indices())
            {
                if (goalsOnly) break;
                if (quarterMode && quarter < 4) break;
                if (run.VaultBundlesPaid.Count >= needVault) break;
                if (run.VaultBundlesPaid.Contains(idx)) continue;
                if (Flip(idx, 0))
                {
                    DonationService.Active?.OnVaultBundlePaid(idx);
                    log.Add($"  vault: paid bundle {idx} ({TheLongestYear.Integration.VaultBundleMap.GoldForIndex(idx):N0}g)");
                }
            }

            donated = run.DonatedLedger();
            bool vaultOk = VaultRules.IsVaultGateSatisfied(season, run, _meta.State);
            bool gateOk = BundleGate.IsSatisfied(season, donated, requirements, vaultOk);
            this.Monitor.Log(
                quarterMode
                    ? $"tly_playseason: {season} quarter {quarter}, {flipped} slot(s) flipped, {_playSeasonDonatedThisSeason} donated this season, plan position {cumulative} of {planCount}"
                    : $"tly_playseason: {season} day {run.DayOfMonth}, {flipped} slot(s) flipped{(chaseGoals ? " (goals chased)" : "")}, vault {run.VaultBundlesPaid.Count}/{needVault}.",
                LogLevel.Info);
            foreach (string l in log) this.Monitor.Log(l, LogLevel.Info);
            if (quarterMode && quarter < 4) return;
            var open = requirements.Where(r => !r.IsSatisfiedAtSeasonEnd(season, donated)).Select(r => r.Name).ToList();
            // goalsonly never touches the vault or a gate bundle, so its failure is normally "vault
            // unpaid and nothing else": an empty bundle list after "open bundles:" read like a bug.
            string failDetail = open.Count == 0
                ? (vaultOk ? "(no open bundles)" : "(vault unpaid, no open bundles)")
                : $"vault {vaultOk}, open bundles: {string.Join(", ", open)}";
            this.Monitor.Log(
                gateOk ? $"tly_playseason: {season} gate WOULD PASS. Ledger {donated.Count} slot(s)."
                       : $"tly_playseason: {season} gate WOULD FAIL: {failDetail}",
                gateOk ? LogLevel.Info : LogLevel.Error);
        }

        /// <summary><c>tly_themepool [theme]</c>: rule C's askable count per theme for the current
        /// week, or the full candidate list for one theme with due/filler, effort, tier and weight.</summary>
        private void CmdThemePool(string command, string[] args)
        {
            if (!Context.IsWorldReady) { this.Monitor.Log("Load a save first.", LogLevel.Warn); return; }
            if (_runController == null) { this.Monitor.Log("No run controller yet.", LogLevel.Warn); return; }

            RunState run = _meta.Run;
            TheLongestYear.Core.Season season = run.Season;
            int week = run.WeekOfYear;
            this.Monitor.Log(
                $"tly_themepool: {season} week {week}, filler allowance {_config.FillerAllowanceFor(season)}, "
                + $"selected this month [{string.Join(",", run.SelectedThemesThisMonth)}].",
                LogLevel.Info);

            if (args.Length == 0)
            {
                foreach (TheLongestYear.Core.Theme theme in Enum.GetValues(typeof(TheLongestYear.Core.Theme)))
                {
                    int askable = _runController.AskableCount(theme, season, week);
                    string mark = askable >= SelectionService.MinAskableToOffer ? "offerable" : "not offered";
                    this.Monitor.Log($"  {theme}: askable {askable} ({mark})", LogLevel.Info);
                }
                return;
            }

            if (!Enum.TryParse(args[0], ignoreCase: true, out TheLongestYear.Core.Theme picked))
            {
                this.Monitor.Log("Usage: tly_themepool [theme]", LogLevel.Info);
                return;
            }
            IReadOnlyList<GoalWeight> weights = _runController.DescribeGoalPool(picked, season, _meta.Run.WeekOfYear, out IReadOnlyList<BonusSlot> pool);
            var weightById = weights.ToDictionary(w => w.ItemId, w => w, StringComparer.Ordinal);
            this.Monitor.Log($"  {picked}: {pool.Count} open line(s), askable {_runController.AskableCount(picked, season, week)}", LogLevel.Info);
            foreach (BonusSlot slot in pool.OrderByDescending(s => s.Due).ThenBy(s => s.ItemId, StringComparer.Ordinal))
            {
                GoalWeight w = weightById[slot.ItemId];
                string effort = w.Effort.HasValue ? w.Effort.Value.ToString() : "price";
                this.Monitor.Log(
                    $"    {(slot.Due ? "DUE   " : "filler")} {DisplayName(slot.ItemId)} ({slot.ItemId}) effort {effort} tier {w.Tier} weight {w.Weight}  [{slot.BundleName} #{slot.BundleIndex}/{slot.IngredientIndex}]",
                    LogLevel.Info);
            }
        }

        /// <summary><c>tly_goals [season] [weekOfYear]</c>: the weekly goals each theme would offer on the
        /// live board, through the same sampler the planning hub uses, so a season's goals can be
        /// audited from the log without sleeping to that season and opening the hub. Defaults to
        /// the run's own season and week; another season defaults to its week 1.</summary>
        private void CmdGoals(string command, string[] args)
        {
            if (!Context.IsWorldReady) { this.Monitor.Log("Load a save first.", LogLevel.Warn); return; }
            if (_runController == null) { this.Monitor.Log("No run controller yet.", LogLevel.Warn); return; }

            RunState run = _meta.Run;
            TheLongestYear.Core.Season season = run.Season;
            if (args.Length > 0 && !Enum.TryParse(args[0], ignoreCase: true, out season))
            {
                this.Monitor.Log("Usage: tly_goals [spring|summer|fall|winter] [weekOfYear]", LogLevel.Info);
                return;
            }
            int week = season == run.Season ? run.WeekOfYear : Calendar.WeekOfYear((int)season, 1);
            if (args.Length > 1 && int.TryParse(args[1], out int explicitWeek))
                week = explicitWeek;

            this.Monitor.Log($"tly_goals: {season} week {week} (run season {run.Season} day {run.DayOfMonth}, loop seed {run.Seed}).", LogLevel.Info);
            foreach (TheLongestYear.Core.Theme theme in Enum.GetValues(typeof(TheLongestYear.Core.Theme)))
            {
                IReadOnlyList<BonusSlot> slots = _runController.SampleSlotsForTheme(theme, season, week);
                this.Monitor.Log($"  {theme}: {slots.Count} goal(s)", LogLevel.Info);
                foreach (BonusSlot slot in slots)
                {
                    string quality = slot.Quality > 0 ? $" q{slot.Quality}" : "";
                    this.Monitor.Log(
                        $"    - {DisplayName(slot.ItemId)} ({slot.ItemId}) x{slot.Stack}{quality}  [{slot.BundleName} #{slot.BundleIndex}/{slot.IngredientIndex}]",
                        LogLevel.Info);
                }
            }
        }

        /// <summary>The season-gate audit shared by <c>tly_gatecheck</c> (live board) and
        /// <c>tly_genbundles</c> (diagnostic board): demanded vs obtainable per season, IMPOSSIBLE
        /// and FREE flags, and the blockers named per gate. <paramref name="pins"/> is the merged
        /// earliest-season table (derived under curated); anything unpinned counts as Spring.</summary>
        /// <param name="vanillaOnlyRecipes">Names of the bundles whose recipe had no pool of its
        /// own and offered their vanilla items only; tagged [no recipe]. Null when the caller has
        /// no recipe data (the live-board audit).</param>
        private void LogGateAudit(
            IReadOnlyList<BundleRequirement> requirements,
            IReadOnlyDictionary<string, TheLongestYear.Core.Season> pins,
            string label,
            IReadOnlySet<string> vanillaOnlyRecipes = null)
        {
            int impossible = 0, free = 0, tight = 0, stretchLineCount = 0, noHardItemCount = 0, springTightCount = 0;
            var lines = new List<string>();
            var blocked = new List<string>();

            foreach (BundleRequirement req in requirements.OrderBy(r => r.Theme).ThenBy(r => r.Name, StringComparer.Ordinal))
            {
                int[] obtainable = new int[Calendar.MonthsPerYear];
                for (int season = 0; season < Calendar.MonthsPerYear; season++)
                    obtainable[season] = req.Ingredients.Count(id =>
                        !pins.TryGetValue(id, out TheLongestYear.Core.Season pinned) || (int)pinned <= season);

                int[] demanded = new int[Calendar.MonthsPerYear];
                for (int season = 0; season < Calendar.MonthsPerYear; season++)
                    demanded[season] = DemandAtSeason(req, (TheLongestYear.Core.Season)season, pins);

                var cells = new List<string>();
                string worst = "ok";
                for (int season = 0; season < Calendar.MonthsPerYear; season++)
                {
                    string flag = "";
                    if (demanded[season] > obtainable[season])
                    {
                        flag = " IMPOSSIBLE";
                        worst = "IMPOSSIBLE";
                        impossible++;
                        // Name the culprits: an audit that says "4/3" without saying WHICH
                        // ingredient is out of reach leaves the reader to re-derive it by hand.
                        string blockers = string.Join(", ", req.Ingredients
                            .Where(id => pins.TryGetValue(id, out var p2) && (int)p2 > season)
                            .Select(id => _availability != null
                                ? $"{DisplayName(id)} (needs {pins[id]}) [{_availability.For(id).Basis}]"
                                : $"{DisplayName(id)} (needs {pins[id]})"));
                        if (blockers.Length > 0)
                            blocked.Add($"      {req.Name} at {(TheLongestYear.Core.Season)season}: blocked by {blockers}");
                    }
                    else if (demanded[season] == obtainable[season] && demanded[season] > 0)
                    {
                        flag = " tight";
                        if (worst == "ok") worst = "tight";
                        tight++;
                    }
                    cells.Add($"{(TheLongestYear.Core.Season)season}: {demanded[season]}/{obtainable[season]}{flag}");
                }
                if (demanded[Calendar.MonthsPerYear - 1] == 0) { free++; worst = "FREE ALL YEAR"; }

                var tags = new List<string>();
                if (vanillaOnlyRecipes != null && vanillaOnlyRecipes.Contains(req.Name))
                    tags.Add("[no recipe]");
                foreach (KeyValuePair<string, TheLongestYear.Core.Season> stretch in req.StretchLines.OrderBy(kv => kv.Key, StringComparer.Ordinal))
                {
                    tags.Add($"[stretch: {DisplayName(stretch.Key)} {stretch.Value}]");
                    stretchLineCount++;
                }

                bool rolled = req.Kind != BundleKind.Seasonal;
                if (_availability != null && BundleSlotFiller.HardItemRuleApplies(_availability)
                    && req.NumberOfSlots >= BundleSlotFiller.MinSlotsForHardItem && rolled
                    && !req.Ingredients.Any(id => EffortTiers.IsHard(_availability.For(id).Effort)))
                {
                    tags.Add("[no hard item]");
                    noHardItemCount++;
                }

                int springDemanded = demanded[(int)TheLongestYear.Core.Season.Spring];
                int springObtainable = obtainable[(int)TheLongestYear.Core.Season.Spring];
                if (springDemanded == springObtainable && springDemanded > 0)
                {
                    tags.Add("[spring tight]");
                    springTightCount++;
                }

                string tagText = tags.Count > 0 ? "  " + string.Join(" ", tags) : "";
                lines.Add($"  [{worst,-13}] {req.Name,-26} {req.Kind,-10} X={req.NumberOfSlots} Y={req.Ingredients.Count}  {string.Join("  |  ", cells)}{tagText}");
            }

            this.Monitor.Log($"=== {label}: season gate audit (demanded / obtainable, by day 28 of each season) ===", LogLevel.Info);
            foreach (string line in lines)
                this.Monitor.Log(line, LogLevel.Info);

            if (blocked.Count > 0)
            {
                this.Monitor.Log("  Ingredients out of reach at the gate that demands them:", LogLevel.Error);
                foreach (string b in blocked)
                    this.Monitor.Log(b, LogLevel.Error);
            }

            this.Monitor.Log(
                $"  Vault gate: pay at least 1 money bundle by Spring 28, 2 by Summer, 3 by Fall, 4 by Winter " +
                $"(cheapest first: {string.Join(", ", VaultLadder())}). Owning '{VaultRules.KeepBusUnlockedId}' satisfies it outright.",
                LogLevel.Info);

            this.Monitor.Log(
                impossible > 0
                    ? $"  {label} RESULT: {impossible} IMPOSSIBLE season gate(s) -- these brick the run and must be fixed."
                    : $"  {label} RESULT: no impossible gates. {tight} tight (demands everything obtainable by then), {free} bundle(s) never gated.",
                impossible > 0 ? LogLevel.Error : LogLevel.Info);
            this.Monitor.Log(
                $"  {label} RESULT: {stretchLineCount} stretch line(s), {noHardItemCount} without a hard item, {springTightCount} Spring tight.",
                LogLevel.Info);
            this.Monitor.Log(
                "  NOTE: this checks CALENDAR feasibility only. An item that exists in Spring but needs a keg, "
                + "a fish pond or a tool upgrade counts as obtainable here.",
                LogLevel.Info);
        }

        /// <summary>How many distinct ingredients this bundle's gate demands by the end of
        /// <paramref name="season"/>, expressed the same way for all three bundle kinds so they can
        /// be compared against obtainability on one scale.</summary>
        private static int DemandAtSeason(
            BundleRequirement req, TheLongestYear.Core.Season season,
            IReadOnlyDictionary<string, TheLongestYear.Core.Season> pins)
        {
            switch (req.Kind)
            {
                case BundleKind.Seasonal:
                    // Everything, but only once its named season has arrived.
                    return (int)req.SeasonalSeason.Value <= (int)season ? req.Ingredients.Count : 0;

                case BundleKind.PerItem:
                    // Each pinned ingredient is due at its own pin.
                    return req.ItemSeasonPins.Count(kv => (int)kv.Value <= (int)season);

                case BundleKind.Percentage:
                    return req.CumulativeRequiredBySeason[(int)season];

                default:
                    return 0;
            }
        }

        private static IEnumerable<string> VaultLadder()
            => VaultRules.VaultIndices.Select(i => $"{VaultRules.GoldForIndex(i):N0}g");

        private void CmdDumpBundles(string command, string[] args)
        {
            if (!Context.IsWorldReady)
            {
                this.Monitor.Log("Load a save first (the pools are derived from live game data).", LogLevel.Warn);
                return;
            }

            MetaState state = _meta.State;
            TheLongestYear.Core.DifficultyProfile difficulty = state.BoardDifficulty(_config);
            BundleGenerationTuning tuning = TheLongestYear.Core.DifficultyTuning.Scale(_config.PoolTuning, difficulty);
            ItemPools pools = new TheLongestYear.Loop.GameDataPools(this.Monitor)
                .Build(tuning, TheLongestYear.Core.YearTwoCrops.ExcludedFor(state.HasUpgrade, difficulty.Steps.ItemRarity));
            pools = TheLongestYear.Core.RarityBias.Apply(pools, difficulty.RarityBias, _config.RarityThresholds);

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("# The Longest Year: engine bundle catalogue");
            sb.AppendLine();
            sb.AppendLine("Generated by `tly_dumpbundles` from live game data.");
            sb.AppendLine();
            sb.AppendLine("The engine picks ONE candidate per room position. For a bundle whose theme it recognises it discards the vanilla item list and re-rolls from a pool; otherwise it keeps vanilla's items exactly. Both kinds are listed here.");
            sb.AppendLine();

            AppendQuantityRules(sb, tuning, difficulty);
            AppendCandidates(sb, pools);
            AppendAuthored(sb);
            AppendPools(sb, pools);
            AppendThemePools(sb);

            string fileName = args.Length > 0 && !string.IsNullOrWhiteSpace(args[0])
                ? args[0] : "engine-bundle-catalogue.md";
            string path = System.IO.Path.Combine(this.Helper.DirectoryPath, fileName);
            System.IO.File.WriteAllText(path, sb.ToString());
            this.Monitor.Log($"tly_dumpbundles: wrote {path} ({sb.Length:N0} chars).", LogLevel.Info);
        }

        private void AppendQuantityRules(System.Text.StringBuilder sb, BundleGenerationTuning t, TheLongestYear.Core.DifficultyProfile d)
        {
            sb.AppendLine("## How quantities are decided");
            sb.AppendLine();
            sb.AppendLine("For a re-rolled slot the engine chooses the quantity itself, by pool:");
            sb.AppendLine();
            sb.AppendLine("| Pool | Quantity asked |");
            sb.AppendLine("|---|---|");
            sb.AppendLine("| Seasonal Crops, Seasonal Forage, Fish, Crab Pot, Metals, Artisan Goods | 1 |");
            sb.AppendLine($"| Quality Crops | {t.QualityCropStack}, always at gold quality |");
            sb.AppendLine($"| Monster Drops, item under {t.CheapPriceCeiling}g | {t.CheapMinStack} to {t.CheapMaxStack} |");
            sb.AppendLine($"| Monster Drops, item under {t.MidPriceCeiling}g | {t.MidMinStack} to {t.MidMaxStack} |");
            sb.AppendLine($"| Monster Drops, dearer than that | {t.DearMinStack} to {t.DearMaxStack} |");
            sb.AppendLine($"| Seasonal Forage, big-ask roll (one slot per bundle, {t.LargeQuantityForageChance:P0} chance) | {t.LargeQuantityMinStack} to {t.LargeQuantityMaxStack} |");
            sb.AppendLine();
            sb.AppendLine($"Every quantity above, and every quantity kept from vanilla, is then multiplied by the stack-size difficulty dial (currently **x{d.StackFactor}**, step {d.Steps.StackSize}), rounded away from zero, floored at 1 and **capped at 99**. Money bundles are never scaled.");
            sb.AppendLine();
            sb.AppendLine($"Quality: a re-rolled crop, forage or fish slot rolls {t.GoldQualityChance:P1} for gold then {t.SilverQualityChance:P1} for silver, and only ever on an item the game itself can star.");
            sb.AppendLine();
        }

        private void AppendCandidates(System.Text.StringBuilder sb, ItemPools pools)
        {
            sb.AppendLine("## Bundles by room");
            sb.AppendLine();
            // The engine's FULL candidate set, authored bundles included. Reading the vanilla pool
            // alone understates every room: the mod's own bundles are widened into every position
            // of their room, which is exactly what gives several positions their alternates.
            TheLongestYear.Core.DifficultyProfile candidateDifficulty = _meta.State.BoardDifficulty(_config);
            var engine = new TheLongestYear.Loop.BundleEngine(
                this.Monitor, _config.PoolTuning, _config.EnableNonObjectDonations, _config.RarityThresholds,
                TheLongestYear.Core.YearTwoCrops.ExcludedFor(_meta.State.HasUpgrade, candidateDifficulty.Steps.ItemRarity),
                candidateDifficulty);
            engine.Availability = _availability;
            int candidateSeed = BundleEngineSeed.For(
                unchecked((ulong)Game1.player.UniqueMultiplayerID), _meta.State.EffectiveBundleSeedLoop);
            var rooms = engine.BuildCandidatePools(pools, candidateSeed);

            foreach (var room in rooms.OrderBy(r => r.Key, System.StringComparer.Ordinal))
            {
                sb.AppendLine($"### {room.Key}");
                sb.AppendLine();
                for (int position = 0; position < room.Value.Count; position++)
                {
                    var candidates = room.Value[position];
                    if (candidates.Count == 0) continue;
                    // Vanilla folds every alternate BundleSets entry in as its own candidate, so the
                    // same bundle can appear several times at one position with an identical
                    // description. Collapse those: the reader wants the distinct possibilities.
                    var seen = new System.Collections.Generic.HashSet<string>(System.StringComparer.Ordinal);
                    var described = new System.Collections.Generic.List<string>();
                    foreach (BundleSpec c in candidates)
                    {
                        DomainMatch match = PoolDomainClassifier.Classify(c, pools);
                        int shown = c.PickCount > 0 ? System.Math.Min(c.PickCount, c.Slots.Count) : c.Slots.Count;
                        string body;
                        if (match.Domain == PoolDomain.Recipe)
                        {
                            // "the Recipe pool" is not a pool anyone can look up: name the parts
                            // the bundle actually draws from (Jeff, 2026-08-29).
                            TheLongestYear.Core.PoolRecipe recipe = BundleSlotFiller.RecipeFor(c, pools, _availability);
                            body = recipe.IsVanillaOnly
                                ? $"  - Re-rolls from: {TheLongestYear.Core.BundlePoolRecipes.VanillaOnlyLabel} (no pool of its own): {DescribeSlots(c)}"
                                : $"  - Re-rolls from: {string.Join(", ", recipe.Parts.Select(p => p.Label))}."
                                    + " No item is asked for twice across the board; see the pool tables below.";
                        }
                        else if (match.Domain != PoolDomain.None)
                        {
                            string season = match.Season != null ? $", {match.Season} only (items specific to that season)" : "";
                            string rule = match.Domain == PoolDomain.Fish
                                ? (TheLongestYear.Core.FishBundleCandidates.IsNightFishingBundle(c)
                                    ? " Only fish that cannot be caught before 6pm, plus at most one Night Market fish."
                                    : " Only fish sharing a spawn location with the bundle's vanilla fish.")
                                : " Any item in that pool can appear.";
                            body = $"  - Re-rolls from the **{match.Domain}** pool{season}.{rule} No item is asked for twice across the board; see the pool tables below.";
                        }
                        else
                        {
                            body = $"  - Keeps vanilla's items: {DescribeSlots(c)}";
                        }
                        string entry = $"- **{c.Name}** — shows {shown}, needs {c.NumberOfSlots}"
                            + System.Environment.NewLine + body;
                        if (seen.Add(entry))
                            described.Add(entry);
                    }

                    sb.AppendLine($"**Position {position}** — {described.Count} possible bundle(s):");
                    sb.AppendLine();
                    foreach (string entry in described)
                        sb.AppendLine(entry);
                    sb.AppendLine();
                }
            }
        }

        private static string DescribeSlots(BundleSpec spec)
            => string.Join(", ", spec.Slots.Select(s =>
            {
                if (s.ItemId == "-1") return $"{s.Stack}g";
                string q = s.Quality > 0 ? $" (quality {s.Quality})" : "";
                return $"{DisplayName(s.ItemId)} x{s.Stack}{q}";
            }));

        private static string DisplayName(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return "(none)";
            if (BundleParsing.IsCategoryRef(itemId)) return $"any item in category {itemId}";
            try
            {
                var data = StardewValley.ItemRegistry.GetData(BundleParsing.NormalizeItemId(itemId));
                return data != null ? data.DisplayName : itemId;
            }
            catch { return itemId; }
        }

        /// <summary>Activity-themes spec (2026-08-28): the simulated line counts that drove the
        /// merge from six narrow themes to three, and the effort override table (ships empty).</summary>
        private void AppendThemePools(System.Text.StringBuilder sb)
        {
            sb.AppendLine("## Theme pools");
            sb.AppendLine();
            sb.AppendLine("Spelunking, Artisan and Kitchen take goals by item kind from anywhere on the board. Open lines per board from the spec's 100,000-board simulation (uniform per slot, no repeats in a room):");
            sb.AppendLine();
            sb.AppendLine("| Domain | Lines per board (avg) | Boards with none |");
            sb.AppendLine("|---|---|---|");
            sb.AppendLine("| Monster drops | 2.4 | 27% |");
            sb.AppendLine("| Artifacts | 3.4 | 23% |");
            sb.AppendLine("| Animal products | 3.5 | 5% |");
            sb.AppendLine("| Minerals + gems | 7.4 | (57% under 8) |");
            sb.AppendLine("| Cooked dishes | 7.4 | |");
            sb.AppendLine("| Artisan goods | 13.5 | |");
            sb.AppendLine("| Spelunking (minerals + gems + monster drops + artifacts) | 13.1 | 1.6% under 4 |");
            sb.AppendLine("| Kitchen (animal products + cooked dishes) | 24.5 | |");
            sb.AppendLine();
            sb.AppendLine("A Spring week asks for 4 goals, Winter for 7. A theme is only offered when it can ask for 2 or more this week (rule C); the per-item effort tiers that weight the draw are in `item-effort-model.md` (`tly_dumpeffort`).");
            sb.AppendLine();
            sb.AppendLine("### Effort overrides");
            sb.AppendLine();
            sb.AppendLine("None. The curated effort override table ships empty; a tier that looks wrong is fixed by fixing its derivation rule.");
            sb.AppendLine();
        }

        private void AppendPools(System.Text.StringBuilder sb, ItemPools pools)
        {
            sb.AppendLine("## The item pools");
            sb.AppendLine();
            sb.AppendLine("A bundle marked above as re-rolling can ask for any item in its pool, filtered by season where the bundle names one (and, for fish, by the habitat of the bundle it replaced).");
            sb.AppendLine();
            AppendPool(sb, "Seasonal Crops (also the Quality Crops pool, at gold)", pools.Crops);
            AppendPool(sb, "Seasonal Forage", pools.Forage);
            AppendPool(sb, "Fish", pools.Fish);
            AppendPool(sb, "Crab Pot", pools.CrabPot);
            AppendPool(sb, "Monster Drops", pools.MonsterDrops);
            AppendPool(sb, "Metals", pools.Metals);
            AppendPool(sb, "Artisan Goods", pools.ArtisanGoods);
            AppendPool(sb, "Artifacts (authored bundles)", pools.Artifacts);
            AppendPool(sb, "Books (authored bundles)", pools.Books);
            AppendPool(sb, "Saplings (authored bundles)", pools.Saplings);
            AppendPool(sb, "Geode Minerals (authored bundles)", pools.GeodeMinerals);
            AppendPool(sb, "Cooking (authored bundles)", pools.Cooking);
            AppendPool(sb, "Tapper Goods (authored bundles)", pools.TapperGoods);
        }

        private static void AppendPool(System.Text.StringBuilder sb, string title, System.Collections.Generic.IReadOnlyList<PoolItem> items)
        {
            sb.AppendLine($"### {title} — {items.Count} items");
            sb.AppendLine();
            if (items.Count == 0) { sb.AppendLine("_(empty)_"); sb.AppendLine(); return; }
            sb.AppendLine("| Item | Price | Seasons |");
            sb.AppendLine("|---|---|---|");
            foreach (PoolItem i in items.OrderBy(i => i.Price))
            {
                string seasons = i.Seasons.Count == 0 ? "any" : string.Join(" / ", i.Seasons);
                sb.AppendLine($"| {DisplayName(i.ItemId)} | {i.Price}g | {seasons} |");
            }
            sb.AppendLine();
        }

        private static void AppendAuthored(System.Text.StringBuilder sb)
        {
            sb.AppendLine("## Bundles authored by the mod");
            sb.AppendLine();
            sb.AppendLine("These are added as extra candidates to every position of their room, so any of them can displace a vanilla bundle. Their slots are composed once and are final: the engine never re-rolls them.");
            sb.AppendLine();
            sb.AppendLine("| Bundle | Room | Shows | Needs | Items drawn from | Quality |");
            sb.AppendLine("|---|---|---|---|---|---|");
            foreach (var def in AuthoredBundleCatalog.All)
            {
                string source = def.Source == AuthoredSlotSource.FixedList
                    ? "fixed: " + string.Join(", ", def.FixedItemIds.Select(DisplayName))
                    : def.Source.ToString() + " pool";
                string quality = def.QualityAsk > 0 ? def.QualityAsk.ToString() : "any";
                sb.AppendLine($"| {def.Name} | {def.Room} | {def.SlotCount} | {def.NumberOfSlots} | {source} | {quality} |");
            }
            sb.AppendLine();
        }

        private void CmdDifficulty(string command, string[] args)
        {
            DifficultySettings configured = _config.Difficulty;
            DifficultyProfile live = _meta?.State != null
                ? _meta.State.EffectiveDifficulty(_config)
                : DifficultyResolver.Resolve(configured, _config);
            bool stamped = _meta?.State?.Difficulty != null;

            this.Monitor.Log("=== The Longest Year: difficulty ===", LogLevel.Info);
            this.Monitor.Log(
                stamped
                    ? "  In force: the profile STAMPED on this save. Config changes apply at your next loop."
                    : "  In force: resolved live from config (this save has no stamp yet; the next reset writes one).",
                LogLevel.Info);

            this.Monitor.Log("  Step               configured -> in force", LogLevel.Info);
            LogStep("stack size", configured.StackSize, live.Steps.StackSize);
            LogStep("quality asks", configured.QualityAsks, live.Steps.QualityAsks);
            LogStep("required slots", configured.RequiredSlots, live.Steps.RequiredSlots);
            LogStep("item rarity", configured.ItemRarity, live.Steps.ItemRarity);
            LogStep("JP earned", configured.JpEarned, live.Steps.JpEarned);
            LogStep("shrine prices", configured.ShrinePrices, live.Steps.ShrinePrices);
            LogStep("starting gold", configured.StartingGold, live.Steps.StartingGold);
            LogStep("cart slots", configured.CartSlots, live.Steps.CartSlots);
            LogStep("hold/pity prices", configured.HoldPrices, live.Steps.HoldPrices);
            LogStep("season pity", configured.SeasonPity, live.Steps.SeasonPity);

            this.Monitor.Log("  Resolved values in force:", LogLevel.Info);
            this.Monitor.Log(
                $"    asks: stack x{live.StackFactor}, quality x{live.QualityFactor}, " +
                $"required slots {(live.RequireAllSlots ? "ALL shown" : live.RequiredSlotsDelta.ToString("+0;-0;0"))}, " +
                $"rarity bias {live.RarityBias}",
                LogLevel.Info);
            this.Monitor.Log(
                $"    economy: JP x{live.JpEarnedFactor}, shrine prices x{live.ShrinePriceFactor}, " +
                $"starting gold {live.StartingGold}g, starting cart slots {live.StartingCartSlots}, " +
                $"hold/pity prices x{live.HoldPriceFactor}",
                LogLevel.Info);
            this.Monitor.Log(
                $"    season pity: {(live.Pity.Enabled ? "on" : "OFF")}, threshold {live.Pity.Threshold}, " +
                $"quota step {live.Pity.QuotaStep}, floor {live.Pity.QuotaFloor}, trim {live.Pity.TrimPerStep}/step",
                LogLevel.Info);

            this.Monitor.Log(
                $"  Board source: {_meta?.State?.BundleSource ?? BundleSourceNames.Engine}. " +
                "Item rarity applies to Engine (TLY Custom) boards only; stack size, quality asks and " +
                "required slots apply to vanilla boards too.",
                LogLevel.Info);

            void LogStep(string label, DifficultyStep configuredStep, DifficultyStep liveStep)
            {
                string note = configuredStep == liveStep ? "" : "   (pending: applies at your next loop)";
                this.Monitor.Log($"    {label,-18} {configuredStep,-8} -> {liveStep}{note}", LogLevel.Info);
            }
        }

        private void CmdNetState(string command, string[] args)
        {
            if (!Context.IsWorldReady) { this.Monitor.Log("Load a save first.", LogLevel.Warn); return; }

            NetWorldState ws = Game1.netWorldState.Value;
            Quest quest = ws.QuestOfTheDay;
            StardewValley.Object dish = ws.DishOfTheDay;

            this.Monitor.Log("=== netWorldState audit probe ===", LogLevel.Info);
            this.Monitor.Log(
                $"  Game1 date: Y{Game1.year} {Game1.season} {Game1.dayOfMonth} @ {Game1.timeOfDay}, " +
                $"DaysPlayed={Game1.stats.DaysPlayed}, uniqueID={Game1.uniqueIDForThisGame}",
                LogLevel.Info);
            this.Monitor.Log(
                $"  netWorldState date: Y{ws.Date.Year} {ws.Date.Season} {ws.Date.DayOfMonth} " +
                $"(NOTE: Date is a computed WorldDate.Now(), so this mirrors Game1 by construction)",
                LogLevel.Info);
            this.Monitor.Log(
                $"  [row 59] QuestOfTheDay = {(quest == null ? "null (expected on Spring 1)" : quest.GetType().Name + " \"" + quest.questTitle + "\" reward=" + quest.moneyReward.Value + "g")}",
                LogLevel.Info);
            this.Monitor.Log(
                $"  [row 54] DishOfTheDay  = {(dish == null ? "null (expected on Spring 1)" : dish.Name + " x" + dish.Stack)}",
                LogLevel.Info);
            this.Monitor.Log(
                $"  [row 16] VisitsUntilY1Guarantee = {ws.VisitsUntilY1Guarantee} (-1 = guarantee not armed on this save)",
                LogLevel.Info);
            this.Monitor.Log(
                $"  [rows 39-42] walnuts={ws.GoldenWalnuts}/{ws.GoldenWalnutsFound} coconut={ws.GoldenCoconutCracked} " +
                $"buriedNuts={ws.FoundBuriedNuts.Count} islandVisitors={ws.IslandVisitors.Count}",
                LogLevel.Info);
            this.Monitor.Log(
                $"  [rows 7-8] minesDifficulty={ws.MinesDifficulty} skullCavesDifficulty={ws.SkullCavesDifficulty}",
                LogLevel.Info);
            this.Monitor.Log(
                $"  [rows 30-31,45,56] raccoonBundles=[{string.Join(",", ws.raccoonBundles)}] " +
                $"season={ws.SeasonOfCurrentRacconBundle} timesFed={ws.TimesFedRaccoons} " +
                $"lastFinishedDay={ws.DaysPlayedWhenLastRaccoonBundleWasFinished}",
                LogLevel.Info);
            this.Monitor.Log(
                $"  [rows 43-44,46] miniBins={ws.MiniShippingBinsObtained} waivers={ws.PerfectionWaivers} totems={ws.TreasureTotemsUsed}",
                LogLevel.Info);
            this.Monitor.Log(
                $"  [rows 49-53,57-58] builders={ws.Builders.Length} worldStateIDs={Game1.worldStateIDs.Count} (Game1 mirror) " +
                $"passiveFestivals={ws.ActivePassiveFestivals.Count} checkedGarbage={ws.CheckedGarbage.Count} " +
                $"canDrive={ws.canDriveYourselfToday.Value} clocksOff={ws.goldenClocksTurnedOff.Value}",
                LogLevel.Info);
            this.Monitor.Log(
                $"  [rows 35-38] lowestMineLevel={ws.LowestMineLevel}/{ws.LowestMineLevelForOrder} " +
                $"museumPieces={ws.MuseumPieces.Length} lostBooks={ws.LostBooksFound}",
                LogLevel.Info);
            this.Monitor.Log(
                $"  [keeps] whichFarm={Game1.whichFarm} shuffleMineChests={ws.ShuffleMineChests} " +
                $"farmhandData={ws.farmhandData.Length} locationsWithBuildings={ws.LocationsWithBuildings.Count}",
                LogLevel.Info);

            // Weather: the live Game1 flags, netWorldState's own copy (synced by UpdateFromGame1
            // mid-reset), and the scheduler's pick for today/tomorrow so the three can be compared.
            var defaultWeather = ws.GetWeatherForLocation(NetStateDefaultWeatherContext);
            var tomorrow = new WorldDate(Game1.Date);
            tomorrow.TotalDays++;
            string scheduledToday = WeatherScheduleWriterPatch.ScheduledFor(Game1.Date) ?? "(vanilla)";
            string scheduledTomorrow = WeatherScheduleWriterPatch.ScheduledFor(tomorrow) ?? "(vanilla)";
            this.Monitor.Log(
                $"  [weather] live: raining={Game1.isRaining} lightning={Game1.isLightning} snowing={Game1.isSnowing} " +
                $"debris={Game1.isDebrisWeather} greenRain={Game1.isGreenRain}; " +
                $"netWorldState Default: today={defaultWeather.Weather} tomorrow={defaultWeather.WeatherForTomorrow}; " +
                $"Game1.weatherForTomorrow={Game1.weatherForTomorrow}; " +
                $"schedule: today={scheduledToday} tomorrow={scheduledTomorrow}",
                LogLevel.Info);

            // `tly_netstate army1 <n>`: arm the Traveling Cart year-1 guarantee window in memory so
            // a reset can be seen re-rolling it (a save that never enabled YearOneCompletable sits
            // at -1 and the reset leaves it alone, so there is nothing to watch otherwise).
            if (args.Length >= 2 && string.Equals(args[0], NetStateArmY1Arg, StringComparison.OrdinalIgnoreCase)
                && int.TryParse(args[1], out int visits))
            {
                ws.VisitsUntilY1Guarantee = visits;
                this.Monitor.Log($"  [row 16] VisitsUntilY1Guarantee armed at {visits} (in memory; a reset re-rolls it).", LogLevel.Info);
            }
        }

        private const string NetStateDefaultWeatherContext = "Default";
        private const string NetStateArmY1Arg = "army1";

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
                ParseBundleQuotas(),
                _availability);
            IReadOnlyList<CcItem> catalog = builder.Build();
            IReadOnlyList<BundleRequirement> requirements = builder.BuildRequirements();
            this.Monitor.Log($"tly_classify: {catalog.Count} catalog items, {requirements.Count} requirements (diagnostics only — active run unchanged).", LogLevel.Info);
        }

        /// <summary>Diagnostics-only dry run of the owned-bundle engine for a given loop number:
        /// generates the set (nothing written or persisted — never touches live BundleData or
        /// MetaState), logs each room's picked bundle names + slot counts, the manifest
        /// classification summary, and a determinism self-check (regenerates off the SAME seed
        /// and diffs the two sets byte-for-byte). Mirrors <see cref="CmdClassify"/>'s design —
        /// locals only. Guarded exactly like tly_classify (requires a loaded save) because the
        /// seed basis is Game1.player.UniqueMultiplayerID, which doesn't exist at the title
        /// screen.
        ///
        /// The optional mode argument (<c>custom</c> default, <c>standard</c>, <c>remixed</c>)
        /// picks WHICH board is audited: the engine's own set, or the board vanilla would build
        /// for the matching Advanced Options dropdown choice
        /// (<see cref="TheLongestYear.Loop.BundleOptionPatch.Choice"/>). Both vanilla modes run
        /// the exact same listing, classification, gate audit and determinism self-check, and
        /// write nothing either.</summary>
        private void CmdGenBundles(string command, string[] args)
        {
            if (!Context.IsWorldReady) { this.Monitor.Log("Load a save first.", LogLevel.Warn); return; }

            int seedLoop = _meta.State.EffectiveBundleSeedLoop;
            var mode = TheLongestYear.Loop.BundleOptionPatch.Choice.TlyCustom;
            foreach (string arg in args)
            {
                if (int.TryParse(arg, out int parsedLoop))
                {
                    // Same rejection tly_reset makes: a seed loop is a count of completed resets,
                    // so a negative one is a typo, not a board.
                    if (parsedLoop < 0)
                    {
                        this.Monitor.Log($"tly_genbundles: '{arg}' is not a seed loop. Usage: tly_genbundles [seedLoop] [custom|standard|remixed]", LogLevel.Warn);
                        return;
                    }
                    seedLoop = parsedLoop;
                }
                else if (TheLongestYear.Loop.BundleOptionPatch.TryParseChoice(arg, out var parsedMode))
                    mode = parsedMode;
                else
                {
                    this.Monitor.Log($"tly_genbundles: unknown argument '{arg}'. Usage: tly_genbundles [seedLoop] [custom|standard|remixed]", LogLevel.Warn);
                    return;
                }
            }

            // Same seed basis as ResolveRequirements/WorldResetService.PerformReset — see
            // ResolveRequirements' comment for why (Game1.uniqueIDForThisGame is time-based and
            // re-seeded by our own reset every loop, so it can't be the basis).
            ulong seedBasis = unchecked((ulong)Game1.player.UniqueMultiplayerID);
            int seed = BundleEngineSeed.For(seedBasis, seedLoop);

            System.Collections.Generic.IReadOnlyDictionary<string, TheLongestYear.Core.Season> itemSeasonPins = ParseItemSeasonPins();
            System.Collections.Generic.IReadOnlyDictionary<string, int[]> bundleQuotas = ParseBundleQuotas();

            if (mode != TheLongestYear.Loop.BundleOptionPatch.Choice.TlyCustom)
            {
                GenBundlesVanilla(mode, seedLoop, seed, itemSeasonPins, bundleQuotas);
                return;
            }

            PityTrim trim = TheLongestYear.Loop.BundleEngine.TrimFor(_meta.State);
            // Diagnostics have to show what the loop actually runs under, so this uses the STAMPED
            // profile like every other generation path. A preview resolved from live config would
            // report a board the save is not playing.
            TheLongestYear.Core.DifficultyProfile genDifficulty = _meta.State.BoardDifficulty(_config);
            BundleGenerationTuning genTuning =
                TheLongestYear.Core.DifficultyTuning.Scale(_config.PoolTuning, genDifficulty);
            var firstEngine = new TheLongestYear.Loop.BundleEngine(this.Monitor, genTuning, _config.EnableNonObjectDonations, _config.RarityThresholds, TheLongestYear.Core.YearTwoCrops.ExcludedFor(_meta.State.HasUpgrade, genDifficulty.Steps.ItemRarity), genDifficulty);
            firstEngine.Availability = _availability;
            GeneratedBundleSet first = firstEngine.Generate(seed, trim);
            this.Monitor.Log(
                $"tly_genbundles: generated for loop {seedLoop} (seed {seed}, mode custom), diagnostics only, nothing written.",
                LogLevel.Info);
            LogGeneratedBundleSet(firstEngine, first, itemSeasonPins, bundleQuotas);

            var secondEngine = new TheLongestYear.Loop.BundleEngine(this.Monitor, genTuning, _config.EnableNonObjectDonations, _config.RarityThresholds, TheLongestYear.Core.YearTwoCrops.ExcludedFor(_meta.State.HasUpgrade, genDifficulty.Steps.ItemRarity), genDifficulty);
            secondEngine.Availability = _availability;
            GeneratedBundleSet second = secondEngine.Generate(seed, trim);
            string difference = FirstBundleSetDifference(first, second);
            if (difference == null)
                this.Monitor.Log("tly_genbundles: determinism OK (second generation matched the first byte-for-byte).", LogLevel.Info);
            else
                this.Monitor.Log($"tly_genbundles: determinism ERROR: {difference}", LogLevel.Error);
        }

        /// <summary>The vanilla half of <see cref="CmdGenBundles"/>: build the board the GAME
        /// would build for the given Advanced Options dropdown choice, then run it through the
        /// same listing, classification, gate audit and determinism self-check the engine board
        /// gets. Nothing is written: neither path touches BundleData or MetaState.
        ///
        /// Standard is just <c>Data/Bundles</c> (what <c>Game1.GenerateBundles(Default)</c>
        /// hands to SetBundleData). Remixed runs the game's own <c>BundleGenerator</c> over
        /// <c>Data/RandomBundles</c>, seeded with vanilla's own formula
        /// (<c>CreateRandom(seed * 9.0)</c>) but off OUR per-loop seed rather than
        /// <c>Game1.uniqueIDForThisGame</c>: a TLY reset re-seeds uniqueIDForThisGame from the
        /// clock, so the game's own per-loop remix is not reproducible, while this is (and it
        /// varies per seed loop, which is the point of the diagnostic).</summary>
        private void GenBundlesVanilla(
            TheLongestYear.Loop.BundleOptionPatch.Choice mode,
            int seedLoop,
            int seed,
            System.Collections.Generic.IReadOnlyDictionary<string, TheLongestYear.Core.Season> itemSeasonPins,
            System.Collections.Generic.IReadOnlyDictionary<string, int[]> bundleQuotas)
        {
            bool remixed = mode == TheLongestYear.Loop.BundleOptionPatch.Choice.VanillaRemixed;
            string label = remixed ? "remixed" : "standard";
            // Standard reads Data/Bundles verbatim, so the seed never enters the board at all: every
            // seed loop yields the same board. Say that in the header line rather than printing a
            // seed the mode ignores.
            string modeLabel = remixed ? "mode remixed" : "mode standard (seed ignored: Data/Bundles is fixed)";

            System.Collections.Generic.IReadOnlyDictionary<string, string> firstData;
            System.Collections.Generic.IReadOnlyDictionary<string, string> secondData = null;
            try
            {
                firstData = remixed
                    ? TheLongestYear.Loop.VanillaBundlePool.GenerateRemixedBundleData(seed)
                    : TheLongestYear.Loop.VanillaBundlePool.LoadStandardBundleData();
                if (remixed)
                    secondData = TheLongestYear.Loop.VanillaBundlePool.GenerateRemixedBundleData(seed);
            }
            catch (Exception ex)
            {
                this.Monitor.Log($"tly_genbundles: could not build the vanilla {label} board ({ex.GetType().Name}: {ex.Message}).", LogLevel.Error);
                return;
            }

            GeneratedBundleSet first = TheLongestYear.Loop.VanillaBundlePool.SetFromBundleData(firstData);
            this.Monitor.Log(
                $"tly_genbundles: generated for loop {seedLoop} (seed {seed}, {modeLabel}), diagnostics only, nothing written.",
                LogLevel.Info);
            LogGeneratedBundleSet(null, first, itemSeasonPins, bundleQuotas, $"vanilla slots ({label})");

            // The self-check regenerates off the same seed and diffs. For standard that compares
            // Data/Bundles to itself, which can never fail and so proves nothing: skip it and say so.
            if (!remixed)
            {
                this.Monitor.Log(
                    "tly_genbundles: determinism self-check skipped for standard (Data/Bundles is fixed and the seed is ignored, so there is nothing to vary).",
                    LogLevel.Info);
                return;
            }

            GeneratedBundleSet second = TheLongestYear.Loop.VanillaBundlePool.SetFromBundleData(secondData);
            string difference = FirstBundleSetDifference(first, second);
            if (difference == null)
                this.Monitor.Log("tly_genbundles: determinism OK (second generation matched the first byte-for-byte).", LogLevel.Info);
            else
                this.Monitor.Log($"tly_genbundles: determinism ERROR: {difference}", LogLevel.Error);
        }

        /// <summary>Logs each room's picked bundle names + slot counts, then the manifest
        /// classification summary ("N generated, M classified, K skipped"). K counts every
        /// bundle <see cref="GeneratedBundleSet.BuildRequirements"/> drops (Vault/non-themed
        /// rooms are expected to drop — RoomThemeMap has no entry for them — so that's not
        /// logged as a problem); any drop INSIDE a themed room is unexpected (the engine
        /// authored every bundle, so nothing themed should ever fail classification) and is
        /// called out at WARN with a per-room breakdown.</summary>
        /// <param name="engine">The engine that produced the set, for its per-slot provenance.
        /// Null for a vanilla board (no engine ran); then <paramref name="slotSourceOverride"/>
        /// labels every slot line instead.</param>
        /// <param name="slotSourceOverride">Fixed slot-source label, e.g.
        /// "vanilla slots (remixed)". Null keeps the engine's per-bundle provenance.</param>
        private void LogGeneratedBundleSet(
            TheLongestYear.Loop.BundleEngine engine,
            GeneratedBundleSet set,
            System.Collections.Generic.IReadOnlyDictionary<string, TheLongestYear.Core.Season> itemSeasonPins,
            System.Collections.Generic.IReadOnlyDictionary<string, int[]> bundleQuotas,
            string slotSourceOverride = null)
        {
            var derivedSeasonPins = engine?.LastDerivedSeasonPins
                ?? (System.Collections.Generic.IReadOnlyDictionary<string, TheLongestYear.Core.Season>)
                   new Dictionary<string, TheLongestYear.Core.Season>(StringComparer.Ordinal);

            foreach (var roomGroup in set.Bundles.GroupBy(b => b.Room).OrderBy(g => g.Key, StringComparer.Ordinal))
            {
                this.Monitor.Log($"  {roomGroup.Key}:", LogLevel.Info);
                foreach (BundleSpec spec in roomGroup.OrderBy(b => b.Index))
                {
                    // Authored names are unique by construction (AuthoredBundleCatalog), so an
                    // exact Name match is sufficient here -- unlike Uniquify's " II"/" III"
                    // collision suffixes (which only ever apply to vanilla RandomBundles name
                    // collisions), an authored def's Name never gets suffixed.
                    string authoredTag = engine != null && TheLongestYear.Core.AuthoredBundleCatalog.All.Any(d => d.Name == spec.Name)
                        ? " [authored]"
                        : "";
                    this.Monitor.Log(
                        $"    [{spec.Index}] {spec.DisplayName} (pick {spec.NumberOfSlots} of {spec.Slots.Count}){authoredTag}",
                        LogLevel.Info);

                    // engine.LastDomains keyed by absolute index; missing key/None = vanilla slots
                    // (money bundles only since spec 2026-08-28-obtainable-board-3-pools). A Recipe
                    // pick names its recipe and its parts, so the log shows WHICH pool it drew from.
                    // An authored bundle is composed by AuthoredBundleComposer and is FINAL, so the
                    // engine files it under None as well; it does NOT keep vanilla slots, and
                    // saying so made the "no non-money bundle keeps vanilla slots" audit unreadable.
                    string source;
                    if (slotSourceOverride != null)
                        source = slotSourceOverride;
                    else if (authoredTag.Length > 0)
                        source = "authored slots";
                    else if (!engine.LastDomains.TryGetValue(spec.Index, out TheLongestYear.Core.DomainMatch m)
                        || m.Domain == TheLongestYear.Core.PoolDomain.None)
                        source = "vanilla slots";
                    else if (m.Domain == TheLongestYear.Core.PoolDomain.Recipe)
                        source = $"re-rolled from recipe {(engine.LastRecipes.TryGetValue(spec.Index, out string recipe) ? recipe : spec.Name)}";
                    else
                        source = $"re-rolled from {m.Domain}{(m.Season != null ? $"({m.Season})" : "")}";
                    this.Monitor.Log(
                        $"      {spec.Room}/{spec.Index} '{spec.Name}' [{spec.Slots.Count} slots, need {spec.NumberOfSlots}] — {source}",
                        LogLevel.Info);

                    // Vault money slots carry the GOLD amount in Quality with item id -1, so they
                    // showed up here as "-1 q25000" and read like an impossible quality ask. They are
                    // not asks at all: skip them (Jeff, 0.13.0 smoke).
                    var qualityAsks = spec.Slots
                        .Where(s => s.Quality > 0 && s.ItemId != "-1" && !string.IsNullOrEmpty(s.ItemId))
                        .Select(s => $"{s.ItemId} q{s.Quality}").ToList();
                    if (qualityAsks.Count > 0)
                        this.Monitor.Log($"        quality asks: {string.Join(", ", qualityAsks)}", LogLevel.Info);

                    // Stack asks above 1, so a balance report can show what the stack-size
                    // difficulty modifier actually did. Money slots are excluded for the same
                    // reason as above: a Vault "stack" is a gold amount, not an ask.
                    var stackAsks = spec.Slots
                        .Where(s => s.Stack > 1 && s.ItemId != "-1" && !string.IsNullOrEmpty(s.ItemId))
                        .Select(s => $"{s.ItemId} x{s.Stack}").ToList();
                    if (stackAsks.Count > 0)
                        this.Monitor.Log($"        stack asks: {string.Join(", ", stackAsks)}", LogLevel.Info);

                    // Every slot by name, so a log alone is enough to audit what the board asks
                    // for (bundle-loop audit, 2026-08-29). Money slots are gold amounts, not asks.
                    var slotNames = spec.Slots
                        .Where(s => s.ItemId != "-1" && !string.IsNullOrEmpty(s.ItemId))
                        .Select(s => $"{DisplayName(s.ItemId)} ({s.ItemId}) x{s.Stack}{(s.Quality > 0 ? $" q{s.Quality}" : "")}")
                        .ToList();
                    if (slotNames.Count > 0)
                        this.Monitor.Log($"        slots: {string.Join(", ", slotNames)}", LogLevel.Info);
                }
            }
            this.Monitor.Log($"  derived season pins in effect: {derivedSeasonPins.Count}", LogLevel.Info);

            IReadOnlyList<BundleRequirement> requirements = engine != null
                ? engine.BuildRequirements(set, itemSeasonPins, bundleQuotas, ease: null, availability: _availability)
                : set.BuildRequirements(itemSeasonPins, bundleQuotas, ease: null, availability: _availability);
            int generated = set.Bundles.Count;
            int classified = requirements.Count;
            int skipped = generated - classified;

            // The gates this board would run under, then the same audit tly_gatecheck runs on the
            // live board, so a diagnostic loop can be checked for IMPOSSIBLE gates without a reset.
            foreach (BundleRequirement req in requirements.OrderBy(r => r.Theme).ThenBy(r => r.Name, StringComparer.Ordinal))
            {
                string gates = req.Kind switch
                {
                    BundleKind.Seasonal => $"all by {req.SeasonalSeason}",
                    BundleKind.PerItem => string.Join(", ", req.ItemSeasonPins
                        .OrderBy(kv => (int)kv.Value).ThenBy(kv => kv.Key, StringComparer.Ordinal)
                        .Select(kv => $"{DisplayName(kv.Key)} by {kv.Value}")),
                    BundleKind.Percentage => $"ramp [{string.Join(",", req.CumulativeRequiredBySeason)}] of X={req.NumberOfSlots}",
                    _ => "",
                };
                this.Monitor.Log($"      gates {req.Name} ({req.Kind}): {gates}", LogLevel.Info);
            }
            var auditPins = new Dictionary<string, TheLongestYear.Core.Season>(derivedSeasonPins, StringComparer.Ordinal);
            foreach (var kv in itemSeasonPins)
                auditPins[kv.Key] = kv.Value;
            LogGateAudit(requirements, auditPins, "tly_genbundles", engine?.LastVanillaOnlyRecipes);

            var themedSkipsByRoom = new System.Collections.Generic.Dictionary<string, int>(StringComparer.Ordinal);
            foreach (BundleSpec spec in set.Bundles)
            {
                if (!RoomThemeMap.TryGetTheme(spec.Room, out TheLongestYear.Core.Theme theme))
                    continue; // Vault / non-themed room — always classified out, not a problem.

                var parsed = BundleParsing.Parse(BundleDataWriter.Key(spec), BundleDataWriter.Value(spec));
                if (BundleClassifier.Classify(parsed, theme, itemSeasonPins, bundleQuotas, _availability) == null)
                    themedSkipsByRoom[spec.Room] = themedSkipsByRoom.TryGetValue(spec.Room, out int n) ? n + 1 : 1;
            }
            int themedSkipped = themedSkipsByRoom.Values.Sum();

            this.Monitor.Log(
                $"tly_genbundles: {generated} generated, {classified} classified, {skipped} skipped.",
                LogLevel.Info);
            if (themedSkipped > 0)
            {
                string breakdown = string.Join(", ", themedSkipsByRoom.Select(kv => $"{kv.Key}: {kv.Value}"));
                // A vanilla board can legitimately drop a themed bundle (category-only asks such
                // as "any fish"), so that is reported, not flagged as a defect; only the engine's
                // own board is expected to classify every themed bundle it authored.
                this.Monitor.Log(
                    engine != null
                        ? $"tly_genbundles: {themedSkipped} skipped bundle(s) fell inside themed rooms (unexpected: {breakdown})."
                        : $"tly_genbundles: {themedSkipped} skipped bundle(s) fell inside themed rooms ({breakdown}).",
                    engine != null ? LogLevel.Warn : LogLevel.Info);
            }
        }

        /// <summary>
        /// Diagnostics-only proof that the weapon/hat donation patches (see
        /// <see cref="TheLongestYear.Patches.BundleDonationPatches"/>) make (W)/(H) items valid CC
        /// ingredients. Creates ephemeral <c>(W)13</c>, <c>(H)8</c>, <c>(O)520</c> items via
        /// <c>ItemRegistry.Create</c> and a synthetic, DETACHED <see cref="Bundle"/> carrying Gil's
        /// Trophies' real ingredient composition (see
        /// <see cref="TheLongestYear.Core.AuthoredBundleCatalog.GilTrophies"/> / the Boiler Room
        /// authored def) via the simple
        /// <c>Bundle(name, displayName, ingredients, completedFlags, rewardListString)</c>
        /// constructor — chosen over the raw-BundleData-string overload because that one loads a
        /// texture and builds a <c>TemporaryAnimatedSprite</c> (Bundle.cs ~87-160), a side effect a
        /// diagnostic command shouldn't risk. The synthetic bundle is never added to
        /// <c>Game1.RequireLocation&lt;CommunityCenter&gt;("CommunityCenter").bundles</c>, so
        /// nothing here can touch the real board.
        ///
        /// For each id, logs PASS/FAIL for (a) <c>Bundle.IsValidItemForThisIngredientDescription</c>
        /// and (b) <c>Bundle.canAcceptThisItem</c> — the checks the highlight-wrapper's
        /// <c>ItemMatchesAnyNonObjectIngredient</c> and the vanilla pickup/click paths both rely on;
        /// (a) is the load-bearing check since (b) and the deposit path both call it internally.
        /// (c) <c>Bundle.tryToDepositThisItem</c> needs a live <see cref="JunimoNoteMenu"/> — its
        /// <c>onIngredientDeposit</c> callback is what breaks BEFORE the
        /// <c>communityCenter.bundles.FieldDict</c> persistence write (Bundle.cs:323-328), which is
        /// what would make a non-persisting deposit test safe — but constructing a
        /// <see cref="JunimoNoteMenu"/> headlessly loads a whole room's textures/bundle set as a
        /// side effect of a debug command, so (c) is skipped and logged rather than risking that.
        /// Requires a loaded save (world-ready gate).
        /// </summary>
        private void CmdTrophyTest(string command, string[] args)
        {
            if (!Context.IsWorldReady) { this.Monitor.Log("Load a save first.", LogLevel.Warn); return; }
            if (_config == null) { this.Monitor.Log("Config unavailable.", LogLevel.Warn); return; }

            bool wrapperActive = RunActivation.IsActive && _config.EnableNonObjectDonations;
            this.Monitor.Log(
                $"tly_trophytest: highlight-wrapper active={wrapperActive} " +
                $"(RunActivation.IsActive={RunActivation.IsActive}, EnableNonObjectDonations={_config.EnableNonObjectDonations}).",
                LogLevel.Info);

            string[] ids = { "(W)13", "(H)8", "(O)520" };
            var ingredients = new List<BundleIngredientDescription>();
            foreach (string id in ids)
                ingredients.Add(new BundleIngredientDescription(id, 1, 0, completed: false));
            var completedFlags = new bool[ingredients.Count];

            Bundle synthetic;
            try
            {
                synthetic = new Bundle(
                    "Gil's Trophies (tly_trophytest)",
                    "Gil's Trophies (tly_trophytest)",
                    ingredients,
                    completedFlags,
                    "O 879 5");
            }
            catch (Exception ex)
            {
                this.Monitor.Log(
                    $"tly_trophytest: couldn't construct the synthetic Bundle: {ex.GetType().Name}: {ex.Message}. Aborting.",
                    LogLevel.Error);
                return;
            }

            int pass = 0, total = 0;
            for (int i = 0; i < ids.Length; i++)
            {
                string id = ids[i];
                Item item;
                try { item = ItemRegistry.Create(id, 1); }
                catch (Exception ex)
                {
                    total++;
                    this.Monitor.Log($"tly_trophytest [{id}]: couldn't create item — {ex.Message}. FAIL.", LogLevel.Error);
                    continue;
                }

                BundleIngredientDescription ingredient = synthetic.ingredients[i];

                bool validA = synthetic.IsValidItemForThisIngredientDescription(item, ingredient);
                total++;
                if (validA) pass++;
                this.Monitor.Log(
                    $"tly_trophytest [{id}] (a) IsValidItemForThisIngredientDescription = {(validA ? "PASS" : "FAIL")}.",
                    validA ? LogLevel.Info : LogLevel.Warn);

                // canAcceptThisItem accepts a null slot (its gate is "slot == null || slot.item == null"),
                // so no ClickableTextureComponent needs to be constructed for this check.
                bool validB = synthetic.canAcceptThisItem(item, null);
                total++;
                if (validB) pass++;
                this.Monitor.Log(
                    $"tly_trophytest [{id}] (b) canAcceptThisItem = {(validB ? "PASS" : "FAIL")}.",
                    validB ? LogLevel.Info : LogLevel.Warn);
            }

            this.Monitor.Log(
                "tly_trophytest (c) tryToDepositThisItem: deposit check skipped (needs live menu) — " +
                "constructing a headless JunimoNoteMenu would load a whole room's textures/bundle set " +
                "as a side effect of a debug command; (a)+(b) already exercise the ingredient-matching " +
                "logic the deposit path shares (Bundle.IsValidItemForThisIngredientDescription's id-branch).",
                LogLevel.Info);

            this.Monitor.Log($"tly_trophytest: {pass}/{total} PASS", pass == total ? LogLevel.Info : LogLevel.Error);
        }

        /// <summary>Compares two engine-generated sets by their written BundleData key/value
        /// pairs (the canonical form the game itself would see) and returns a description of the
        /// first difference found, or null if they're identical.</summary>
        private static string FirstBundleSetDifference(GeneratedBundleSet a, GeneratedBundleSet b)
        {
            IReadOnlyDictionary<string, string> dataA = a.ToBundleData();
            IReadOnlyDictionary<string, string> dataB = b.ToBundleData();

            foreach (string key in dataA.Keys.Union(dataB.Keys).OrderBy(k => k, StringComparer.Ordinal))
            {
                bool inA = dataA.TryGetValue(key, out string valueA);
                bool inB = dataB.TryGetValue(key, out string valueB);
                if (!inA)
                    return $"key '{key}' is present in the second generation but missing from the first.";
                if (!inB)
                    return $"key '{key}' is present in the first generation but missing from the second.";
                if (valueA != valueB)
                    return $"key '{key}' differs: '{valueA}' vs '{valueB}'.";
            }
            return null;
        }

        private void CmdTestDonate(string command, string[] args)
        {
            if (!Context.IsWorldReady) { this.Monitor.Log("Load a save first.", LogLevel.Warn); return; }
            if (args.Length < 1) { this.Monitor.Log("Usage: tly_testdonate <qualifiedId> [count]", LogLevel.Warn); return; }

            int count = args.Length > 1 && int.TryParse(args[1], out int c) ? c : 1;
            // The ledger mirrors the board, so fill the board slot first and pay with slot identity.
            var slot = TheLongestYear.Integration.CcSlotWriter.FirstOpenSlotFor(args[0]);
            if (slot == null) { this.Monitor.Log($"tly_testdonate: no open slot wants '{args[0]}'.", LogLevel.Warn); return; }
            TheLongestYear.Integration.CcSlotWriter.TryFill(slot.Value.BundleIndex, slot.Value.IngredientIndex);
            DonationService.Active?.OnItemDonated(args[0], count, slot.Value.BundleIndex, slot.Value.IngredientIndex);
        }

        private void CmdOpenHub(string command, string[] args)
        {
            if (!Context.IsWorldReady) { this.Monitor.Log("Load a save first.", LogLevel.Warn); return; }
            _launcher?.OpenWeeklyHub();
        }

        private void CmdSeasonGoals(string command, string[] args)
        {
            if (!Context.IsWorldReady) { this.Monitor.Log("Load a save first.", LogLevel.Warn); return; }
            _launcher?.OpenSeasonGoals();
        }

        /// <summary>Diagnostics only: the maximum JP the CURRENT loop's board can pay out, per
        /// season and in total, under two models — "donate as soon as obtainable" and the
        /// "strong player" (meet each checkpoint minimum with the cheapest obtainable slots, hoard
        /// the rest for Winter's 4×) — plus a hoard-everything ceiling. Pure maths in
        /// <see cref="JpBudgetCalculator"/>; this reduces the live BundleData + CcItem catalog +
        /// resolved requirements to its inputs. Baseline economy — no jp_boost tiers applied.
        /// Usage: tly_jpbudget [verbose]</summary>
        private void CmdJpBudget(string command, string[] args)
        {
            if (!Context.IsWorldReady) { this.Monitor.Log("Load a save first.", LogLevel.Warn); return; }
            bool verbose = args.Length > 0 && string.Equals(args[0], "verbose", StringComparison.OrdinalIgnoreCase);

            var catalogById = new Dictionary<string, CcItem>(StringComparer.Ordinal);
            foreach (CcItem item in _catalog)
                catalogById[item.Id] = item;
            var reqByName = new Dictionary<string, BundleRequirement>(StringComparer.Ordinal);
            foreach (BundleRequirement req in _requirements)
                if (!reqByName.ContainsKey(req.Name))
                    reqByName[req.Name] = req;

            var bundles = new List<BudgetBundle>();
            var notInCatalog = new SortedSet<string>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, string> kvp in Game1.netWorldState.Value.BundleData)
            {
                ParsedBundle parsed = BundleParsing.Parse(kvp.Key, kvp.Value);
                int vaultGold = TheLongestYear.Integration.VaultBundleMap.GoldForIndex(parsed.Index);
                if (vaultGold > 0)
                {
                    bundles.Add(new BudgetBundle(parsed.Room, parsed.Name, parsed.NumberOfSlots, new List<BudgetSlot>(), vaultGold));
                    continue;
                }

                reqByName.TryGetValue(parsed.Name, out BundleRequirement requirement);
                var slots = new List<BudgetSlot>();
                foreach (BundleIngredient ing in parsed.Ingredients)
                {
                    if (BundleParsing.IsCategoryRef(ing.ItemRef)) continue;
                    string id = BundleParsing.NormalizeItemId(ing.ItemRef);
                    Rarity rarity;
                    int earliest;
                    if (catalogById.TryGetValue(id, out CcItem cc))
                    {
                        rarity = cc.Rarity;
                        earliest = cc.ObtainableSeasons.Min(x => (int)x);
                    }
                    else
                    {
                        notInCatalog.Add(id);
                        rarity = TheLongestYear.Donations.ItemRarityResolver.Resolve(id, _config.RarityThresholds);
                        earliest = 0;
                    }
                    int? pin = null;
                    if (requirement?.Kind == BundleKind.PerItem && requirement.ItemSeasonPins != null
                        && requirement.ItemSeasonPins.TryGetValue(id, out TheLongestYear.Core.Season pinned))
                        pin = (int)pinned;
                    slots.Add(new BudgetSlot(id, rarity, earliest, pin));
                    if (verbose)
                        this.Monitor.Log(
                            $"  {parsed.Room}/{parsed.Index} '{parsed.Name}' {id} {rarity} earliest={(TheLongestYear.Core.Season)earliest}" +
                            (pin.HasValue ? $" pin={(TheLongestYear.Core.Season)pin.Value}" : "") +
                            (catalogById.ContainsKey(id) ? "" : " (not in catalog)"),
                            LogLevel.Info);
                }

                int? seasonal = requirement?.Kind == BundleKind.Seasonal && requirement.SeasonalSeason.HasValue
                    ? (int)requirement.SeasonalSeason.Value : (int?)null;
                IReadOnlyList<int> quota = requirement?.Kind == BundleKind.Percentage
                    ? requirement.CumulativeRequiredBySeason : null;
                bundles.Add(new BudgetBundle(parsed.Room, parsed.Name, parsed.NumberOfSlots, slots, 0, seasonal, quota));
            }

            JpBudgetReport report = JpBudgetCalculator.Compute(
                bundles, _config.Jp, _config.SelectionBonusMultiplier, BonusItemSampler.DefaultMaxCountBySeason,
                _meta.State.EffectiveDifficulty(_config).JpEarnedFactor);

            this.Monitor.Log(
                $"tly_jpbudget: loop {_meta.State.CompletedResets} (run {_meta.Run.RunNumber}), {bundles.Count} bundles, " +
                $"{bundles.Sum(b => b.Slots.Count)} item slots ({bundles.Sum(b => b.VaultGold > 0 ? 0 : Math.Min(b.NumberOfSlots, b.Slots.Count))} payable). " +
                "Baseline economy, no jp_boost.",
                LogLevel.Info);
            if (notInCatalog.Count > 0)
                this.Monitor.Log($"  not in the CcItem catalog (Spring/price-rarity fallback): {string.Join(", ", notInCatalog)}", LogLevel.Info);
            LogModel("EARLIEST model (donate as soon as obtainable)", report.Earliest, report);
            LogModel("STRONG model (checkpoint minimums only, hoard the rest for Winter)", report.Strong, report);
            this.Monitor.Log(
                $"  TOTALS: earliest = {report.EarliestTotal} JP; strong = {report.StrongTotal} JP; " +
                $"hoard-everything ceiling = {report.HoardCeiling} JP (ignores checkpoints — upper bound only). " +
                $"Fixed awards inside each total: {report.FixedAwards} (weekly {report.WeeklyQuest.Sum()}, checkpoints {report.Checkpoint.Sum()}, vault {report.Vault.Sum()}).",
                LogLevel.Info);
            foreach (string gate in report.ImpossibleGates)
                this.Monitor.Log($"  IMPOSSIBLE GATE: {gate}", LogLevel.Warn);
        }

        /// <summary>Diagnostics: read/set MetaState.BundleSource + VanillaBundleType and the
        /// config's BundleSource in memory so an unattended smoke can reset in each mode.</summary>
        private void CmdBundleSource(string command, string[] args)
        {
            if (!Context.IsWorldReady) { this.Monitor.Log("Load a save first.", LogLevel.Warn); return; }
            if (args.Length >= 1)
            {
                string source = BundleSourceNames.Normalize(args[0]);
                _config.BundleSource = source;
                _meta.State.BundleSource = source;
            }
            if (args.Length >= 2)
                _meta.State.VanillaBundleType = string.Equals(args[1], "Remixed", StringComparison.OrdinalIgnoreCase)
                    ? Game1.BundleType.Remixed.ToString() : Game1.BundleType.Default.ToString();
            this.Monitor.Log(
                $"tly_bundlesource: save BundleSource={_meta.State.BundleSource}, VanillaBundleType={_meta.State.VanillaBundleType ?? "(unknown)"}, " +
                $"config BundleSource={_config.BundleSource}, marker={_meta.State.BundlesGeneratedForReset}, loop={_meta.State.CompletedResets}.",
                LogLevel.Info);
        }

        private void LogModel(string title, JpBudgetModel model, JpBudgetReport report)
        {
            this.Monitor.Log($"  {title}:", LogLevel.Info);
            this.Monitor.Log("    season  slots  donation  selBonus  bundles  rooms  weekly  checkpoint  vault  TOTAL", LogLevel.Info);
            for (int s = 0; s < Calendar.MonthsPerYear; s++)
            {
                long total = model.Donation[s] + model.SelectionBonus[s] + model.BundleBonus[s] + model.RoomBonus[s] + report.FixedAwardsFor(s);
                this.Monitor.Log(
                    $"    {(TheLongestYear.Core.Season)s,-6}  {model.Slots[s],5}  {model.Donation[s],8}  {model.SelectionBonus[s],8}  " +
                    $"{model.BundleBonus[s],7}  {model.RoomBonus[s],5}  {report.WeeklyQuest[s],6}  {report.Checkpoint[s],10}  " +
                    $"{report.Vault[s],5}  {total,5}",
                    LogLevel.Info);
            }
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
                    this.Monitor.Log($"    - {u.Id}: {u.DisplayName} — {TheLongestYear.Core.UpgradePricing.EffectiveCost(u, _meta.State.EffectiveDifficulty(_config))} JP{prereq}{owned}", LogLevel.Info);
                }
            }
        }

        private void CmdBuyUpgrade(string command, string[] args)
        {
            if (!Context.IsWorldReady) { this.Monitor.Log("Load a save first.", LogLevel.Warn); return; }
            if (args.Length < 1) { this.Monitor.Log("Usage: tly_buyupgrade <id>", LogLevel.Warn); return; }
            _purchases?.TryPurchase(args[0]);
        }

        /// <summary>Debug: run the shrine board's boost purchase without clicking it, so the
        /// headless bridge can exercise the same callback the Buy button uses.
        /// Usage: tly_boost &lt;yeartwoseeds|sneakpeek&gt;</summary>
        private void CmdBoost(string command, string[] args)
        {
            if (!Context.IsWorldReady) { this.Monitor.Log("Load a save first.", LogLevel.Warn); return; }
            if (args.Length < 1) { this.Monitor.Log("Usage: tly_boost <yeartwoseeds|sneakpeek>", LogLevel.Warn); return; }

            BoostId id;
            switch (args[0].ToLowerInvariant())
            {
                case "yeartwoseeds": id = BoostId.YearTwoSeeds; break;
                case "sneakpeek": id = BoostId.SneakPeek; break;
                default:
                    this.Monitor.Log($"tly_boost: unknown boost '{args[0]}'. Use yeartwoseeds or sneakpeek.", LogLevel.Warn);
                    return;
            }

            if (_boostPurchases == null) { this.Monitor.Log("tly_boost: boost service not wired yet.", LogLevel.Warn); return; }
            _boostPurchases.TryBuy(id);
        }

        /// <summary>Debug: invoke the TV's protected <c>getWeeklyRecipe()</c> directly so the
        /// headless bridge can exercise the Queen of Sauce path (and the Sneak Peek boost patch)
        /// without walking to a TV and clicking it. Logs the two dialogue lines the TV would show
        /// and reports whether the episode's recipe is now in the player's cookingRecipes.</summary>
        private void CmdTv(string command, string[] args)
        {
            if (!Context.IsWorldReady) { this.Monitor.Log("Load a save first.", LogLevel.Warn); return; }

            int week = (int)(Game1.stats.DaysPlayed % 224 / 7);
            this.Monitor.Log($"tly_tv: DaysPlayed={Game1.stats.DaysPlayed} day={Game1.dayOfMonth} ({Game1.shortDayNameFromDayOfSeason(Game1.dayOfMonth)}) vanilla week={week}", LogLevel.Info);

            var before = new System.Collections.Generic.HashSet<string>(Game1.player.cookingRecipes.Keys);
            var tv = new StardewValley.Objects.TV();
            var method = HarmonyLib.AccessTools.Method(typeof(StardewValley.Objects.TV), "getWeeklyRecipe", new System.Type[0]);
            if (method == null) { this.Monitor.Log("tly_tv: getWeeklyRecipe() not found.", LogLevel.Warn); return; }

            string[] result;
            try
            {
                result = method.Invoke(tv, new object[0]) as string[];
            }
            catch (System.Reflection.TargetInvocationException ex)
            {
                this.Monitor.Log($"tly_tv: getWeeklyRecipe threw {ex.InnerException?.GetType().Name}: {ex.InnerException?.Message}", LogLevel.Warn);
                return;
            }

            if (result == null) { this.Monitor.Log("tly_tv: getWeeklyRecipe returned null.", LogLevel.Warn); return; }
            for (int i = 0; i < result.Length; i++)
                this.Monitor.Log($"tly_tv: line[{i}] = {result[i]}", LogLevel.Info);

            var after = new System.Collections.Generic.HashSet<string>(Game1.player.cookingRecipes.Keys);
            foreach (string key in after)
            {
                if (!before.Contains(key))
                    this.Monitor.Log($"tly_tv: cookingRecipes gained '{key}'", LogLevel.Info);
            }
            this.Monitor.Log($"tly_tv: cookingRecipes count {before.Count} -> {after.Count}; Pizza present={after.Contains("Pizza")}", LogLevel.Info);
        }

        private void CmdDejaVu(string command, string[] args)
        {
            if (!Context.IsWorldReady) { this.Monitor.Log("Load a save first.", LogLevel.Warn); return; }
            MetaState s = _meta.State;
            RunState run = _meta.Run;
            string mode = args.Length > 0 ? args[0].ToLowerInvariant() : "status";
            switch (mode)
            {
                case "set" when args.Length >= 3 && int.TryParse(args[2], out int n):
                    s.VillagerFamiliarity[args[1]] = n;
                    this.Monitor.Log($"tly_dejavu: {args[1]} familiarity = {n}.", LogLevel.Info);
                    break;
                case "force" when args.Length >= 2:
                    TheLongestYear.Loop.DejaVuDialoguePatch.ForceNext = args[1];
                    this.Monitor.Log($"tly_dejavu: next talk with {args[1]} will inject a line (Introduction day excepted).", LogLevel.Info);
                    break;
                case "reset":
                    run.DejaVuShownTo.Clear();
                    run.DejaVuLastDay = -1;
                    this.Monitor.Log("tly_dejavu: loop caps cleared.", LogLevel.Info);
                    break;
                default:
                    int day = (int)Game1.stats.DaysPlayed;
                    var sb = new System.Text.StringBuilder(
                        $"tly_dejavu status: enabled={_config.EnableDejaVuDialogue} resets={s.CompletedResets} threshold={_config.DejaVuThreshold} " +
                        $"chance={_config.DejaVuChancePercent}% day={day} lastDay={run.DejaVuLastDay} " +
                        $"shownThisLoop=[{string.Join(",", run.DejaVuShownTo)}] force={TheLongestYear.Loop.DejaVuDialoguePatch.ForceNext ?? "-"}");
                    foreach (var kv in s.VillagerFamiliarity.OrderByDescending(k => k.Value))
                        sb.Append($"\n  {kv.Key}={kv.Value} tier={DejaVuRules.Tier(kv.Value, _config.DejaVuThreshold)} eligible={DejaVuRules.IsEligible(s, run, kv.Key, day, _config.DejaVuThreshold)}");
                    this.Monitor.Log(sb.ToString(), LogLevel.Info);
                    break;
            }
        }

        private void CmdReadBook(string command, string[] args)
        {
            if (!Context.IsWorldReady) { this.Monitor.Log("Load a save first.", LogLevel.Warn); return; }
            Farmer p = Game1.player;
            if (args.Length == 0)
            {
                var sb = new System.Text.StringBuilder("tly_readbook: ");
                foreach (BookKeep book in BookKeepTable.Entries)
                    sb.Append(book.StatKey).Append('=').Append(p.stats.Get(book.StatKey)).Append(' ');
                this.Monitor.Log(sb.ToString().TrimEnd(), LogLevel.Info);
                return;
            }
            string key = args[0];
            if (!key.StartsWith(BookKeepTable.StatKeyPrefix, System.StringComparison.Ordinal))
            {
                this.Monitor.Log($"tly_readbook: '{key}' is not a Book_* stat key.", LogLevel.Warn);
                return;
            }
            p.stats.Set(key, 1);
            this.Monitor.Log($"tly_readbook: {key}=1 (reach '{BookKeepTable.ReachFor(key)}' now met; buy {BookKeepTable.UpgradeIdFor(key)} at the shrine or via tly_buyupgrade).", LogLevel.Info);
        }

        private void CmdHold(string command, string[] args)
        {
            if (!Context.IsWorldReady) { this.Monitor.Log("Load a save first.", LogLevel.Warn); return; }
            MetaState s = _meta.State;
            string mode = args.Length > 0 ? args[0].ToLowerInvariant() : "status";
            switch (mode)
            {
                case "keep":
                case "reshuffle":
                    bool keep = mode == "keep";
                    var result = BundleHold.Apply(s, keep: keep, _config.BundleHoldCosts, s.EffectiveDifficulty(_config).HoldPriceFactor);
                    if (result != BundleHold.HoldResult.NotEnoughJp)
                        SeasonPity.DeclinePity(s, held: keep);   // the offer is a separate step: tly_pity accept|decline
                    this.Monitor.Log($"tly_hold {mode}: {result}. JP {s.JunimoPoints}, consecutive holds {s.ConsecutiveHolds}, seed loop {s.BundleSeedLoop}, choice stamped {s.HoldChoiceMadeForReset}, ease {s.BoardEaseSeason}/{s.BoardEaseSteps}, trim {s.BoardTrimSeason}/{s.BoardTrimSteps}; offer now {SeasonPity.OfferFor(s, keep, _config)} at {SeasonPity.PityCost(s, _config)} JP (tly_pity accept|decline).", LogLevel.Info);
                    this.Monitor.Log("tly_hold: run tly_reset before sleeping or this choice goes stale.", LogLevel.Warn);
                    break;
                default:
                    this.Monitor.Log($"tly_hold status: CompletedResets {s.CompletedResets}, seed loop {s.EffectiveBundleSeedLoop} (stored {s.BundleSeedLoop}), consecutive holds {s.ConsecutiveHolds}, next hold costs {BundleHold.NextCost(s, _config.BundleHoldCosts, s.EffectiveDifficulty(_config).HoldPriceFactor)} JP, choice stamped {s.HoldChoiceMadeForReset}.", LogLevel.Info);
                    break;
            }
        }

        private void CmdPity(string command, string[] args)
        {
            if (!Context.IsWorldReady) { this.Monitor.Log("Load a save first.", LogLevel.Warn); return; }
            MetaState s = _meta.State;
            string mode = args.Length > 0 ? args[0].ToLowerInvariant() : "status";
            if (mode == "set")
            {
                if (args.Length < 3 || !Enum.TryParse(args[1], ignoreCase: true, out TheLongestYear.Core.Season season) || !int.TryParse(args[2], out int fails))
                {
                    this.Monitor.Log("Usage: tly_pity set <spring|summer|fall|winter> <fails>", LogLevel.Warn);
                    return;
                }
                SeasonPity.Counts(s)[(int)season] = Math.Max(0, fails);
                s.LastFailSeason = (int)season;
                _meta.Save();
                this.Monitor.Log($"tly_pity: {season} fails set to {fails} (LastFailSeason = {season}). Saved.", LogLevel.Info);
            }
            else if (mode == "accept" || mode == "decline")
            {
                bool held = s.ConsecutiveHolds > 0;   // the pending tly_hold choice decides the path
                if (mode == "accept")
                {
                    var offer = SeasonPity.OfferFor(s, held, _config);
                    var pity = SeasonPity.AcceptPity(s, held, _config);
                    this.Monitor.Log($"tly_pity accept ({(held ? "kept" : "reshuffled")} board, offer {offer}): {pity}. JP {s.JunimoPoints}, consecutive uses {s.ConsecutivePityUses}.", LogLevel.Info);
                }
                else
                {
                    SeasonPity.DeclinePity(s, held);
                    this.Monitor.Log($"tly_pity decline ({(held ? "kept" : "reshuffled")} board): uses reset, stamps cleared.", LogLevel.Info);
                }
                this.Monitor.Log("tly_pity: run tly_reset before sleeping or this choice goes stale.", LogLevel.Warn);
            }
            var counts = SeasonPity.Counts(s);
            var ease = SeasonPity.CurrentQuotaEase(s, _config);
            this.Monitor.Log(
                $"tly_pity status: fails Spring {counts[0]} / Summer {counts[1]} / Fall {counts[2]} / Winter {counts[3]}; threshold {_config.PityThreshold}; " +
                $"steps Spring {SeasonPity.EaseSteps(s, TheLongestYear.Core.Season.Spring, _config)} / Summer {SeasonPity.EaseSteps(s, TheLongestYear.Core.Season.Summer, _config)} / Fall {SeasonPity.EaseSteps(s, TheLongestYear.Core.Season.Fall, _config)} / Winter {SeasonPity.EaseSteps(s, TheLongestYear.Core.Season.Winter, _config)}; " +
                $"last fail season {s.LastFailSeason}; held {s.ConsecutiveHolds}; quota ease {(ease == null ? "none" : $"{ease.Season} {ease.Steps} steps factor {ease.Factor:0.00}")}; " +
                $"ease stamp season {s.BoardEaseSeason} steps {s.BoardEaseSteps}; " +
                $"board trim season {s.BoardTrimSeason} units {s.BoardTrimSteps}; consecutive pity uses {s.ConsecutivePityUses} (next offer {SeasonPity.PityCost(s, _config)} JP); enabled {_config.PityEnabled}.",
                LogLevel.Info);
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
        /// engine wiring, Task 6; review-fixed v0.11.76). The three-way branch itself is the pure,
        /// tested <see cref="EngineModeDecider.Decide"/>:
        /// <list type="number">
        ///   <item>Engine mode -- <c>BundlesGeneratedForReset == CompletedResets</c>: the live
        ///   save's bundles were engine-written for THIS exact loop. Regenerate the manifest
        ///   deterministically from the seed (Generate() is pure given (UniqueMultiplayerID,
        ///   CompletedResets), so this reproduces the same set WriteToWorld wrote without a
        ///   second write) and defensively verify every generated entry matches the live
        ///   BundleData VALUE-for-value via <see cref="EngineManifestCheck.Matches"/> before
        ///   trusting it -- the engine's write-key space is invariant across generations, so
        ///   key-existence alone would pass a live board stuck on an OLDER generation (reachable:
        ///   MetaStore.Save persists the bumped counters before ForceFullSave writes the world; a
        ///   crash/skip in that window reloads with the new meta but the old generation's bundles
        ///   still on the board). A mismatch logs a WARN and falls through to case 3 instead of
        ///   silently serving a manifest that disagrees with what's actually on the CC board.</item>
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

            bool vanillaSource = BundleSourceNames.IsVanilla(state.BundleSource);
            RequirementsSource source = EngineModeDecider.Decide(
                state.BundlesGeneratedForReset, state.CompletedResets, AnyBundleSlotComplete(), vanillaSource);

            if (source == RequirementsSource.EngineManifest)
            {
                Dictionary<string, string> liveData = Game1.netWorldState.Value.BundleData;
                var seed = BundleEngineSeed.For(seedBasis, state.EffectiveBundleSeedLoop);

                // Generate with the CURRENT EnableNonObjectDonations first; if the live board
                // doesn't match, try the OPPOSITE flag — the only generation input that can
                // change between launches mid-loop. A flipped flag must not demote a healthy
                // engine board to the legacy read path (spec 2026-08-21): the board on disk was
                // composed with the old value and stays valid until the next reset regenerates.
                // Difficulty: re-derivation MUST use the STAMPED profile, never live config. The
                // board on disk was generated under the stamp, so resolving the current GMCM
                // values here would re-derive a different board and demote a healthy save to the
                // legacy read path on the next launch. A legacy save has no stamp and resolves
                // all-Normal, which is exactly what generated its board.
                TheLongestYear.Core.DifficultyProfile difficulty = state.BoardDifficulty(_config);
                BundleGenerationTuning difficultyTuning =
                    TheLongestYear.Core.DifficultyTuning.Scale(_config.PoolTuning, difficulty);

                foreach (bool nonObject in new[] { _config.EnableNonObjectDonations, !_config.EnableNonObjectDonations })
                {
                    var engine = new TheLongestYear.Loop.BundleEngine(this.Monitor, difficultyTuning, nonObject, _config.RarityThresholds, TheLongestYear.Core.YearTwoCrops.ExcludedFor(state.HasUpgrade, difficulty.Steps.ItemRarity), difficulty);
                    engine.Availability = _availability;
                    GeneratedBundleSet set = engine.Generate(seed, TheLongestYear.Loop.BundleEngine.TrimFor(state));
                    if (!EngineManifestCheck.Matches(set.ToBundleData(), liveData))
                        continue;

                    var requirements = engine.BuildRequirements(
                        set, itemSeasonPins, bundleQuotas,
                        SeasonPity.CurrentQuotaEase(state, _config), _availability);
                    string flagNote = nonObject == _config.EnableNonObjectDonations
                        ? ""
                        : $"; board was generated with EnableNonObjectDonations={nonObject} — honouring it this loop, the current setting applies from the next reset";
                    this.Monitor.Log(
                        $"Requirements source: engine manifest (loop {state.CompletedResets}, seed loop {state.EffectiveBundleSeedLoop}, {requirements.Count} bundles{flagNote}).",
                        LogLevel.Info);
                    return requirements;
                }

                this.Monitor.Log(
                    "ResolveRequirements: engine manifest mismatch (stale or foreign bundle data), " +
                    "falling back to read path; any season-pity easing on the held board is not applied on this path.",
                    LogLevel.Warn);
                // fall through to the legacy read-and-classify path below.
            }
            else if (source == RequirementsSource.GenerateFreshRun)
            {
                // The first run has never been through a reset, so nothing has stamped a profile
                // yet. Stamp it here, before generating, so this board and the rest of loop 1 run
                // under the same values a later reset would re-stamp.
                state.Difficulty = TheLongestYear.Core.DifficultyResolver.Resolve(_config.Difficulty, _config);
                BundleGenerationTuning freshTuning =
                    TheLongestYear.Core.DifficultyTuning.Scale(_config.PoolTuning, state.Difficulty);
                var engine = new TheLongestYear.Loop.BundleEngine(this.Monitor, freshTuning, _config.EnableNonObjectDonations, _config.RarityThresholds, TheLongestYear.Core.YearTwoCrops.ExcludedFor(_meta.State.HasUpgrade, state.Difficulty.Steps.ItemRarity), state.Difficulty);
                engine.Availability = _availability;
                GeneratedBundleSet set = engine.Generate(BundleEngineSeed.For(seedBasis, 0));
                engine.WriteToWorld(set, this.Monitor);
                state.BundlesGeneratedForReset = 0;
                var requirements = engine.BuildRequirements(
                    set, itemSeasonPins, bundleQuotas, ease: null, availability: _availability);
                this.Monitor.Log(
                    $"Requirements source: engine generation (fresh run, {requirements.Count} bundles written).",
                    LogLevel.Info);
                return requirements;
            }

            var legacyRequirements = builder.BuildRequirements();
            if (vanillaSource)
            {
                if (string.IsNullOrEmpty(state.VanillaBundleType))
                    state.VanillaBundleType = InferVanillaBundleType();
                this.Monitor.Log(
                    $"Requirements source: vanilla board (BundleSource=Vanilla, {state.VanillaBundleType}; read-and-classify, {legacyRequirements.Count} bundles; regenerated the same way at each reset).",
                    LogLevel.Info);
            }
            else
            {
                this.Monitor.Log(
                    "Requirements source: legacy read-and-classify (pre-engine save; regenerates at next reset).",
                    LogLevel.Info);
            }
            return legacyRequirements;
        }

        /// <summary>Standard vs Remixed for a Vanilla-mode save that predates the persisted
        /// choice: the live board IS the Data/Bundles asset (value-for-value) ⇒ Default,
        /// anything else ⇒ Remixed. A Content-Patcher-edited Data/Bundles compares equal too,
        /// which is right — GenerateBundles(Default) reproduces it.</summary>
        private string InferVanillaBundleType()
        {
            try
            {
                var standard = Game1.content.Load<Dictionary<string, string>>("Data\\Bundles");
                bool isStandard = BoardInspection.MatchesReference(Game1.netWorldState.Value.BundleData, standard);
                string type = isStandard ? Game1.BundleType.Default.ToString() : Game1.BundleType.Remixed.ToString();
                this.Monitor.Log($"VanillaBundleType inferred from the live board: {type}.", LogLevel.Info);
                return type;
            }
            catch (Exception ex)
            {
                this.Monitor.Log($"VanillaBundleType inference failed ({ex.GetType().Name}: {ex.Message}) — assuming Default.", LogLevel.Warn);
                return Game1.BundleType.Default.ToString();
            }
        }

        /// <summary>Vanilla mode only: if another mod rewrote BundleData since we classified
        /// (Challenging CC Bundles swaps values on DayStarted, AFTER our SaveLoaded), rebuild
        /// the catalog + requirements from the live board so season goals, weekly-theme pools
        /// and the donation patches follow what the CC actually shows.</summary>
        private void ReclassifyIfBoardChanged()
        {
            if (_boardBuilder == null || !BundleSourceNames.IsVanilla(_meta.State.BundleSource)) return;
            string fingerprint = BoardInspection.Fingerprint(Game1.netWorldState.Value.BundleData);
            if (fingerprint == _boardFingerprint) return;

            _boardFingerprint = fingerprint;
            _catalog = _boardBuilder.Build();
            _requirements = _boardBuilder.BuildRequirements();
            _runController?.ReplaceCatalog(_catalog);
            _runController?.ReplaceRequirements(_requirements);
            TheLongestYear.Patches.BundleDonationPatches.LiveBoardHasNonObjectSlots =
                BoardInspection.HasNonObjectIngredients(Game1.netWorldState.Value.BundleData);
            this.Monitor.Log(
                $"Board changed by another mod since load — re-classified from the live data ({_requirements.Count} bundles, {_catalog.Count} catalog items).",
                LogLevel.Info);
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
        /// <summary>Localised label for a difficulty step in the GMCM dropdown. Written as four
        /// literal <see cref="Strings.Get"/> calls rather than an interpolated key so the i18n
        /// guard's source scan can prove all four keys are reachable.</summary>
        /// <summary>Localised label for a bundle-source choice. Written as literal
        /// <see cref="Strings.Get"/> calls rather than an interpolated key so the i18n guard's
        /// source scan can prove every key is reachable.</summary>
        private static string FormatBundleSource(string rawValue)
        {
            if (string.Equals(rawValue, BundleSourceNames.Normal, StringComparison.OrdinalIgnoreCase))
                return Strings.Get("gmcm.bundle-source.normal");
            if (string.Equals(rawValue, BundleSourceNames.Remixed, StringComparison.OrdinalIgnoreCase))
                return Strings.Get("gmcm.bundle-source.remixed");
            return Strings.Get("gmcm.bundle-source.engine");
        }

        private static string FormatDifficultyStep(string rawValue) => DifficultySteps.Parse(rawValue) switch
        {
            DifficultyStep.Easy => Strings.Get("gmcm.difficulty.step.easy"),
            DifficultyStep.Hard => Strings.Get("gmcm.difficulty.step.hard"),
            DifficultyStep.Extreme => Strings.Get("gmcm.difficulty.step.extreme"),
            _ => Strings.Get("gmcm.difficulty.step.normal"),
        };

    }
}
