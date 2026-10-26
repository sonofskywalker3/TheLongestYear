using System.Collections.Generic;

namespace TheLongestYear.Core.Interactables
{
    /// <summary>Rules for the Bundle Log tab in the game menu (the row of Inventory, Skills, Social,
    /// Map ... tabs). The tab opens the same page as the Bundle Log book, so a player can check the
    /// season's missing items without walking home. Game-free so the placement and trigger rules
    /// can be tested.</summary>
    public static class BundleLogTabRules
    {
        /// <summary>The tab's name in <c>GameMenu.tabs</c>; vanilla maps names to tab numbers.</summary>
        public const string TabName = "sonofskywalker3.TheLongestYear_BundleLogTab";

        /// <summary>Snap id of the tab. Vanilla tabs use 12340-12349; this stays clear of them and of
        /// the page ids menus usually take.</summary>
        public const int TabComponentId = 91_400;

        /// <summary>Vanilla tab width and height (16px sprite at 4x).</summary>
        public const int TabSize = 64;

        /// <summary>True when the tab belongs in a freshly built game menu: a run is active and
        /// the tab is not already there.</summary>
        public static bool ShouldAdd(bool runActive, bool alreadyPresent) => runActive && !alreadyPresent;

        /// <summary>Left edge of the new tab: one tab width right of the right-most existing tab,
        /// so it sits after the last tab whatever other mods added. <paramref name="fallbackX"/> is
        /// used when the row is empty.</summary>
        public static int NextTabX(IReadOnlyList<int> existingTabXs, int fallbackX)
        {
            if (existingTabXs == null || existingTabXs.Count == 0) return fallbackX;
            int max = existingTabXs[0];
            for (int i = 1; i < existingTabXs.Count; i++)
                if (existingTabXs[i] > max) max = existingTabXs[i];
            return max + TabSize;
        }

        /// <summary>Tab to switch to on a right trigger press, or null to leave it to vanilla.
        /// Vanilla stops at its own last tab (it only moves while the current tab is below its tab
        /// count), so the tab right after that last tab is reached here. Tabs inside vanilla's
        /// range are left to vanilla.</summary>
        public static int? RightTriggerTarget(int currentTab, int bundleLogTab, int vanillaTabCount)
        {
            if (bundleLogTab < 0) return null;
            if (currentTab < vanillaTabCount) return null;
            return currentTab == bundleLogTab - 1 ? bundleLogTab : (int?)null;
        }
    }
}
