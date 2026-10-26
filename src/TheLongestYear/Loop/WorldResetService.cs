using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarmonyLib;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Utilities;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.Characters;
using StardewValley.Locations;
using StardewValley.Network;
using StardewValley.Quests;
using TheLongestYear.Core;
using TheLongestYear.UI;
using CoreSeason = TheLongestYear.Core.Season;

namespace TheLongestYear.Loop
{
    /// <summary>
    /// Performs the in-place rewind to Spring 1. Reuses the game's own new-game initializer
    /// (Game1.loadForNewGame) for the heavy lifting — it clears Game1.locations, rebuilds the Farm and
    /// every location, regenerates the CC bundles (wiping donation progress), and re-adds NPCs — then
    /// applies targeted resets for the persistent Farmer and mine progress. "Fix the data, don't fight
    /// the system": no per-collection hacks.
    /// </summary>
    internal sealed partial class WorldResetService
    {
        private readonly IMonitor _monitor;
        private readonly TheLongestYear.Core.MetaState _meta;
        private readonly CommunityCenterUnlock _ccUnlock;
        private readonly string _modDirectory;
        private readonly RunState _run;
        private readonly GameplayConfig _config;
        private readonly FarmerReset _farmerReset;
        private readonly ProfessionPickerScheduler _professionPicker;
        private readonly JunimoStashService _stashService;
        private readonly MountainUnlock _mountainUnlock;
        private readonly TheLongestYear.Integration.BookFurniture _bookFurniture;
        /// <summary>Re-grants the owned Circles of Warding after the wipe (CircleOfWardingService.Reconcile).</summary>
        public System.Action ReconcileCircles { get; set; }
        private readonly TheLongestYear.UI.PlanningShrineService _planningShrine;
        private readonly IReadOnlyDictionary<string, CoreSeason> _itemSeasonPins;
        private readonly IReadOnlyDictionary<string, int[]> _bundleQuotas;
        private readonly IGameContentHelper _gameContent;

        /// <summary>The pre-reset save folder recorded in <see cref="PerformReset"/>, deleted by
        /// <see cref="CleanupAbandonedSaveFolder"/> only after the post-reset full save confirms the
        /// new canonical folder. Null when there's nothing to clean up.</summary>
        private string _abandonedSaveFolder;

        /// <summary>Other mods' save data copied to the new save key in <see cref="PerformReset"/>;
        /// the old-keyed copies go with the old save folder in <see cref="CleanupAbandonedSaveFolder"/>.</summary>
        private SaveDataCarryResult _carriedModData;

        /// <summary>Every loaded mod's folder, searched for per-save data keyed on the old save
        /// (ModSaveDataCarryover). Set by ModEntry; null searches only SMAPI's global mod data.</summary>
        public Func<IEnumerable<string>> OtherModFolders { get; set; }

        /// <summary>Per-stage stopwatch for the last <see cref="PerformReset"/> (freeze investigation,
        /// TODO "Closing the post-loop shrine freezes the game"). Rebuilt at the top of each reset.</summary>
        private ResetTiming _timing;

        public ProfessionPickerScheduler ProfessionPicker => _professionPicker;

        /// <summary>Derived item model, forwarded to the classifier when the reset regenerates a
        /// board so its PerItem bundles gate on computed deadlines instead of the curated pin
        /// table. Settable rather than a constructor argument because ModEntry builds this service
        /// before the engine pools the model is derived from exist; ModEntry sets it in the same
        /// pass, right after the pools are built. Null until then (and on any host that never sets
        /// it), which keeps the legacy pin-table path.</summary>
        public TheLongestYear.Core.ItemAvailabilityModel AvailabilityModel { get; set; }

        /// <summary>Tech's Cross-Mod Bundles, for the Normal and Remixed reroll (spec 2026-10-08 addendum 2).
        /// Null skips it.</summary>
        public TheLongestYear.Core.ITechBundlesRerollTarget TechBundles { get; set; }

        /// <summary>Rebuilds <see cref="AvailabilityModel"/> for one difficulty step, called right
        /// after <see cref="PerformReset"/> re-resolves the new run's difficulty (spec
        /// 2026-08-28-obtainable-board, section 1: the week mode is a function of that step). Set by
        /// ModEntry to <c>BuildAvailabilityModelFor</c>, the same method that builds the model at
        /// SaveLoaded, so the two builds can never drift apart. Null on any host that never sets it,
        /// which keeps <see cref="AvailabilityModel"/> at whatever SaveLoaded built.</summary>
        public Func<TheLongestYear.Core.DifficultyStep, TheLongestYear.Core.ItemAvailabilityModel> RebuildAvailabilityModel { get; set; }

        /// <summary>The bundle-requirement manifest the most recent <see cref="PerformReset"/> call
        /// generated for the new loop (owned-bundle engine wiring) -- RunController.FinalizeReset
        /// re-injects this into the run via RunController.ReplaceRequirements right after PerformReset
        /// returns. Null until the first PerformReset call.</summary>
        public IReadOnlyList<BundleRequirement> LastGeneratedRequirements { get; private set; }

        public WorldResetService(
            IMonitor monitor,
            TheLongestYear.Core.MetaState meta,
            TheLongestYear.Core.RunState run,
            TheLongestYear.Core.GameplayConfig config,
            CommunityCenterUnlock ccUnlock,
            string modDirectory,
            FarmerReset farmerReset,
            ProfessionPickerScheduler professionPicker,
            JunimoStashService stashService,
            MountainUnlock mountainUnlock,
            TheLongestYear.Integration.BookFurniture bookFurniture,
            TheLongestYear.UI.PlanningShrineService planningShrine,
            IReadOnlyDictionary<string, CoreSeason> itemSeasonPins,
            IReadOnlyDictionary<string, int[]> bundleQuotas,
            IGameContentHelper gameContent = null)
        {
            _monitor = monitor;
            _meta = meta;
            _run = run;
            _config = config;
            _ccUnlock = ccUnlock;
            _modDirectory = modDirectory;
            _farmerReset = farmerReset;
            _professionPicker = professionPicker;
            _stashService = stashService;
            _mountainUnlock = mountainUnlock;
            _bookFurniture = bookFurniture;
            _planningShrine = planningShrine;
            _itemSeasonPins = itemSeasonPins;
            _bundleQuotas = bundleQuotas;
            _gameContent = gameContent;
        }

        /// <summary>The in-place rewind to Spring 1, as named phases in their fixed order (each phase
        /// lives in the WorldResetService.ResetPhases*.cs files). The order matters: captures run before
        /// loadForNewGame wipes the world, the board is written after the farmer reset, and the farm is
        /// placed after the board.</summary>
        public void PerformReset()
        {
            // One-time safety backup before the first destructive reset (a failed backup is logged and the reset goes ahead).
            // Lands inside the mod folder (not in Stardew's Saves dir) so it doesn't appear as a second
            // save on the title screen.
            _timing = new ResetTiming(_monitor, "PerformReset");
            SaveBackup.BackupOnce(_meta, _monitor, _modDirectory);
            _timing.Mark("0 backup");

            _monitor.Log("In-place reset: starting.", LogLevel.Info);

            var ctx = new ResetContext();
            ResolveDifficulty(ctx);
            ResolveBoardSource(ctx);
            SnapshotHeldVanillaBoard(ctx);
            ReseedWorld();
            CaptureCarryovers(ctx);

            // 1. The game's own new-game initializer rebuilds the world + regenerates CC bundles.
            _timing.Mark("0 stamps, snapshots, reseed");
            Game1.game1.loadForNewGame(loadedGame: false);
            _timing.Mark("1 loadForNewGame");

            // 1-display. Put the zoom + UI scale back on the new Options instance. Game1.Update
            // notices the change on its next tick and calls refreshWindowSettings itself.
            RestoreStep("display options", () => DisplayOptionsCarryover.Restore(ctx.DisplayOptions, _monitor));

            // 1-seeds. First-loop-only starting seeds: loadForNewGame rebuilds the FarmHouse, whose
            // constructor (AddStarterGiftBox) drops a starter gift box of 15 parsnip seeds. FarmerReset
            // wipes the inventory but not this placed box, so it reappears every loop. PerformReset only
            // runs on resets (never the first new game), so removing it here means run 1 keeps the
            // vanilla nudge and every loop after gets none.
            RestoreStep("starter gift box", RemoveStarterGiftBox);

            ResetCommunityCenterState();
            ResetWorldStateCollections();
            _timing.Mark("1a-1d CC bundles, museum, books, netWorldState wipes");
            RewindCalendar();
            _timing.Mark("2 calendar, weather, netWorldState sync");
            ResetFarmer(ctx);
            _timing.Mark("3-7 farmer reset, professions, mine, robin, cellar");
            RestoreKeptFarm(ctx);

            _timing.Mark("8-10 kept buildings, horse, animals, pet");
            // 11. Bump CompletedResets — the single producer for the season:N meta-requirement.
            _meta.CompletedResets += 1;

            WriteBoard(ctx);
            _timing.Mark("11a board generation (engine or vanilla)");
            RestoreGiftsAndIntros(ctx);
            RestoreFarmAndHouse(ctx);
            _timing.Mark("11b-13 kept gifts, book quests, stash chest, shrine, decor");
            ReapplyWorldUnlocks();

            _timing.Mark("14 player placement, furniture, books, circles");
            // Money earned rewinds with the year. Last, so the starting gold and any gold a restore
            // step hands out are not counted as this year's earnings.
            RestoreStep("money earned", _farmerReset.ResetMoneyEarned);
            _timing.Mark("15 money earned");
            _timing.Total();

            _monitor.Log(
                $"In-place reset: complete. {Game1.season} {Game1.dayOfMonth}, money {Game1.player.Money}. " +
                $"Reset #{_meta.CompletedResets}.",
                LogLevel.Info);
        }

        /// <summary>Debug only (tly_fixcheck failstep): the restore step with this name throws on the
        /// next reset, to check a failing step does not strand the run. Null in play.</summary>
        internal static string DebugFailStep;

        /// <summary>One carry-over step after loadForNewGame. By then the old world is gone, so a
        /// throw must not abort the reset: that stranded the player on a fresh Spring 1 farm with
        /// the old run's state and the HUD hidden, and the next save kept that mix. Log the step
        /// and keep going; the rest of the reset still lands.</summary>
        private void RestoreStep(string name, Action step)
        {
            try
            {
                if (DebugFailStep != null && string.Equals(DebugFailStep, name, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException($"debug: tly_fixcheck failstep forced '{name}' to fail.");
                step();
            }
            catch (Exception ex)
            {
                _monitor.Log($"Reset: the '{name}' step failed; continuing the reset without it.\n{ex}", LogLevel.Error);
            }
        }

        /// <summary>Rename the main + "_old" save files inside a just-renamed save folder so their
        /// names match the new folder (Stardew names both folder and save file
        /// <c>&lt;FarmerName&gt;_&lt;uniqueID&gt;</c>). SaveGameInfo files are not id-named, so they're
        /// left alone. Best-effort: a failure here only re-opens the "save bricks on mid-window kill"
        /// gap, it never corrupts data.</summary>
        /// <summary>Delete the pre-reset save folder recorded in <see cref="PerformReset"/>. Called
        /// by the post-reset save (ResetSave) ONLY after the post-reset full save has written the new
        /// canonical folder — deleting after the confirmed save guarantees we never remove the
        /// player's only loadable copy (a kill before this point leaves the old folder intact).
        /// The reset always changes <c>uniqueIDForThisGame</c>, so the abandoned folder is always a
        /// different folder than the one just saved; a defensive id check skips it anyway.</summary>
        public void CleanupAbandonedSaveFolder()
        {
            ModSaveDataCarryover.DeleteOldCopies(_monitor, _carriedModData);
            _carriedModData = null;

            string old = _abandonedSaveFolder;
            _abandonedSaveFolder = null;
            if (string.IsNullOrEmpty(old) || !Directory.Exists(old))
                return;

            // Defensive: never delete a folder named for the CURRENT (new) uniqueID.
            if (Path.GetFileName(old).EndsWith(Game1.uniqueIDForThisGame.ToString(), StringComparison.Ordinal))
                return;

            try
            {
                Directory.Delete(old, recursive: true);
                _monitor.Log(
                    $"In-place reset: deleted the abandoned pre-reset save folder ({Path.GetFileName(old)}) — no duplicate left behind.",
                    LogLevel.Info);
            }
            catch (IOException ex)
            {
                _monitor.Log(
                    $"In-place reset: could not delete abandoned save folder '{Path.GetFileName(old)}' ({ex.Message}). " +
                    "It may show as a duplicate on the title screen; it is safe to delete manually.",
                    LogLevel.Warn);
            }
        }

        // Read in-run peaks from the live player so the baseline builder can apply
        // cap-not-grant. The tool peak is the highest tier owned ANYWHERE (bag, Clint's counter,
        // owned chests; ToolTierScan), the same read the shrine's keep offer uses, so a kept tier
        // bought for a tool sitting at Clint's or in a chest is not capped back to basic here.
        // Runs before FarmerReset cancels the Clint upgrade and before the world wipe empties the
        // chests. Skill levels are read directly.
        private static PlayerSnapshot CapturePeaks(Farmer p)
        {
            var toolTiers = new Dictionary<string, int>(TheLongestYear.Integration.ToolTierScan.Peaks(p));

            var skillLevels = new Dictionary<int, int>
            {
                [0] = p.farmingLevel.Value,
                [1] = p.fishingLevel.Value,
                [2] = p.foragingLevel.Value,
                [3] = p.miningLevel.Value,
                [4] = p.combatLevel.Value,
            };

            return new PlayerSnapshot { ToolTiers = toolTiers, SkillLevels = skillLevels };
        }
    }
}
