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
using TheLongestYear.Loop;
using TheLongestYear.UI;

namespace TheLongestYear
{
    public sealed class ModEntry : Mod
    {
        private GameplayConfig _config;
        private MetaStore _meta;
        private CommunityCenterUnlock _ccUnlock;
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

        // Debug command-file bridge: lets the developer trigger tly_ actions by writing lines into a file
        // in the mod folder, so PC in-game testing needs no console typing (the mod polls + executes them).
        private const string DebugCommandFileName = "tly_commands.txt";
        private const int DebugPollTicks = 30;
        private string _commandFilePath;

        public override void Entry(IModHelper helper)
        {
            _config = helper.ReadConfig<GameplayConfig>();
            _meta = new MetaStore(helper.Data);
            helper.Events.GameLoop.SaveLoaded += this.OnSaveLoaded;
            helper.Events.GameLoop.Saving += this.OnSaving;
            helper.Events.GameLoop.DayStarted += this.OnDayStarted;
            helper.Events.GameLoop.DayEnding += this.OnDayEnding;
            helper.Events.GameLoop.UpdateTicked += this.OnUpdateTicked;
            helper.Events.Display.RenderedHud += this.OnRenderedHud;
            helper.Events.Display.RenderedWorld += IndicatorRegistry.OnRenderedWorld;

            var harmony = new Harmony(this.ModManifest.UniqueID);
            harmony.PatchAll();

            // Observation-based donation detector. See DonationObserver.cs for why we can't rely
            // on a Harmony patch of Bundle.tryToDepositThisItem alone (the 2026-05-26 playtest
            // showed it didn't fire on real CC deposits).
            _donationObserver = new DonationObserver(helper, this.Monitor);

            // Interactable Season Goals board inside the Community Center. MenuLauncher isn't
            // constructed until OnSaveLoaded, so we hand the board a lazy accessor instead of
            // the instance — the Harmony prefix on GameLocation.checkAction resolves it just-
            // in-time. PatchAll() above already discovered the board's static checkAction patch.
            SeasonGoalsBoard.ConnectTo(helper, this.Monitor, _config, () => _launcher);
            CookbookInteractable.ConnectTo(this.Monitor, _config, _meta);
            CraftbookInteractable.ConnectTo(this.Monitor, _config, _meta);

            _commandFilePath = Path.Combine(helper.DirectoryPath, DebugCommandFileName);

            helper.ConsoleCommands.Add("tly_meta", "Print The Longest Year meta-state (requires a loaded save).", this.PrintMeta);
            helper.ConsoleCommands.Add("tly_addjp", "Add Junimo Points in memory; persists on the next save. Usage: tly_addjp <amount>", this.AddJp);
            helper.ConsoleCommands.Add("tly_reset", "Force an in-place reset to Spring 1 (debug).", this.ForceReset);
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
            helper.ConsoleCommands.Add("tly_setboard", "Anchor the Season Goals board to the player's current tile inside the CC. Writes config.json.", this.CmdSetBoard);
            helper.ConsoleCommands.Add("tly_setcookbook",
                "Anchor the Cookbook to the tile you are facing in the FarmHouse. Writes config.json.",
                this.CmdSetCookbook);
            helper.ConsoleCommands.Add("tly_setcraftbook",
                "Anchor the Craftbook to the tile you are facing in the FarmHouse. Writes config.json.",
                this.CmdSetCraftbook);
            helper.ConsoleCommands.Add("tly_opencookbook",
                "Open the Cookbook menu directly (debug).",
                this.CmdOpenCookbook);
            helper.ConsoleCommands.Add("tly_opencraftbook",
                "Open the Craftbook menu directly (debug).",
                this.CmdOpenCraftbook);

            this.Monitor.Log("The Longest Year loaded.", LogLevel.Info);
        }

        /// <summary>Load this playthrough's banked progress when a save opens.</summary>
        private void OnSaveLoaded(object sender, SaveLoadedEventArgs e)
        {
            _meta.Load();
            UpgradeChecker.HasUpgrade = id => _meta.State.HasUpgrade(id);
            IndicatorRegistry.Attach(_meta.State);
            IndicatorRegistry.ClearRegistrations();
            _ccUnlock = new CommunityCenterUnlock(this.Monitor);
            _ccUnlock.Apply();
            var farmerReset = new FarmerReset(this.Monitor);
            var professionPicker = new ProfessionPickerScheduler(this.Monitor);
            _reset = new WorldResetService(
                this.Monitor, _meta.State, _meta.Run, _config, _ccUnlock,
                this.Helper.DirectoryPath, farmerReset, professionPicker);

            _seasonResolver = new SeasonResolver();
            var builder = new BundleCatalogBuilder(
                _config.RarityThresholds, _seasonResolver, this.Monitor,
                ParseThemeOverrides(),
                ParseItemSeasonPins(),
                ParseBundleQuotas());
            _catalog = builder.Build();
            _requirements = builder.BuildRequirements();
            _ingredientStacks = builder.BuildIngredientStacks();
            DonationService.Active = new DonationService(this.Monitor, _meta, _config);

            _runController = new RunController(this.Monitor, _meta, _config, _reset, _catalog, _requirements, _ingredientStacks);
            _runController.OnRunLoaded();
            if (_peakMineFloorTracker != null)
                this.Helper.Events.Player.Warped -= _peakMineFloorTracker.OnWarped;
            _peakMineFloorTracker = new PeakMineFloorTracker(this.Monitor, _meta.Run);
            this.Helper.Events.Player.Warped += _peakMineFloorTracker.OnWarped;
            _reset.RegisterIndicators();
            _purchases = new UpgradePurchaseService(this.Monitor, _meta);
            _launcher = new MenuLauncher(this.Monitor, _config, _meta, _runController, _purchases);
            _runController.AttachLauncher(_launcher);
            this.Monitor.Log(
                $"Run {_meta.Run.RunNumber} loaded ({_meta.Run.Season} {_meta.Run.DayOfMonth}). JP banked: {_meta.State.JunimoPoints}.",
                LogLevel.Info);
        }

        /// <summary>Commit meta-state as part of the game's save — never eagerly, to prevent save-scumming.</summary>
        private void OnSaving(object sender, SavingEventArgs e)
        {
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
            this.Monitor.Log(
                $"JP={s.JunimoPoints}, StashTier={s.StashCapacityTier}, Upgrades=[{string.Join(", ", s.OwnedUpgrades)}]",
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

        /// <summary>Anchor the Season Goals board to the tile the player is currently FACING
        /// (not standing on — vanilla checkAction dispatches based on the faced tile, so we
        /// record that one so the player can stand next to e.g. the fireplace, face it, and
        /// press the action button to open the menu). Writes config.json so the anchor
        /// persists across launches. Refuses to set outside the CC — the board only fires there.</summary>
        private void CmdSetBoard(string command, string[] args)
        {
            if (!Context.IsWorldReady)
            {
                this.Monitor.Log("Load a save first.", LogLevel.Warn);
                return;
            }

            if (Game1.currentLocation is not StardewValley.Locations.CommunityCenter)
            {
                this.Monitor.Log(
                    "tly_setboard: stand inside the Community Center first — the board only " +
                    "fires there.",
                    LogLevel.Warn);
                return;
            }

            // Compute the tile the player is facing. Facing direction is 0=up, 1=right, 2=down, 3=left.
            int dx = Game1.player.FacingDirection == 1 ? 1 : Game1.player.FacingDirection == 3 ? -1 : 0;
            int dy = Game1.player.FacingDirection == 2 ? 1 : Game1.player.FacingDirection == 0 ? -1 : 0;
            int x = (int)Game1.player.Tile.X + dx;
            int y = (int)Game1.player.Tile.Y + dy;

            _config.SeasonGoalsBoardTileX = x;
            _config.SeasonGoalsBoardTileY = y;
            this.Helper.WriteConfig(_config);

            this.Monitor.Log(
                $"Season Goals board anchored to ({x}, {y}) — the tile you were facing. " +
                "Saved to config.json. Stand in the same spot and press action to test.",
                LogLevel.Info);
        }

        private void CmdSetCookbook(string command, string[] args)
        {
            if (!Context.IsWorldReady) { this.Monitor.Log("Load a save first.", LogLevel.Warn); return; }
            if (Game1.currentLocation is not StardewValley.Locations.FarmHouse)
            {
                this.Monitor.Log("tly_setcookbook: stand inside the FarmHouse first.", LogLevel.Warn);
                return;
            }
            int dx = Game1.player.FacingDirection == 1 ? 1 : Game1.player.FacingDirection == 3 ? -1 : 0;
            int dy = Game1.player.FacingDirection == 2 ? 1 : Game1.player.FacingDirection == 0 ? -1 : 0;
            _config.CookbookTileX = (int)Game1.player.Tile.X + dx;
            _config.CookbookTileY = (int)Game1.player.Tile.Y + dy;
            this.Helper.WriteConfig(_config);
            this.Monitor.Log($"Cookbook anchored to ({_config.CookbookTileX}, {_config.CookbookTileY}). Saved to config.json.", LogLevel.Info);
        }

        private void CmdSetCraftbook(string command, string[] args)
        {
            if (!Context.IsWorldReady) { this.Monitor.Log("Load a save first.", LogLevel.Warn); return; }
            if (Game1.currentLocation is not StardewValley.Locations.FarmHouse)
            {
                this.Monitor.Log("tly_setcraftbook: stand inside the FarmHouse first.", LogLevel.Warn);
                return;
            }
            int dx = Game1.player.FacingDirection == 1 ? 1 : Game1.player.FacingDirection == 3 ? -1 : 0;
            int dy = Game1.player.FacingDirection == 2 ? 1 : Game1.player.FacingDirection == 0 ? -1 : 0;
            _config.CraftbookTileX = (int)Game1.player.Tile.X + dx;
            _config.CraftbookTileY = (int)Game1.player.Tile.Y + dy;
            this.Helper.WriteConfig(_config);
            this.Monitor.Log($"Craftbook anchored to ({_config.CraftbookTileX}, {_config.CraftbookTileY}). Saved to config.json.", LogLevel.Info);
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

        private void ForceReset(string command, string[] args)
        {
            if (!Context.IsWorldReady)
            {
                this.Monitor.Log("Load a save first.", LogLevel.Warn);
                return;
            }

            FullResetAndPresentOffer();
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

        /// <summary>Re-draw the clock/date/money HUD during festivals. Vanilla's drawHUD
        /// short-circuits on eventUp (Game1.cs:15410) so only the menu button is drawn during
        /// festivals — that was sensible when time was frozen, but with FestivalTimeFlow
        /// active the clock actually means something so the user wants to see it.</summary>
        private void OnRenderedHud(object sender, StardewModdingAPI.Events.RenderedHudEventArgs e)
        {
            if (!Game1.isFestival() || Game1.dayTimeMoneyBox == null)
                return;
            Game1.dayTimeMoneyBox.draw(e.SpriteBatch);
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
                case "tly_setboard": this.CmdSetBoard(command, args); break;
                case "tly_setcookbook":   this.CmdSetCookbook(command, args); break;
                case "tly_setcraftbook":  this.CmdSetCraftbook(command, args); break;
                case "tly_opencookbook":  this.CmdOpenCookbook(command, args); break;
                case "tly_opencraftbook": this.CmdOpenCraftbook(command, args); break;
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
