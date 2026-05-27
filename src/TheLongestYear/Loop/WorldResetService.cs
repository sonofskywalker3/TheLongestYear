using System.IO;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Locations;

namespace TheLongestYear.Loop
{
    /// <summary>
    /// Performs the in-place rewind to Spring 1. Reuses the game's own new-game initializer
    /// (Game1.loadForNewGame) for the heavy lifting — it clears Game1.locations, rebuilds the Farm and
    /// every location, regenerates the CC bundles (wiping donation progress), and re-adds NPCs — then
    /// applies targeted resets for the persistent Farmer and mine progress. "Fix the data, don't fight
    /// the system": no per-collection hacks.
    /// </summary>
    internal sealed class WorldResetService
    {
        private readonly IMonitor _monitor;
        private readonly TheLongestYear.Core.MetaState _meta;
        private readonly CommunityCenterUnlock _ccUnlock;
        private readonly string _modDirectory;

        public WorldResetService(IMonitor monitor, TheLongestYear.Core.MetaState meta, CommunityCenterUnlock ccUnlock,
            string modDirectory)
        {
            _monitor = monitor;
            _meta = meta;
            _ccUnlock = ccUnlock;
            _modDirectory = modDirectory;
        }

        public void PerformReset(int startingMoney)
        {
            // One-time safety backup before the first destructive reset (throws if it fails -> reset aborts).
            // Lands inside the mod folder (not in Stardew's Saves dir) so it doesn't appear as a second
            // save on the title screen.
            SaveBackup.BackupOnce(_meta, _monitor, _modDirectory);

            _monitor.Log("In-place reset: starting.", LogLevel.Info);

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
            string oldSavePath = Constants.CurrentSavePath;
            Game1.uniqueIDForThisGame = Utility.NewUniqueIdForThisGame();
            Game1.weatherForTomorrow = "Sun";
            string newSavePath = Constants.CurrentSavePath; // recomputed from the new ID
            if (!string.IsNullOrEmpty(oldSavePath) && !string.IsNullOrEmpty(newSavePath)
                && oldSavePath != newSavePath && Directory.Exists(oldSavePath) && !Directory.Exists(newSavePath))
            {
                try
                {
                    Directory.Move(oldSavePath, newSavePath);
                    _monitor.Log(
                        $"In-place reset: renamed save folder to match new uniqueID ({Path.GetFileName(newSavePath)}).",
                        LogLevel.Trace);
                }
                catch (IOException ex)
                {
                    _monitor.Log(
                        $"In-place reset: could NOT rename save folder ({ex.Message}). " +
                        "Next save will create a new folder; the previous one will appear as a duplicate on the title screen.",
                        LogLevel.Warn);
                }
            }
            _monitor.Log(
                $"In-place reset: new uniqueIDForThisGame={Game1.uniqueIDForThisGame}.",
                LogLevel.Trace);

            // 1. The game's own new-game initializer rebuilds the world + regenerates CC bundles.
            Game1.game1.loadForNewGame(loadedGame: false);

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

            // 2. Calendar -> Spring 1, year 1, morning. (loadForNewGame leaves dayOfMonth = 0 as a flag.)
            Game1.year = 1;
            Game1.season = Season.Spring;
            Game1.dayOfMonth = 1;
            Game1.timeOfDay = 600;
            Game1.netWorldState.Value.Date.Year = 1;
            Game1.netWorldState.Value.Date.Season = Season.Spring;
            Game1.netWorldState.Value.Date.DayOfMonth = 1;
            Game1.stats.DaysPlayed = 1;

            // 3. Farmer baseline (loadForNewGame leaves the existing player's stats intact).
            FarmerReset.ToBaseline(Game1.player, startingMoney);

            // 4. Mine progress.
            Game1.netWorldState.Value.LowestMineLevelForOrder = -1;
            MineShaft.clearActiveMines();

            // 5. Place the player home, awake, in the rebuilt FarmHouse.
            GameLocation home = Utility.getHomeOfFarmer(Game1.player);
            Game1.player.currentLocation = home;
            Game1.currentLocation = home;
            Game1.player.Position = new Vector2(9f, 9f) * 64f;
            home.resetForPlayerEntry();

            // Re-apply the CC unlock so the loop preserves day-1 CC access (loadForNewGame + FarmerReset wiped it).
            _ccUnlock.Apply();

            _monitor.Log(
                $"In-place reset: complete. {Game1.season} {Game1.dayOfMonth}, money {Game1.player.Money}.",
                LogLevel.Info);
        }
    }
}
