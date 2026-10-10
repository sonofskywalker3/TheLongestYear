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

        /// <summary>Reset phase (steps 1a, 1a-mail): wipe the Community Center's bundles, rooms and completion mail.</summary>
        private void ResetCommunityCenterState()
        {
            // 1a. Defensive bundle / area-completion wipe. CommunityCenter.bundles is a PROPERTY
            // pointing at Game1.netWorldState.Value.Bundles (CommunityCenter.cs:104) — a NetCollection
            // at world-state level, not on the CC instance. The 2026-05-26 round-2 playtest showed
            // donations surviving tly_reset even after loadForNewGame (user: "the reset didn't
            // remove the foraging items I had donated, even though it switched which items were
            // required for it"). Re-clearing here regardless of whether loadForNewGame already
            // touched it — costs nothing and guarantees a clean slate.
            CommunityCenter cc = Game1.getLocationFromName("CommunityCenter") as CommunityCenter;
            if (cc != null)
            {
                // Force-repopulate Bundles + BundleRewards FIRST. Necessary because (a) a prior
                // broken reset may have Clear()ed the dict (2026-05-26 round-3 crash) leaving
                // bundlesDict()[0] missing, and (b) NetWorldState.BundleData's lazy SetBundleData
                // only fires when netBundleData is empty — once it's populated, missing Bundles
                // entries aren't restored automatically. SetBundleData is idempotent for
                // existing keys (only ADDS missing), so calling it here is safe regardless of
                // current state.
                Game1.netWorldState.Value.SetBundleData(Game1.netWorldState.Value.BundleData);

                // Now zero each per-slot completion array WITHOUT clearing the keys — vanilla
                // does bundles[bundleIndex] lookups (CC.cs:585), KeyNotFoundException there is
                // what crashed JunimoNoteMenu.setUpMenu in round 3.
                // FieldDict exposes the underlying Dictionary<int, NetArray<bool, NetBool>> so we
                // can mutate the NetArray entries in-place (the .Pairs projection only gives a
                // bool[] snapshot, mutating that wouldn't sync).
                foreach (var kvp in Game1.netWorldState.Value.Bundles.FieldDict)
                {
                    var arr = kvp.Value;
                    for (int i = 0; i < arr.Length; i++)
                        arr[i] = false;
                }
                foreach (var kvp in Game1.netWorldState.Value.BundleRewards.FieldDict)
                    kvp.Value.Value = false;
                for (int i = 0; i < cc.areasComplete.Count; i++)
                    cc.areasComplete[i] = false;
                // Mail flags that vanilla sets on room completion + post-completion world
                // changes. These gate (a) hasCompletedCommunityCenter() + the "you completed
                // the Community Center" achievement, (b) the JojaMart-closed visual + door
                // (abandonedJojaMartAccessible — verified via Town.cs:580 +
                // WorldChangeEvent.cs:283, the lightning strike adds this mail for tomorrow),
                // (c) the Joja-path branch (JojaMember + jojaMember + the per-room ccXxx
                // Joja-path flags). User feedback 2026-05-26 round 2: "Joja is still closed
                // day 1" after reset confirmed these were sticking — the prior reset only
                // cleared in-memory bundle state.
                string[] mailToClear =
                {
                    "ccBoilerRoom", "ccCraftsRoom", "ccPantry", "ccFishTank",
                    "ccVault", "ccBulletin", "ccIsComplete",
                    "abandonedJojaMartAccessible", "ccMovieTheater",
                    "JojaMember", "jojaMember",
                    "ccBoilerRoomJoja", "ccCraftsRoomJoja", "ccPantryJoja",
                    "ccFishTankJoja", "ccVaultJoja", "ccBulletinJoja",
                };
                foreach (string flag in mailToClear)
                    Game1.MasterPlayer.mailReceived.Remove(flag);
                Game1.MasterPlayer.mailForTomorrow.Remove("abandonedJojaMartAccessible");
                _monitor.Log(
                    $"In-place reset: cleared CC bundles + areasComplete + {mailToClear.Length} completion/joja mail flags.",
                    LogLevel.Trace);
            }

            // 1a-mail. Purge any PENDING CC room-restoration mail from mailForTomorrow. The block
            // above clears mailReceived (the "already restored" record), but a ccVault/ccGreenhouse/…
            // queued the day the reset fires sits in mailForTomorrow, which the rewind never touched —
            // so vanilla's next-morning pickFarmEvent would play the Junimos-fix-the-bus WorldChangeEvent
            // on the fresh, 0-bundle run (user 2026-06-08: "bus is fixed and it's a carryover"). The
            // day-end fail path also strips this, but purging here guarantees a clean world regardless
            // of how the reset was reached (debug tly_failreset, a legacy save that already baked in the
            // stuck mail, etc.). Done on Game1.player (= MasterPlayer in single-player, where the CC
            // mail lives).
            var purged = CcRestorationMail.PurgeFromMailForTomorrow(Game1.player);
            if (purged.Count > 0)
                _monitor.Log(
                    $"In-place reset: purged {purged.Count} stale CC restoration mail from mailForTomorrow " +
                    $"([{string.Join(", ", purged)}]) so no phantom room-fix scene fires on the fresh run.",
                    LogLevel.Info);
        }

        /// <summary>Reset phase (steps 1b to 1d): the museum, lost books and the rest of NetWorldState.</summary>
        private void ResetWorldStateCollections()
        {
            // 1b. Museum wipe. LibraryMuseum.museumPieces is a PROPERTY over
            // Game1.netWorldState.Value.MuseumPieces (LibraryMuseum.cs:50) — world-state level,
            // the same survival class as the CC bundles above: loadForNewGame rebuilds the
            // location but not the netWorldState dictionary, so donations persisted across loops
            // (Dusklight7 2026-07-05). FarmerReset wipes the museumCollectedRewardO_* mail, so
            // persisting donations re-armed the entire reward ladder every loop (free early
            // scarecrows/starfruit) — and long-tenured players could exhaust donatable items and
            // lock themselves out of rewards. Clearing rewinds the museum to empty; rewards are
            // re-earned by re-donating (user-approved 2026-07-09).
            int museumPieces = Game1.netWorldState.Value.MuseumPieces.Length;
            if (museumPieces > 0)
            {
                Game1.netWorldState.Value.MuseumPieces.Clear();
                _monitor.Log(
                    $"In-place reset: cleared {museumPieces} museum donation(s) — the museum rewinds with the year.",
                    LogLevel.Info);
            }

            // 1c. Lost books — same netWorldState survival class as the museum pieces above.
            // LostBooksFound is the found COUNT (the per-book "lb_<n>" read markers live in
            // mailReceived, which FarmerReset already clears wholesale). Reset the count so the
            // library shelf rewinds with the museum (user ruling 2026-07-10: full reset for
            // consistency; books scatter again each loop).
            int lostBooks = Game1.netWorldState.Value.LostBooksFound;
            if (lostBooks > 0 && _meta.HasUpgrade(TheLongestYear.Core.LostBookKeep.UpgradeId))
                _monitor.Log($"In-place reset: Keep Lost Books owned; {lostBooks} lost book(s) stay found.", LogLevel.Info);
            else if (lostBooks > 0)
            {
                Game1.netWorldState.Value.LostBooksFound = 0;
                _monitor.Log(
                    $"In-place reset: cleared {lostBooks} lost book(s) found — the library shelf rewinds too.",
                    LogLevel.Info);
            }

            // 1d. Everything else on NetWorldState — one-time full field audit (user request
            // 2026-07-10; spec docs/superpowers/specs/2026-07-13-networldstate-audit-design.md).
            // Bundles, museum, and lost books were caught one report at a time from this same
            // survival class (loadForNewGame never rebuilds netWorldState); this closes the rest
            // of the class in one pass instead of reactively.
            ResetNetWorldStateLeftovers();
        }

        /// <summary>Reset phase (steps 2 to 2e): calendar, weather, NetWorldState sync, quest of the day, special orders.</summary>
        private void RewindCalendar()
        {
            // 2. Calendar -> Spring 1, year 1, morning. (loadForNewGame leaves dayOfMonth = 0 as a flag.)
            Game1.year = 1;
            Game1.season = StardewValley.Season.Spring;
            Game1.dayOfMonth = 1;
            Game1.timeOfDay = 600;
            Game1.stats.DaysPlayed = 1;

            // NOTE: the three `netWorldState.Value.Date.X = ...` lines that used to sit here were
            // NO-OPS and were removed (audit 2026-08-26). NetWorldState.Date is a computed property
            // (`public WorldDate Date => WorldDate.Now();`, NetWorldState.cs:229) that builds a NEW
            // WorldDate from the Game1 statics on every call, so assigning to it wrote to a throwaway
            // object and never reached netWorldState's own year/season/dayOfMonth/timeOfDay fields.
            // Those are synced from Game1 by vanilla's UpdateFromGame1 — called explicitly after the
            // weather rebuild in step 2c below, so nothing can read (or WriteToGame1 back) the
            // pre-reset date in the window before the post-reset save.

            // The load-menu date label is read from the Farmer's *ForSaveGame display fields
            // (LoadGameMenu uses dayOfMonthForSaveGame/seasonForSaveGame/yearForSaveGame), which
            // vanilla refreshes during the normal sleep-save (Game1 sets player.*ForSaveGame). A
            // direct post-reset SaveGame.Save doesn't run that step, so without this the slot kept
            // the pre-reset date ("Day 5 of Spring" on a Spring-1 save — 2026-06-03 playtest). Set
            // them to match the rewound date so the title screen shows Spring 1.
            if (Game1.player != null)
            {
                Game1.player.dayOfMonthForSaveGame = Game1.dayOfMonth;
                Game1.player.seasonForSaveGame = (int)Game1.season;
                Game1.player.yearForSaveGame = Game1.year;
            }

            // 2b. Weather. The reset rewinds the calendar but nothing above re-resolves the DAY's
            //     weather, so the pre-reset day's state — Game1.isRaining/isLightning/isSnowing,
            //     the HUD icon, and every netWorldState LocationWeather — rides into Spring 1 and
            //     gets SAVED there (2026-07-13 playtest: reset during a Summer thunderstorm left
            //     Spring 1 with Weather=Storm serialized, lightning flashes and a storm icon on a
            //     clear day). Run vanilla's own day-start chain for the rewound date:
            //     UpdateWeatherForNewDay resolves today via getWeatherModificationsForDate (which
            //     WeatherModificationsPatch routes to the new run's schedule — uniqueID was
            //     re-seeded in step 0), ApplyWeatherForNewDay copies the result into the live
            //     flags (and resets the day-1 monthly counters), updateWeatherIcon redraws the HUD.
            Game1.UpdateWeatherForNewDay();
            Game1.ApplyWeatherForNewDay();
            Game1.updateWeatherIcon();

            // 2c. Push the rewound Game1 statics into netWorldState's own copies (audit 2026-08-26).
            //     UpdateFromGame1 (NetWorldState.cs:804) is vanilla's one-way Game1 -> netWorldState
            //     sync for year / season / dayOfMonth / timeOfDay / daysPlayed / uniqueIDForThisGame /
            //     whichFarm / the Default LocationWeather. Until it runs, netWorldState still holds
            //     the PRE-reset values, and WriteToGame1 is the reverse sync: in single-player
            //     (multiplayerMode == 0, so Game1.IsServer is false) its `if (!Game1.IsServer)` block
            //     copies all of those back OVER Game1 — including the old uniqueIDForThisGame that
            //     step 0 deliberately re-seeded for the new run's weather/forage RNG. Vanilla calls
            //     WriteToGame1 whenever a farmEvent finishes (Game1.cs:4982), so the stale window was
            //     real rather than theoretical. Sync here, right after the calendar and weather are
            //     final, so both sides agree before anything can read either one.
            Game1.netWorldState.Value.UpdateFromGame1();

            // 2d. Quest of the day. loadForNewGame calls RefreshQuestOfTheDay (Game1.cs:4229) while
            //     the calendar is still the PRE-reset one, so the board quest was rolled from the old
            //     run's Stats.DaysPlayed (Utility.getQuestOfTheDay seeds on DaysPlayed * 777) and,
            //     for the SlayMonsterQuest branch, gated on the old run's MineShaft.lowestLevelReached
            //     — which step 6 has not yet pinned back to the kept elevator floor. Nothing re-rolled
            //     it afterwards, so Spring 1 of every loop opened with a stale quest, and its gold
            //     reward, that a genuine day 1 cannot offer: getQuestOfTheDay returns null outright
            //     while DaysPlayed <= 1. Re-run vanilla's refresh now that DaysPlayed is back to 1.
            Game1.RefreshQuestOfTheDay();

            // 2e. Special orders. They live on player.team, which loadForNewGame never rebuilds, so a
            //     town order, the board's offer and the completed list all rode into the next loop.
            //     Drop town orders, clear the board so it re-rolls, forget completed town orders.
            //     Qi's orders are left alone. Every player, with or without Keep Special Orders Board.
            RestoreStep("special orders", () => SpecialOrderReset.Apply(_monitor));
        }

        /// <summary>Reset phase (steps 3 to 7c): peaks, the reset baseline, the farmer, professions, mines, shortcuts, cellar.</summary>
        private void ResetFarmer(ResetContext ctx)
        {
            // 3. Capture the in-run peaks from the live player BEFORE the wipe — the cap
            //    side of cap-not-grant. The Farmer-side wipe happens inside
            //    _farmerReset.Apply, so peak-reading has to land here.
            PlayerSnapshot peaks = CapturePeaks(Game1.player);

            // 4. Build the reset baseline + apply Farmer-side state (gold, items, tool
            //    tiers, skill levels, kitchen flag).
            // Starting gold comes from the STAMP resolved at the top of this reset, not straight
            // from config: the step scales config.StartingMoney, so a hand-tuned baseline is
            // still honoured and a GMCM change only lands on the next loop.
            ctx.Baseline = RunBaselineBuilder.Build(_meta, _run, peaks, _meta.Difficulty.StartingGold);
            // Keep Lost Books: FarmerReset clears all mail, so lift the books' read markers first.
            IReadOnlyList<string> keptBookMail = _meta.HasUpgrade(TheLongestYear.Core.LostBookKeep.UpgradeId)
                ? TheLongestYear.Core.LostBookKeep.MailToKeep(Game1.player.mailReceived)
                : Array.Empty<string>();
            _farmerReset.Apply(Game1.player, ctx.Baseline,
                _meta.CookbookRecipes,
                _meta.CraftbookRecipes,
                _meta.SeenEventsEver,
                CatchLimitedFishIds);
            foreach (string flag in keptBookMail)
                Game1.player.mailReceived.Add(flag);

            // 5. Profession picker re-trigger queue. Enqueued here; the actual menus
            //    surface on the next DayStarted (RunController drains after reset).
            foreach (int skill in ctx.Baseline.ProfessionPickerSkillsToRequeue)
                _professionPicker.Enqueue(skill, ctx.Baseline.SkillLevels[skill]);

            // 6. Mine progress. Pin the elevator to the kept floor (cap-not-grant — the player
            //    keeps ONLY the floor they bought a keep_mine_elevator_N upgrade for; 0 = none).
            //    The elevator panel reads MineShaft.lowestLevelReached, whose getter FALLS BACK to
            //    NetWorldState.LowestMineLevel when LowestMineLevelForOrder < 0 (MineShaft.cs:181-189,
            //    Android decompile). So clearing only LowestMineLevelForOrder leaves LowestMineLevel
            //    (and Farmer.deepestMineLevel) at the in-run depth, and the elevator still offers
            //    every floor reached this run — khauser13: "the mine elevator did not lock on reset,
            //    I could still get down to floor sixty and I didn't buy the elevator unlocks."
            //    Pin all THREE fields to the kept floor exactly (no Math.Max — that would leak the
            //    in-run peak back in).
            MineShaft.clearActiveMines();
            MineShaft.yearUpdate();
            int keptFloor = ctx.Baseline.MineElevatorFloor;
            Game1.netWorldState.Value.LowestMineLevel = keptFloor;
            Game1.netWorldState.Value.LowestMineLevelForOrder = keptFloor > 0 ? keptFloor : -1;
            Game1.player.deepestMineLevel = keptFloor;

            // 7. (Kept bus: see step 11b, RestoreKeptBus. It has to run AFTER the board write in
            //    step 11, which re-zeroes every bundle slot and areasComplete.)

            // 7b. Robin's community-upgrade map shortcuts — single mail flag controls all five
            //     (Town fence, bus tunnel, forest stump bridge, Mountain quarry path, Mountain
            //     side route). Vanilla reads this flag in Forest.cs:421, Mountain.cs:177,
            //     Town.cs:589, Beach.cs:468, BeachNightMarket.cs:235 and on GameLocation map
            //     overrides — adding the flag here is sufficient; the per-location code re-applies
            //     map overrides on first entry of each location this run.
            if (ctx.Baseline.ShortcutsUnlocked)
                Game1.MasterPlayer.mailReceived.Add("communityUpgradeShortcuts");

            // 7c. Cellar location for kept_basement. loadForNewGame doesn't include the Cellar
            //     location unless the player previously had an L3 house at save creation. With
            //     HouseUpgradeLevel = 3 forced by FarmerReset, the FarmHouse warp tile will try
            //     to teleport into a non-existent "Cellar" location. Create it on demand here
            //     before resetForPlayerEntry runs the warp setup. updateCellarAssignments
            //     binds the cellar to the master player.
            if (ctx.Baseline.BasementOnDay1 && Game1.getLocationFromName("Cellar") == null)
            {
                Game1.locations.Add(new StardewValley.Locations.Cellar("Maps\\Cellar", "Cellar"));
                Game1.updateCellarAssignments();
            }
        }

        /// <summary>Reset phase (steps 8 to 10c): kept buildings, greenhouse, horse, fish pond, animals and pet.</summary>
        private void RestoreKeptFarm(ResetContext ctx)
        {
            // 8. Pre-build kept buildings on the Farm. Coords are deterministic — we always
            //    use the same tiles so subsequent runs land buildings in the same spots.
            RestoreStep("kept buildings", () => ApplyKeptBuildings(ctx.Baseline.KeptBuildings));
            // 8a. Keep Greenhouse: back to where the player had moved it (Gifts of the Junimos).
            RestoreStep("greenhouse spot", RestoreGreenhouseSpot);

            // 9. Keep Horse — restore the player's stable + horse at its saved tile (pure carry-over;
            //    no auto-build, so a player who hasn't built a stable yet has no horse this loop).
            //    Gated on the upgrade + a prior snapshot inside the service.
            RestoreStep("horse", () => HorseCarryoverService.RestoreHorse(_meta, _monitor));

            // 9a. Keep Fish Pond: one EMPTY pond back at the player's spot. Runs after every other
            //     building is back, so the pond is the one that moves if its spot is now taken.
            RestoreStep("fish pond", () => FishPondCarryoverService.Restore(_meta, _monitor));

            // 10. Herd Book animals move in first (Jeff, 2026-09-25, option C): the Herd Book's
            //     room check counts only its own slots, so its animals get each building's room
            //     before the Start-with animals. An entry with no building or no room waits in the
            //     book.
            RestoreStep("Herd Book animals", () => HerdBookService.Restore(_meta, _monitor));

            // 10-start. Start-with animals fill whatever room the Herd Book left; one that no
            //     longer fits is skipped and logged.
            RestoreStep("starting animals", () => ApplyStartingAnimals(ctx.Baseline.StartingAnimals));

            // 10a. Restore the snapshotted pet on the Farm (keep_pet upgrade). Runs after
            // starting animals so the Farm.characters collection is already settled. No-op
            // when the upgrade isn't owned or no prior snapshot exists. Also sets the
            // MarniePetAdoption mail flag so vanilla's day-1 adoption offer is suppressed.
            RestoreStep("pet", () => PetCarryoverService.RestorePet(_meta, _monitor));
            // 10b. If the rewind left the farm petless (no Keep Pet, or the upgrade was never
            //      bought), re-open vanilla adoption route at Marnie counter: the rewind
            //      otherwise shuts every door to a new pet. See EnableAdoptionIfPetless.
            RestoreStep("pet adoption", () => PetCarryoverService.EnableAdoptionIfPetless(_monitor));
            // 10c. A petless farm gets Marnie's pet visit back too, not just the paid Adopt option.
            RestoreStep("pet visit", () =>
            {
                int reopened = TheLongestYear.Core.PetCarryover.ReopenArrivalScenes(
                    Game1.player.eventsSeen, farmHasPet: Utility.getAllPets().Any());
                if (reopened > 0)
                    _monitor.Log($"PetCarryover: no pet after the rewind; Marnie's pet visit can play again ({reopened} scene ids cleared).", LogLevel.Info);
            });
        }

        /// <summary>Reset phase (step 11a): the new loop's board, engine-written or vanilla, held or new.</summary>
        private void WriteBoard(ResetContext ctx)
        {
            // 11a. Regenerate the owned-bundle set for the new loop (Task 6: engine wiring) and
            // expose its requirement manifest via LastGeneratedRequirements so FinalizeReset can
            // re-inject it (RunController.ReplaceRequirements) before the hub samples goal slots.
            //
            // Seed basis: NOT Game1.uniqueIDForThisGame. Decompile-verified
            // (Utility.NewUniqueIdForThisGame, StardewValley/Utility.cs): it's
            // `(ulong)(DateTime.UtcNow - epoch).TotalSeconds` -- wall-clock, not deterministic --
            // and step 0 above just reseeded it to a fresh value for THIS loop. Two problems rule
            // it out entirely, not just the obvious one:
            //   (a) using the value captured HERE (post-reseed) would make a replayed reset
            //       reroll: ForceFullSave (called by FinalizeReset right after this method
            //       returns) explicitly documents a skip-and-defer-to-next-sleep path when an
            //       event/minigame is active, so "generate+write happened but the save hasn't
            //       landed on disk yet" is a real, reachable window, not just a theoretical
            //       process-kill race. A reload from that window followed by re-triggering the
            //       reset would draw a NEW wall-clock reseed and a DIFFERENT bundle set --
            //       exactly the reroll the engine exists to prevent.
            //   (b) using a PRE-reset capture (the id from BEFORE step 0's reseed) fixes (a) but
            //       breaks the far more common case: this id is only ever this loop's OLD id --
            //       every ordinary subsequent reload of the NEW save (the whole rest of this
            //       loop's playtime) sees the POST-reseed id instead, so ModEntry's SaveLoaded
            //       engine-mode branch could never reproduce this exact generation and would
            //       permanently fall back to the legacy read path for the entire loop.
            // Game1.player.UniqueMultiplayerID has neither problem: it's assigned once at farmer
            // creation, is part of the Farmer's own persisted save data, and loadForNewGame
            // above only resets the EXISTING persistent Farmer's stats/inventory -- it never
            // reassigns this id (decompile-verified, Game1.loadForNewGame). So it is IDENTICAL
            // across a replayed reset (satisfies the anti-scum guarantee) AND identical on every
            // later reload of this loop's save (satisfies SaveLoaded's manifest-first re-derivation) --
            // the one value that is simultaneously stable across both.
            // A reset that skipped the Fail-night hold choice (console tly_reset, post-win new loop)
            // must behave like a reshuffle; BundleHold.ConsumeChoiceAtReset owns that rule.
            TheLongestYear.Core.BundleHold.ConsumeChoiceAtReset(_meta);

            // Randomizer "Random bundle rewards" is board-level: stamp it only when this reset builds
            // a NEW board. A held board (vanilla snapshot restored, or the Engine re-deriving off the
            // pinned seed loop; ConsecutiveHolds > 0 only after a Kept choice) keeps the stamp it was
            // built under, so toggling the option never changes a board the player paid to keep.
            bool holdingBoard = ctx.VanillaBoard ? ctx.HeldVanillaBoard != null : _meta.ConsecutiveHolds > 0;
            if (!holdingBoard)
                _meta.RandomBundleRewardsBoard = _config.Randomizer?.RandomBundleRewards ?? false;
            // Bundle-count dial (Jeff, 2026-10-09): a held board keeps its count.
            TheLongestYear.Core.BundleCountStamp.ForReset(_meta.Difficulty, ctx.PreviousBundleCount, holdingBoard);

            if (ctx.VanillaBoard)
            {
                // Vanilla mode: the board loadForNewGame just wrote IS the board. No engine write,
                // no manifest marker; the post-reset reload classifies it (read-and-classify).
                _meta.BundlesGeneratedForReset = -1;
                _meta.WrittenBoard = null;
                _meta.WrittenBoardSeasonPins = null;
                _meta.WrittenBoardFlavors = null;
                LastGeneratedRequirements = null;
                // Normal or Remixed with Tech's Cross-Mod Bundles: roll a fresh Tech board in place
                // of the old one, before the difficulty and reward passes below run over it (spec
                // 2026-10-08 addendum 2). Skipped for a held board and TLY Custom; a failure logs
                // one warning and keeps the game's own board. The post-reset
                // reload classifies and fingerprints whatever is live, so this board is the
                // expected one for the loop.
                TheLongestYear.Core.TechBundlesReroll.Run(
                    TechBundles, ctx.BoardSource, restoringHeldBoard: ctx.HeldVanillaBoard != null,
                    message => _monitor.Log(message, LogLevel.Info),
                    message => _monitor.Log(message, LogLevel.Warn));
                if (ctx.HeldVanillaBoard != null)
                {
                    // The held board already carries whatever difficulty adjustments it was built
                    // with, so the ask-side pass must NOT run over it again and compound them.
                    Game1.netWorldState.Value.SetBundleData(new Dictionary<string, string>(ctx.HeldVanillaBoard));
                    _monitor.Log(
                        $"Reset: restored the held vanilla board ({ctx.HeldVanillaBoard.Count} bundles); no re-roll, no difficulty re-pass.",
                        LogLevel.Info);
                }
                else
                {
                    _monitor.Log("Reset: vanilla board — keeping the game's own board (no engine write).", LogLevel.Info);
                    ApplyVanillaBoardDifficulty();
                    ApplyVanillaBoardRewardShuffle();
                }
                // With Tech's Cross-Mod Bundles loaded, store the board this reset just wrote: Tech's
                // mod writes its own saved board over it on every load, and the load puts this one
                // back (spec 2026-10-08 addendum 3). Without Tech it stays null, as before.
                _meta.WrittenBoard = TheLongestYear.Core.TechBoardOfRecord.VanillaBoardToStore(
                    TechBundles?.IsLoaded ?? false, Game1.netWorldState.Value.BundleData);
                if (_meta.WrittenBoard != null)
                    _monitor.Log(
                        $"Reset: stored this loop's board ({_meta.WrittenBoard.Count} bundles) as the board of record, since Tech's Cross-Mod Bundles rewrites the board on every load.",
                        LogLevel.Info);
            }
            else
            {
                // Stack size and quality asks arrive as a SCALED TUNING BLOCK rather than as
                // engine changes: BundleSlotFiller already reads every stack number and quality
                // chance off this object, so scaling it applies both modifiers with no edit to
                // generation. Scale returns the same instance at Normal.
                var difficultyTuning = TheLongestYear.Core.DifficultyTuning.Scale(_config.PoolTuning, _meta.Difficulty);
                var engine = new BundleEngine(_monitor, difficultyTuning, _config.EnableNonObjectDonations, _config.RarityThresholds,
                    TheLongestYear.Core.YearTwoCrops.ExcludedFor(_meta.HasUpgrade, _meta.Difficulty.Steps.ItemRarity), _meta.Difficulty);
                engine.Availability = AvailabilityModel;
                // "Allow mod items in custom bundles" (spec 2026-10-08 addendum 1): a new board takes
                // the save's choice, a held board keeps the value it was built under. Stamped with the
                // board so the load-time check re-derives it the same way after a mid-loop toggle.
                _meta.BoardAllowsModItems = TheLongestYear.Core.CustomBoardModItems.ForReset(
                    holdingBoard, _meta.BoardAllowsModItems, _meta.AllowModItemsInCustomBundles);
                engine.AllowModItems = _meta.BoardAllowsModItems.Value;
                // Keep-bundles hold (spec 2026-08-24): the seed loop is EffectiveBundleSeedLoop, which
                // RunController's Fail-night choice already pinned (hold) or advanced to this loop
                // (reshuffle) before we got here. Legacy saves resolve to CompletedResets.
                int seed = BundleEngineSeed.For(unchecked((ulong)Game1.player.UniqueMultiplayerID), _meta.EffectiveBundleSeedLoop);
                GeneratedBundleSet generatedSet = engine.Generate(seed, _meta.RandomBundleRewardsBoard);
                engine.WriteToWorld(generatedSet, _monitor);
                // Persist exactly what was written (and the derived pins it was classified under)
                // so later loads verify the live board against this instead of re-deriving from
                // the seed; see MetaState.WrittenBoard.
                _meta.WrittenBoard = new Dictionary<string, string>(generatedSet.ToBundleData());
                _meta.WrittenBoardSeasonPins = TheLongestYear.Core.BoardRequirements.PinsToStored(engine.LastDerivedSeasonPins);
                // The fruit/mushroom/fish each flavored slot names. Stamped with the board it
                // belongs to, so a board written before 0.18.33 keeps a null map and no flavors.
                _meta.WrittenBoardFlavors = new Dictionary<string, string>(generatedSet.Flavors);
                _monitor.Log(
                    $"Reset: bundle seed loop {_meta.EffectiveBundleSeedLoop} (CompletedResets {_meta.CompletedResets}, consecutive holds {_meta.ConsecutiveHolds}).",
                    LogLevel.Info);
                _meta.BundlesGeneratedForReset = _meta.CompletedResets;
                LastGeneratedRequirements = engine.BuildRequirements(
                    generatedSet, _itemSeasonPins, _bundleQuotas, AvailabilityModel);
            }
        }

        /// <summary>Reset phase (steps 11b, 12): Gifts of the Junimos and the intro quests.</summary>
        private void RestoreGiftsAndIntros(ResetContext ctx)
        {
            // 11b. Gifts of the Junimos (kept bus, greenhouse, quarry bridge, boulder, minecarts):
            //      the room's world reward stands from day 1; the bundles stay on the board and are
            //      paid like any other (Jeff, 2026-08-29).
            if (ctx.Baseline.KeptGiftMails.Count > 0)
                RestoreStep("Gifts of the Junimos", () => RestoreKeptGifts(ctx.Baseline));

            // 12. Fire cookbook/craftbook quest intros on the first run after purchase.
            RestoreStep("book quest intros", FireBookQuestIntros);
        }

        /// <summary>Reset phase (steps 13 to 14c): stash, shrine, decor, the farmhouse and its furniture, with ground drops remembered.</summary>
        private void RestoreFarmAndHouse(ResetContext ctx)
        {
            // 13-drops. From here to step 14b, overflow can drop on the ground; remember it (below).
            GroundDrop.BeginResetCapture();

            // 13. Place the Junimo Stash chest on the Farm and populate from MetaState.
            RestoreStep("Junimo Stash", () =>
            {
                _stashService?.PlaceChest();
                _stashService?.PopulateFromMeta();
            });
            RestoreStep("planning shrine", () => _planningShrine?.Place(_stashService?.LastPlacedTile));

            // 13a. Keep Farm Decor back on its tiles, after kept buildings, the stash chest and the
            // planning shrine, so each of them wins its tile and displaced decor can go to the stash.
            // Restore guards each piece; this catch only keeps a bug in it from stopping the reset.
            try
            {
                FarmDecorCarryoverService.Restore(ctx.KeptDecor, ctx.Baseline.ToolTiers, _stashService, _monitor);
            }
            catch (Exception ex)
            {
                _monitor.Log($"Reset: Keep Farm Decor restore failed; continuing the reset.\n{ex}", LogLevel.Error);
            }

            // 14. Place the player home, awake, in the rebuilt FarmHouse. resetForPlayerEntry
            //     also rebuilds the FarmHouse layout to match HouseUpgradeLevel — picking up
            //     the kitchen if the baseline set it.
            GameLocation home = Utility.getHomeOfFarmer(Game1.player);
            Game1.player.currentLocation = home;
            Game1.currentLocation = home;
            Game1.player.Position = new Vector2(9f, 9f) * 64f;
            home.resetForPlayerEntry();

            // 14a. Rebuild the FarmHouse's built-in furniture. After the loop's house downgrade the
            //      cabin came back without its starter set (2026-06-01: fireplace MISSING, and earlier
            //      a stale bed blocked the door). Clear the furniture and re-run vanilla's own
            //      AddStarterFurniture so the FULL default set (bed, fireplace, rug, table+bowl, …) is
            //      laid down at the correct positions for the current HouseUpgradeLevel.
            RestoreFarmHouseFurniture(home);

            // 14b. Keep Farmhouse Furniture: the kept set replaces the starter set just laid down, on
            //      the house and cellar this loop has (kitchen and cellar exist since step 14).
            try
            {
                FarmhouseFurnitureCarryover.Restore(ctx.KeptHouseFurniture, home, _monitor);
            }
            catch (Exception ex)
            {
                _monitor.Log($"Reset: Keep Farmhouse Furniture restore failed; continuing the reset.\n{ex}", LogLevel.Error);
            }

            // 14c. Ground debris is never saved, and the forced save follows right after the reset,
            //      so a quit before picking these up lost them. Remember them until the first night.
            List<PendingGroundDrop> resetDrops = GroundDrop.EndResetCapture();
            ResetGroundDrops.Remember(_meta, resetDrops);
            if (resetDrops.Count > 0)
                _monitor.Log($"Reset: {resetDrops.Count} item(s) dropped on the ground; remembered until the first night in case the game is quit before they are picked up.", LogLevel.Info);
        }

        /// <summary>Reset phase: undo vanilla's one-way map edits and re-apply the CC, mountain and book unlocks.</summary>
        private void ReapplyWorldUnlocks()
        {
            // Undo vanilla's one-way map edits. Fixing the beach bridge (Beach.fixBridge) and
            // Robin's community shortcuts (showCommunityUpgradeShortcuts / ApplyMapOverride) edit
            // the loaded xTile Map in place, and that Map is the content manager's CACHED asset.
            // loadForNewGame builds a fresh Beach with bridgeFixed = false, but its loadMap pulls
            // the same cached, already-edited Map: the bridge tiles are intact and walkable while
            // the flag says broken, so the "?" marker floats over a bridge you can't interact with
            // and can't re-break (Nexus bug 1124076, Bumblewyn: it "unfixed itself" after a full
            // restart, which is exactly when the content cache is dropped). Shortcut maps leak the
            // same way for a player who unlocked them last loop and doesn't keep them. Drop the
            // cached maps and reload each location from clean data, AFTER the mail flags for this
            // loop are settled (step 7b) so MakeMapModifications re-applies only what is kept.
            RefreshMutatedVanillaMaps();

            // Re-apply the CC unlock so the loop preserves day-1 CC access (loadForNewGame + FarmerReset wiped it).
            RestoreStep("Community Center unlock", () => _ccUnlock.Apply());

            // Re-clear the Mountain landslide. loadForNewGame rebuilt every location, so the
            // Mountain ctor saw DaysPlayed = 1 and re-initialised landslide.Value = true.
            RestoreStep("mountain landslide", () => _mountainUnlock?.Apply());

            // Books are inventory items wiped by FarmerReset; re-grant exactly one of each.
            RestoreStep("books", () => _bookFurniture?.ReconcileInventory());
        }
    }
}
