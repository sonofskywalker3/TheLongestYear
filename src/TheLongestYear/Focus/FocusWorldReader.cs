using System.Collections.Generic;
using StardewValley;
using StardewValley.Buildings;
using TheLongestYear.Core;
using TheLongestYear.Integration;

namespace TheLongestYear.Focus
{
    /// <summary>How far up each focus chain the live world already is, as a rung number (0 = none).
    /// Reads the game; the rung maths lives in <see cref="FocusWorldRungs"/>.</summary>
    internal static class FocusWorldReader
    {
        private const string ShortcutsMail = "communityUpgradeShortcuts";

        /// <summary>The live rung for one chain. Walks every owned chest for tools, so a caller
        /// quoting many chains should read <see cref="ToolTierScan.Peaks"/> once and use the overload.</summary>
        public static int Rung(string chainId)
            => Rung(chainId, IsToolChain(chainId) ? ToolTierScan.Peaks(Game1.player) : null);

        public static int Rung(string chainId, IReadOnlyDictionary<string, int> toolPeaks)
        {
            Farmer p = Game1.player;
            if (p == null || chainId == null) return 0;
            switch (chainId)
            {
                case ToolTierScan.HoeSlug:
                case ToolTierScan.PickaxeSlug:
                case ToolTierScan.AxeSlug:
                case ToolTierScan.WateringCanSlug:
                    return FocusWorldRungs.ToolRung(chainId, ToolTierPeaks.Level(toolPeaks, chainId));
                case ToolTierScan.RodSlug:
                    return FocusWorldRungs.ToolRung(chainId, ToolTierPeaks.Level(toolPeaks, chainId, ToolTierPeaks.NoRod));
                case "coop":
                case "barn":
                case "silo":
                case "fish_pond":
                    return FocusWorldRungs.BuildingRung(chainId, FinishedBuildingTypes());
                case "house":
                    return FocusWorldRungs.HouseRung(p.HouseUpgradeLevel, p.daysUntilHouseUpgrade.Value > 0);
                case "shortcuts":
                    return Game1.MasterPlayer?.mailReceived.Contains(ShortcutsMail) == true ? 1 : 0;
                case "backpack":
                    return FocusWorldRungs.BackpackRung(p.MaxItems);
                default:
                    return 0;
            }
        }

        public static bool IsToolChain(string chainId)
            => FocusChains.TryGet(chainId)?.Theme == FocusTheme.Tools;

        /// <summary>Each farm building's type once its work is done: the pending upgrade's name
        /// while Robin is upgrading it, else its current type.</summary>
        private static IEnumerable<string> FinishedBuildingTypes()
        {
            Farm farm = Game1.getFarm();
            if (farm == null) yield break;
            foreach (Building b in farm.buildings)
            {
                string pending = b.upgradeName.Value;
                yield return b.daysUntilUpgrade.Value > 0 && !string.IsNullOrEmpty(pending)
                    ? pending
                    : b.buildingType.Value;
            }
        }
    }
}
