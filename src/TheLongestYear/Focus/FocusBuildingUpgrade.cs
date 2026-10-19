using StardewValley;
using StardewValley.Buildings;
using TheLongestYear.Core;

namespace TheLongestYear.Focus
{
    /// <summary>Upgrades a coop or barn chain in place for a focus card: the farm's best building
    /// of the chain is raised to the card's rung at once, one vanilla upgrade at a time so each
    /// step's own changes (bigger interior, incubator, auto-feed) apply. Work Robin already has
    /// in hand on that building is finished first, since the card's rung sits above it.</summary>
    internal static class FocusBuildingUpgrade
    {
        private const int UpgradeTonight = 1;

        /// <summary>Raises the chain's best farm building to <paramref name="targetRung"/>. False when
        /// the farm has no building of the chain, the rung is unknown, or the building is
        /// already at or above it.</summary>
        public static bool Apply(string chainId, int targetRung)
        {
            if (FocusWorldRungs.BuildingTypeForRung(chainId, targetRung) == null) return false;
            Building building = BestOf(chainId, out int rung);
            if (building == null || rung >= targetRung) return false;

            if (building.isUnderConstruction(ignoreUpgrades: false))
                building.FinishConstruction();

            int current = RungOf(chainId, building.buildingType.Value);
            while (current < targetRung)
            {
                string next = FocusWorldRungs.BuildingTypeForRung(chainId, current + 1);
                if (next == null) return false;
                building.upgradeName.Value = next;
                building.daysUntilUpgrade.Value = UpgradeTonight;
                building.FinishConstruction();
                int after = RungOf(chainId, building.buildingType.Value);
                if (after <= current) return false;
                current = after;
            }
            return true;
        }

        /// <summary>The farm's building of this chain at the highest finished rung (a pending
        /// construction or upgrade counts at its finished type, like the world reader).</summary>
        private static Building BestOf(string chainId, out int bestRung)
        {
            bestRung = 0;
            Building best = null;
            Farm farm = Game1.getFarm();
            if (farm == null) return null;
            foreach (Building b in farm.buildings)
            {
                int r = RungOf(chainId, FinishedType(b));
                if (r > bestRung) { bestRung = r; best = b; }
            }
            return best;
        }

        private static string FinishedType(Building b)
        {
            string pending = b.upgradeName.Value;
            return b.daysUntilUpgrade.Value > 0 && !string.IsNullOrEmpty(pending) ? pending : b.buildingType.Value;
        }

        private static int RungOf(string chainId, string buildingType)
            => FocusWorldRungs.BuildingRung(chainId, new[] { buildingType });
    }
}
