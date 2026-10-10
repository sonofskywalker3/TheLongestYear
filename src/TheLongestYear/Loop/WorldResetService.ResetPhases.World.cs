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
    }
}
