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
