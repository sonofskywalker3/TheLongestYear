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
    internal sealed partial class WorldResetService
    {
        /// <summary>The values one <see cref="PerformReset"/> call hands from phase to phase.</summary>
        private sealed class ResetContext
        {
            /// <summary>The bundle-count rule the outgoing board was built with.</summary>
            public TheLongestYear.Core.BundleCountRule PreviousBundleCount;
            public string BoardSource;
            public bool VanillaBoard;
            /// <summary>The live vanilla board when this reset holds it; null otherwise.</summary>
            public Dictionary<string, string> HeldVanillaBoard;
            public DisplayOptionsCarryover.Snapshot DisplayOptions;
            public FarmDecorSnapshot KeptDecor;
            public FarmhouseFurnitureCarryover.Snapshot KeptHouseFurniture;
            public RunBaseline Baseline;
        }

        /// <summary>Reset phase: resolve this loop's difficulty stamp and rebuild the availability model for its rarity step.</summary>
        private void ResolveDifficulty(ResetContext ctx)
        {
            // Difficulty modifiers (spec 2026-08-26): resolve the nine configured steps ONCE, here,
            // and stamp the result on the save. Everything downstream -- board generation this
            // reset, and the JP / price / cart / hold reads for the whole loop -- reads the stamp,
            // which is what makes a GMCM change take effect at the NEXT reset rather than
            // mid-season. Stamped before the board is built, because the board is built from it.
            // The bundle-count rule the outgoing board was built with: a held board keeps it (see
            // BundleCountStamp, applied once the hold is known below).
            ctx.PreviousBundleCount = _meta.Difficulty?.BundleCount;
            _meta.Difficulty = TheLongestYear.Core.DifficultyResolver.Resolve(_config.Difficulty, _config);
            if (!_meta.Difficulty.Steps.IsAllNormal())
                _monitor.Log(
                    "Reset: difficulty modifiers active -- " +
                    $"stacks {_meta.Difficulty.Steps.StackSize}, quality {_meta.Difficulty.Steps.QualityAsks}, " +
                    $"required slots {_meta.Difficulty.Steps.RequiredSlots}, rarity {_meta.Difficulty.Steps.ItemRarity}, " +
                    $"JP {_meta.Difficulty.Steps.JpEarned}, prices {_meta.Difficulty.Steps.ShrinePrices}, " +
                    $"gold {_meta.Difficulty.Steps.StartingGold} ({_meta.Difficulty.StartingGold}g), " +
                    $"cart {_meta.Difficulty.Steps.CartSlots} ({_meta.Difficulty.StartingCartSlots} slots), " +
                    $"holds {_meta.Difficulty.Steps.HoldPrices}, bundles per room {_meta.Difficulty.Steps.BundleCount}.",
                    LogLevel.Info);

            // The availability model's week mode is a function of the same step (item rarity is
            // the difficulty dial that governs how hard the board's items are), so a step change
            // at this reset must rebuild it before the board below is generated from it.
            if (RebuildAvailabilityModel != null)
            {
                TheLongestYear.Core.DifficultyStep step = _meta.Difficulty.Steps.ItemRarity;
                AvailabilityModel = RebuildAvailabilityModel(step);
                TheLongestYear.Core.WeekMode mode = TheLongestYear.Core.WeekModes.For(step);
                _monitor.Log($"Availability model rebuilt for {step} ({mode}).", LogLevel.Info);
            }
        }

        /// <summary>Reset phase: the save's chosen bundle source, stamped, and vanilla's board type set before loadForNewGame.</summary>
        private void ResolveBoardSource(ResetContext ctx)
        {
            // BundleSource (spec 2026-08-21): the save's chosen source takes effect HERE, at the reset. Stamp
            // the save with the mode this loop runs under, and in Vanilla mode hand vanilla the
            // player's Standard/Remixed choice BEFORE loadForNewGame — Game1.bundleType is a
            // non-persisted static (Nexus bug 1108030), so without this every reset wrote the
            // Standard set. Remixed re-rolls off the fresh uniqueIDForThisGame below.
            // The SAVE's own choice, never the config: the config is shared by every save, so a
            // newer TLY Custom game used to flip an older Normal save to custom bundles here
            // (victoriatauanem, Nexus 2026-09-28).
            ctx.BoardSource = TheLongestYear.Core.BundleSourceNames.ForSave(
                _meta.ChosenBundleSource, _meta.BundleSource, _meta.VanillaBundleType);
            _meta.ChosenBundleSource = ctx.BoardSource;
            ctx.VanillaBoard = TheLongestYear.Core.BundleSourceNames.IsVanilla(ctx.BoardSource);
            _meta.BundleSource = ctx.VanillaBoard
                ? TheLongestYear.Core.BundleSourceNames.LegacyVanilla : TheLongestYear.Core.BundleSourceNames.Engine;
            if (ctx.VanillaBoard)
            {
                // One setting, three choices (Jeff 2026-08-27): Normal and Remixed each name a
                // Game1.BundleType outright, so a player can move between the two on an existing save.
                string configuredType = TheLongestYear.Core.BundleSourceNames.VanillaTypeFor(ctx.BoardSource);
                if (configuredType != null)
                    _meta.VanillaBundleType = configuredType;

                bool remixed = string.Equals(_meta.VanillaBundleType, Game1.BundleType.Remixed.ToString(), StringComparison.OrdinalIgnoreCase);
                Game1.bundleType = remixed ? Game1.BundleType.Remixed : Game1.BundleType.Default;
                if (string.IsNullOrEmpty(_meta.VanillaBundleType))
                    _meta.VanillaBundleType = Game1.bundleType.ToString();
                _monitor.Log($"Reset: bundle source {ctx.BoardSource} — vanilla will generate a {Game1.bundleType} board.", LogLevel.Info);
            }
            else
            {
                Game1.bundleType = Game1.BundleType.Default;
            }
        }

        /// <summary>Reset phase: snapshot a held vanilla board before loadForNewGame wipes it.</summary>
        private void SnapshotHeldVanillaBoard(ResetContext ctx)
        {
            // Keep-bundles hold on a VANILLA board (Jeff 2026-08-27: all three sources can hold).
            // A vanilla reset regenerates through loadForNewGame off a freshly re-seeded
            // uniqueIDForThisGame, so there is no seed to pin the way the engine path pins
            // BundleSeedLoop. Snapshot the live board HERE, before loadForNewGame wipes it, and
            // write it back afterwards: that reproduces the held board exactly, including any
            // difficulty adjustments already baked into it.
            //
            // Read the flag before BundleHold.ConsumeChoiceAtReset clears it further down. No new
            // saved field is needed: the board is in the save until this reset replaces it, so a
            // quit between the Fail-night choice and the reset still snapshots the right board.
            if (ctx.VanillaBoard && _meta.HoldChoiceMadeForReset && _meta.ConsecutiveHolds > 0)
            {
                var live = Game1.netWorldState.Value.BundleData;
                if (live != null && live.Count > 0)
                {
                    ctx.HeldVanillaBoard = new Dictionary<string, string>(live);
                    _monitor.Log(
                        $"Reset: holding the vanilla board ({ctx.HeldVanillaBoard.Count} bundles snapshotted; " +
                        $"consecutive holds {_meta.ConsecutiveHolds}).",
                        LogLevel.Info);
                }
                else
                {
                    _monitor.Log(
                        "Reset: asked to hold the vanilla board but there was no bundle data to snapshot; " +
                        "it will regenerate instead.",
                        LogLevel.Warn);
                }
            }
        }

        /// <summary>Reset phase (step 0): a fresh world seed, and the pre-reset save folder recorded for cleanup.</summary>
        private void ReseedWorld()
        {
            // 0. Fresh world seed BEFORE loadForNewGame. Game1.uniqueIDForThisGame is the master
            // seed used by Utility.CreateDaySaveRandom and per-location forage-spawn randoms.
            // loadForNewGame does NOT touch it; without this, day-1 forage placement is
            // identical across runs (user playtest 2026-05-27: "always a dandelion in the same
            // place on day 1"). Reset weatherForTomorrow so the previous run's evening doesn't
            // bleed into Spring 1.
            //
            // SIDE-EFFECT: Stardew's save folder name is "<FarmerName>_<uniqueIDForThisGame>",
            // so changing the ID would create a new folder on next save (orphaning the existing
            // one). After changing the ID we rename the on-disk folder to match the new path so
            // the save stays a single folder.
            //
            // First attempt at this used Constants.CurrentSavePath both before AND after the ID
            // change, but SMAPI caches CurrentSavePath at SaveLoaded time — it does NOT recompute
            // when uniqueIDForThisGame changes mid-session. That made oldSavePath == newSavePath,
            // so the rename condition was false and we silently produced an orphan folder
            // (user playtest 2026-05-27: "I've still got 2 saves"). Compute the new folder name
            // ourselves from the old path: keep everything before the last underscore (the player
            // name component), append the new uniqueID.
            string oldSavePath = Constants.CurrentSavePath;
            Game1.uniqueIDForThisGame = Utility.NewUniqueIdForThisGame();
            Game1.weatherForTomorrow = "Sun";

            // Record the folder we're abandoning so it can be deleted AFTER the post-reset full save
            // writes the new canonical folder (RunController.ForceFullSave → CleanupAbandonedSaveFolder).
            // We deliberately DON'T rename it here: Stardew's SaveGame.Save writes to the CANONICAL
            // "<farmName>_<uniqueID>" folder, but the on-disk folder may carry a non-canonical prefix
            // (e.g. "None2_" from an earlier de-dup). Renaming by the old prefix produced a SECOND
            // folder that disagreed with what the save actually wrote — two "None" farms on the title
            // screen (2026-06-03 playtest). Leaving the old folder intact until the new save is
            // confirmed also means a kill mid-reset degrades to a loadable stale folder, not a brick.
            _abandonedSaveFolder = (!string.IsNullOrEmpty(oldSavePath) && Directory.Exists(oldSavePath))
                ? oldSavePath
                : null;

            _monitor.Log(
                $"In-place reset: new uniqueIDForThisGame={Game1.uniqueIDForThisGame}.",
                LogLevel.Trace);
        }

        /// <summary>Reset phase (steps 0a to 0h): capture everything the rebuild would wipe that a keep upgrade carries over.</summary>
        private void CaptureCarryovers(ResetContext ctx)
        {
            // 0a. Capture the player's pet (kind, breed, name, friendship) BEFORE loadForNewGame
            // wipes it. Gated on the keep_pet upgrade — owners get a sentimental pet-survives-
            // resets carryover; non-owners skip the snapshot and the pet is wiped normally.
            PetCarryoverService.SnapshotPet(_meta, _monitor);

            // 0b. Capture the player's stable tile + horse (name/hat) BEFORE loadForNewGame wipes the
            // buildings. Gated on early_horse ("Keep Horse"); restored after the rebuild at the same
            // tile so the stable persists where the player built it.
            HorseCarryoverService.SnapshotHorse(_meta, _monitor);

            // 0c. Capture where each kept-building family (coop/barn/silo) currently stands, so the
            // step-8 rebuild puts the building back exactly where the player had it (2026-07-13 user
            // ruling — same contract as the stable above). Unconditional: cheap, and keeping the spot
            // fresh even before the keep is purchased means the first keep-owning reset already knows it.
            SnapshotKeptBuildingSpots();
            // Keep Fish Pond remembers one pond (most fish, first on a tie), under its own key.
            FishPondCarryoverService.SnapshotSpot(_meta);

            // 0d. Bank the LIVE stash chest before loadForNewGame wipes the Farm. StashItems is
            // otherwise refreshed only on the Saving event, so anything deposited after the last
            // save (all of day 28) was restored from a stale snapshot at step 13 (Nexus bug 1111046).
            _stashService?.BankToMeta();

            // 0e. Capture the player's Zoom Level + UI Scale BEFORE loadForNewGame swaps in a
            // fresh Options instance. Those two dials are the only ones vanilla's
            // LoadDefaultOptions refuses to carry (they're marked [DontLoadDefaultSetting] as
            // per-save settings), so they -- and only they -- snapped back to default on every
            // loop (Nexus posts, RiseiJaku 2026-09-09). See DisplayOptionsCarryover.
            ctx.DisplayOptions = DisplayOptionsCarryover.Capture();

            // 0f. Refresh the Herd Book from the live farm BEFORE loadForNewGame wipes the animals, so
            // each registered animal comes back with this loop's hearts (spec 2026-09-25). An entry
            // whose animal is gone keeps its last snapshot.
            HerdBookService.RefreshBeforeReset(_meta, _monitor);

            // 0g. Keep Farm Decor: lift paths, fences, lights, signs and decorations off the
            // farm before loadForNewGame discards it; they go back at step 13a. Held in memory like
            // the display options above: the reset is one call.
            ctx.KeptDecor = _meta.HasUpgrade(FarmDecorKeep.UpgradeId)
                ? FarmDecorSnapshot.Capture(Game1.getFarm(), _monitor)
                : null;

            // 0h. Keep Farmhouse Furniture: lift the house's and cellar's furniture (non-cosmetic
            // contents wiped) before loadForNewGame builds a new house; it goes back at step 14b.
            if (_meta.HasUpgrade(FarmhouseFurnitureKeep.UpgradeId))
            {
                try
                {
                    ctx.KeptHouseFurniture = FarmhouseFurnitureCarryover.Capture(_monitor);
                }
                catch (Exception ex)
                {
                    _monitor.Log($"Reset: Keep Farmhouse Furniture capture failed; continuing the reset.\n{ex}", LogLevel.Error);
                }
            }
        }
    }
}
