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
        private IReadOnlyDictionary<string, int> _ingredientStacks = new Dictionary<string, int>();
        private DonationObserver _donationObserver;
        private PeakMineFloorTracker _peakMineFloorTracker;
        private JunimoStashService _stashService;
        private WeeklyThemeQuestService _questService;
        private IntroEventInjector _introInjector;
        private IntroSequenceDriver _introDriver;
        private BookFurniture _bookFurniture;
        private UI.PlanningShrineService _planningShrine;

        // Debug command-file bridge: lets the developer trigger tly_ actions by writing lines into a file
        // in the mod folder, so PC in-game testing needs no console typing (the mod polls + executes them).
        private const string DebugCommandFileName = "tly_commands.txt";
        private const int DebugPollTicks = 30;
        private string _commandFilePath;

        public override void Entry(IModHelper helper)
        {
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
            // Placeable book furniture (Cookbook/Craftbook/Bundle-log) — registers via asset edit.
            _bookFurniture = new BookFurniture(this.Monitor, helper);
            // View-only planning shrine — registers its furniture + auto-places near the stash.
            _planningShrine = new UI.PlanningShrineService(this.Monitor, helper);
            helper.Events.GameLoop.SaveLoaded += this.OnSaveLoaded;
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

            // Activate the seed-driven weather scheduler. Toggled at Entry rather than
            // OnSaveLoaded so the override is in place for the very first day's weather
            // resolution (which happens during save creation, before SaveLoaded fires).
            WeatherModificationsPatch.Enabled = _config.Enabled;

            // Observation-based donation detector. See DonationObserver.cs for why we can't rely
            // on a Harmony patch of Bundle.tryToDepositThisItem alone (the 2026-05-26 playtest
            // showed it didn't fire on real CC deposits).
            _donationObserver = new DonationObserver(helper, this.Monitor);

            // The Cookbook, Craftbook, and Bundle-log are placeable book furniture now
            // (see BookFurniture) — no tile-anchored interactables.

            _commandFilePath = Path.Combine(helper.DirectoryPath, DebugCommandFileName);

            helper.ConsoleCommands.Add("tly_meta", "Print The Longest Year meta-state (requires a loaded save).", this.PrintMeta);
            helper.ConsoleCommands.Add("tly_addjp", "Add Junimo Points in memory; persists on the next save. Usage: tly_addjp <amount>", this.AddJp);
            helper.ConsoleCommands.Add("tly_addmoney", "Add gold to the loaded farmer (debug). Usage: tly_addmoney <amount>", this.AddMoney);
            helper.ConsoleCommands.Add("tly_reset", "Force an in-place reset to Spring 1 (debug).", this.ForceReset);
            helper.ConsoleCommands.Add("tly_failreset", "Simulate a day-28 gate-miss reset: opens the JP shrine, then resets to Spring 1 on close (debug — exercises the natural loop-reset path the JP-refund bug lived in).", this.CmdFailReset);
            helper.ConsoleCommands.Add("tly_resetif", "Reset only if the loaded farmer's name matches. Usage: tly_resetif <name>", this.ResetIfNameMatches);
            helper.ConsoleCommands.Add("tly_leaktest", "Reset twice and report any state that leaks between runs (debug).", this.LeakTest);
            helper.ConsoleCommands.Add("tly_select", "Select one of this week's offered themes. Usage: tly_select <theme>", this.CmdSelect);
            helper.ConsoleCommands.Add("tly_offer", "Show this week's selection offer.", this.CmdOffer);
            helper.ConsoleCommands.Add("tly_donate", "Simulate a CC donation. Usage: tly_donate <itemId>", this.CmdDonate);
            helper.ConsoleCommands.Add("tly_runstate", "Print the current run state.", this.CmdRunState);
            helper.ConsoleCommands.Add("tly_catalog", "Print the bundle-derived CC catalog summary.", this.CmdCatalog);
            helper.ConsoleCommands.Add("tly_testdonate", "Simulate a CC donation through the JP service. Usage: tly_testdonate <qualifiedId> [count]", this.CmdTestDonate);
            helper.ConsoleCommands.Add("tly_openhub", "Open the weekly planning hub menu (debug).", this.CmdOpenHub);
            helper.ConsoleCommands.Add("tly_openshop", "Open the Junimo Shrine upgrade shop (debug).", this.CmdOpenShop);
            helper.ConsoleCommands.Add("tly_listupgrades", "List the upgrade catalog grouped by category.", this.CmdListUpgrades);
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
            if (!_config.Enabled)
            {
                this.Monitor.Log("TLY disabled in config — skipping all save-load setup.", LogLevel.Info);
                return;
            }

            // Standard farm only. Tile defaults + building placement coords assume
            // the Standard farm layout. Other farm types (Riverland, Forest, Beach, etc.)
            // would land the stash chest / cookbook / craftbook / pre-built coops + barns
            // in unpredictable places (or in water). Skip setup with a clear log message.
            if (Game1.whichFarm != 0)
            {
                this.Monitor.Log(
                    $"TLY only supports the Standard farm (Game1.whichFarm == 0). " +
                    $"Current farm type is {Game1.whichFarm}. Skipping all setup. " +
                    $"To use TLY, start a new game on the Standard farm.",
                    LogLevel.Info);
                return;
            }

            _meta.Load();
            // Inject the tly_intro_done mail flag now if the player has already seen the intro
            // on a prior loop — that's what suppresses both intro events for years 2+.
            _introInjector?.ApplyMailFlagsForRun();
            UpgradeChecker.HasUpgrade = id => _meta.State.HasUpgrade(id);
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
            PatchLog.Connect(this.Monitor);
            _reset = new WorldResetService(
                this.Monitor, _meta.State, _meta.Run, _config, _ccUnlock,
                this.Helper.DirectoryPath, farmerReset, professionPicker,
                _stashService, _mountainUnlock, _bookFurniture, _planningShrine);

            _seasonResolver = new SeasonResolver();
            var builder = new BundleCatalogBuilder(
                _config.RarityThresholds, _seasonResolver, this.Monitor,
                ParseThemeOverrides(),
                ParseItemSeasonPins(),
                ParseBundleQuotas());
            _catalog = builder.Build();
            _requirements = builder.BuildRequirements();
            _ingredientStacks = builder.BuildIngredientStacks();
            var ingredientQualities = builder.BuildIngredientQualities();
            DonationService.Active = new DonationService(this.Monitor, _meta, _config);

            _questService = new WeeklyThemeQuestService(
                this.Monitor, _meta, _config,
                stackForIngredient: id => _ingredientStacks.TryGetValue(id, out int s) ? s : 1);
            // Wire the post-donation callback so each CC deposit refreshes the quest's progress
            // text (and auto-completes when every bonus item this week has been donated).
            DonationService.Active.AfterDonation = _questService.OnItemDonated;

            _runController = new RunController(this.Monitor, _meta, _config, _reset, _catalog, _requirements, _ingredientStacks, ingredientQualities);
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

        /// <summary>Commit meta-state as part of the game's save — never eagerly, to prevent save-scumming.</summary>
        private void OnSaving(object sender, SavingEventArgs e)
        {
            // Promote per-run tly_intro_cc_seen mail to cross-run MetaState.HasSeenIntro BEFORE
            // we persist, so a save+reset can't lose the flag (mailReceived gets wiped by
            // FarmerReset.loadForNewGame, MetaState doesn't).
            _introInjector?.MarkIntroSeenIfApplicable();
            _meta.Save();
            this.Monitor.Log($"Meta-state saved with the game. JP banked: {_meta.State.JunimoPoints}.", LogLevel.Trace);
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
            _reset.PerformReset();
            _reset.ProfessionPicker.DrainOnDayStart();
            _meta.Run.BeginNewRun(NewRunSeed());
            _meta.Save();   // persist the cleared state so deferred SaveLoaded can't revert it
            this.Monitor.Log(
                $"Reset complete. Run {_meta.Run.RunNumber} begins (seed {_meta.Run.Seed}). " +
                "Opening week 1 hub.",
                LogLevel.Info);
            _runController?.PresentOffer(targetWeekOfYear: 1);
        }

        /// <summary>Pure-random reset seed for inline resets (the OnDayStarted reset path uses its
        /// own seed via RunController.NewSeed; this is the simpler external entry point).</summary>
        private static int NewRunSeed() => new Random().Next();

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
            var gmcm = this.Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
            if (gmcm == null)
            {
                this.Monitor.Log("GMCM not installed — config edits via config.json only.", LogLevel.Trace);
                return;
            }

            gmcm.Register(this.ModManifest,
                reset: () => _config = new GameplayConfig(),
                save: () => this.Helper.WriteConfig(_config));

            gmcm.AddSectionTitle(this.ModManifest, () => "The Longest Year");
            gmcm.AddParagraph(this.ModManifest,
                () => "Master switch. When off, TLY skips all setup at save load and no effects fire. " +
                      "Toggling takes effect on the next save load.");
            gmcm.AddBoolOption(this.ModManifest,
                getValue: () => _config.Enabled,
                setValue: v => _config.Enabled = v,
                name: () => "Enabled");

            gmcm.AddBoolOption(this.ModManifest,
                getValue: () => _config.ShowJpHud,
                setValue: v => _config.ShowJpHud = v,
                name: () => "Show JP HUD",
                tooltip: () => "Always-on corner counter showing banked JP and the current week's theme.");

            this.Monitor.Log("Registered GMCM options.", LogLevel.Info);
        }

        private void OnDayStarted(object sender, StardewModdingAPI.Events.DayStartedEventArgs e)
            => _runController?.OnDayStarted(sender, e);

        private void OnDayEnding(object sender, StardewModdingAPI.Events.DayEndingEventArgs e)
            => _runController?.OnDayEnding(sender, e);


        /// <summary>
        /// Poll the debug command file (mod folder) and execute any queued lines once. Lets the developer
        /// drive tly_ actions by writing the file while the player only plays — no in-game console needed.
        /// </summary>
        private void OnUpdateTicked(object sender, UpdateTickedEventArgs e)
        {
            if (!Context.IsWorldReady)
                return;

            // Festival auto-eject runs every tick (cheap conditional — most ticks bail in the first check).
            // Has to be every tick, not just on the DebugPollTicks cadence, so we eject right at the
            // festival's end time rather than up to 30 ticks (~500ms) later.
            if (FestivalTimeFlow.ShouldAutoEnd())
                FestivalTimeFlow.ForceEnd(this.Monitor);

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
                case "tly_addjp": this.AddJp(command, args); break;
                case "tly_addmoney": this.AddMoney(command, args); break;
                case "tly_reset": this.ForceReset(command, args); break;
                case "tly_resetif": this.ResetIfNameMatches(command, args); break;
                case "tly_leaktest": this.LeakTest(command, args); break;
                case "tly_select": this.CmdSelect(command, args); break;
                case "tly_offer": this.CmdOffer(command, args); break;
                case "tly_donate": this.CmdDonate(command, args); break;
                case "tly_runstate": this.CmdRunState(command, args); break;
                case "tly_catalog": this.CmdCatalog(command, args); break;
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
            _runController.SelectByName(args[0]);
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
                this.Monitor.Log("Usage: tly_payvault <Spring|Summer|Fall|Winter|34|35|36|37>", LogLevel.Warn);
                return;
            }

            int bundleIndex;
            if (int.TryParse(args[0], out bundleIndex))
            {
                // direct index
            }
            else if (System.Enum.TryParse(args[0], ignoreCase: true, out TheLongestYear.Core.Season s))
            {
                bundleIndex = TheLongestYear.Core.VaultRules.BundleIndexForSeason(s);
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
