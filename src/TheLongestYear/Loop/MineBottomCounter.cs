using StardewValley;

namespace TheLongestYear.Loop
{
    /// <summary>
    /// <c>Farmer.timesReachedMineBottom</c> is run state. Vanilla counts it up each time the player
    /// arrives on floor 120 (MineShaft.cs ~3382), and once it is 1 or more every mine monster is
    /// built tougher: up to double its health, +50% resilience, double miss chance and more damage
    /// (Monster.parseMonsterInfo, ~444), plus richer gem nodes and container loot on shallow floors,
    /// a bonus diamond/prismatic drop chance and event preconditions that need the bottom. The rewind pinned the mine depth back but left this counter, so after one loop that
    /// reached the bottom every later loop's mines stayed at the post-bottom difficulty
    /// (StevanieA, 2026-11-22). Vanilla can never have the bottom counted with a deepest floor
    /// under 120; both the rewind and a save load restore that.
    /// </summary>
    internal static class MineBottomCounter
    {
        /// <summary>The mines' bottom floor; vanilla counts the bottom reached on arrival here.</summary>
        internal const int BottomFloor = 120;

        /// <summary>Rewind: the new loop has not reached the bottom. Returns the old count.
        /// Arriving on floor 120 again (an elevator kept to 120 included) counts it up as vanilla does.</summary>
        internal static int ClearForNewLoop(Farmer farmer)
        {
            int was = farmer.timesReachedMineBottom;
            farmer.timesReachedMineBottom = 0;
            return was;
        }

        /// <summary>Save load: a counted bottom with a deepest floor under 120 is the leak from an
        /// earlier loop (saves rewound before this fix). Returns the cleared count, 0 if nothing changed.</summary>
        internal static int RepairOnLoad(Farmer farmer)
        {
            if (farmer.timesReachedMineBottom == 0 || farmer.deepestMineLevel >= BottomFloor)
                return 0;
            return ClearForNewLoop(farmer);
        }
    }
}
