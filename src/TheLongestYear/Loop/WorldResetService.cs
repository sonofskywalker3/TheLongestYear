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

        public WorldResetService(IMonitor monitor) => _monitor = monitor;

        public void PerformReset(int startingMoney)
        {
            _monitor.Log("In-place reset: starting.", LogLevel.Info);

            // 1. The game's own new-game initializer rebuilds the world + regenerates CC bundles.
            Game1.game1.loadForNewGame(loadedGame: false);

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

            _monitor.Log(
                $"In-place reset: complete. {Game1.season} {Game1.dayOfMonth}, money {Game1.player.Money}.",
                LogLevel.Info);
        }
    }
}
