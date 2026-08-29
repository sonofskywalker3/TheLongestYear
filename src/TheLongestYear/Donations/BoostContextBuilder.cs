using System.Collections.Generic;
using StardewValley;
using TheLongestYear.Core;

namespace TheLongestYear.Donations
{
    /// <summary>Fills a BoostContext from live game state: the one place Core's boost rules touch Game1.</summary>
    internal static class BoostContextBuilder
    {
        public static BoostContext Build(RunState run, int skill = -1)
        {
            int day = Calendar.DayOfYear((int)run.Season, run.DayOfMonth);
            Farmer p = Game1.player;
            IReadOnlyList<int> levels = p == null
                ? new[] { 0, 0, 0, 0, 0 }
                : new[] { p.farmingLevel.Value, p.fishingLevel.Value, p.foragingLevel.Value, p.miningLevel.Value, p.combatLevel.Value };
            int floor = Game1.netWorldState?.Value?.LowestMineLevel ?? 0;
            return new BoostContext(day, TomorrowIsFestival(), levels, floor, skill);
        }

        private static bool TomorrowIsFestival()
        {
            if (Game1.Date == null) return false;
            WorldDate tomorrow = new WorldDate(Game1.Date);
            tomorrow.TotalDays += 1;
            return Utility.isFestivalDay(tomorrow.DayOfMonth, tomorrow.Season);
        }
    }
}
