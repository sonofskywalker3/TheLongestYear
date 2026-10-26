using System.Collections.Generic;

namespace TheLongestYear.Core.Interactables
{
    /// <summary>Rules for the Bundle Log button on the inventory page: the column of side buttons right
    /// of the inventory grid (the Junimo note, then the organize button with its bag icon, the trash
    /// can lower down). The button sits right under the bag and opens the same page as the Bundle Log
    /// book, so a player can check the season's missing items without walking home. Game-free so the
    /// placement and controller rules can be tested.</summary>
    public static class BundleLogButtonRules
    {
        /// <summary>The button's component name.</summary>
        public const string ButtonName = "sonofskywalker3.TheLongestYear_BundleLogButton";

        /// <summary>Snap id of the button. The inventory page uses 0-35 for slots and 101-120 and
        /// 898 for its other components; this stays clear of them and of the game menu's tabs
        /// (12340-12349).</summary>
        public const int ButtonComponentId = 91_400;

        /// <summary>Side button width and height (16px sprite at 4x, like the organize button).</summary>
        public const int ButtonSize = 64;

        /// <summary>Space left between the bag and the button below it.</summary>
        public const int ButtonGap = 8;

        /// <summary>True when the button belongs on a freshly built inventory page: a run is active and
        /// the button is not already there.</summary>
        public static bool ShouldAdd(bool runActive, bool alreadyPresent) => runActive && !alreadyPresent;

        /// <summary>Top edge of the button: right under the bag (<paramref name="bagBottom"/>), or, when
        /// another button already holds that spot, under the lowest button in the way, so nothing
        /// already in the column moves. <paramref name="taken"/> lists the (top, bottom) of the
        /// column's other buttons.</summary>
        public static int ButtonY(int bagBottom, IReadOnlyList<(int Top, int Bottom)> taken)
        {
            int y = bagBottom + ButtonGap;
            if (taken == null) return y;
            bool moved = true;
            while (moved)
            {
                moved = false;
                foreach ((int top, int bottom) in taken)
                {
                    if (top < y + ButtonSize && bottom > y)
                    {
                        y = bottom + ButtonGap;
                        moved = true;
                    }
                }
            }
            return y;
        }

        /// <summary>Controller left neighbour: the inventory slot (from the grid's right-most column,
        /// as (id, centre y)) nearest the button's centre, or -1 when there is none.</summary>
        public static int NearestSlot(IReadOnlyList<(int Id, int CenterY)> rightColumn, int centerY)
        {
            int best = -1, bestDistance = int.MaxValue;
            if (rightColumn == null) return best;
            foreach ((int id, int y) in rightColumn)
            {
                int distance = System.Math.Abs(y - centerY);
                if (distance < bestDistance)
                {
                    best = id;
                    bestDistance = distance;
                }
            }
            return best;
        }
    }
}
