using System.Collections.Generic;
using StardewValley;
using StardewValley.Locations;
using TheLongestYear.Core;

namespace TheLongestYear.Donations
{
    /// <summary>Fills a BoostContext from live game state: the one place Core's boost rules touch Game1.</summary>
    internal static class BoostContextBuilder
    {
        private const string DefaultLocationContext = "Default";

        public static BoostContext Build(RunState run, int skill = -1)
        {
            // The game's own date: correct on every morning, including cutscene mornings where the
            // run's calendar is synced later in the DayStarted chain.
            int day = Calendar.DayOfYear((int)Game1.season, Game1.dayOfMonth);
            Farmer p = Game1.player;
            IReadOnlyList<int> levels = p == null
                ? new[] { 0, 0, 0, 0, 0 }
                : new[] { p.farmingLevel.Value, p.fishingLevel.Value, p.foragingLevel.Value, p.miningLevel.Value, p.combatLevel.Value };
            // The elevator's real deepest stop: vanilla stops updating LowestMineLevel once
            // LowestMineLevelForOrder is set (the reset pins it), so read through the same getter
            // the elevator menu uses.
            int floor = Game1.netWorldState?.Value == null ? 0 : MineShaft.lowestLevelReached;
            return new BoostContext(day, TomorrowIsFestival(), levels, floor, skill);
        }

        /// <summary>Festival tomorrow, active or passive with a map replacement (Trout Derby,
        /// SquidFest): vanilla forces Sun on those mornings, so a weather buy would be wasted.</summary>
        private static bool TomorrowIsFestival()
        {
            if (Game1.Date == null) return false;
            WorldDate tomorrow = new WorldDate(Game1.Date);
            tomorrow.TotalDays += 1;
            if (Utility.isFestivalDay(tomorrow.DayOfMonth, tomorrow.Season)) return true;
            return Utility.IsPassiveFestivalDay(tomorrow.DayOfMonth, tomorrow.Season, DefaultLocationContext);
        }
    }
}
