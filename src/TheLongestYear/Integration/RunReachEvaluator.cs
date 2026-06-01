using System;
using StardewValley;
using StardewValley.Menus;
using StardewValley.Tools;
using TheLongestYear.Core;

namespace TheLongestYear.Integration
{
    /// <summary>Evaluates a keep's <see cref="UpgradeDefinition.RunReachRequirement"/> against the
    /// player's LIVE in-run state (read at the moment a shrine menu is built). Reads Game1, so it
    /// lives in the glue layer; the parse + threshold compare are in Core (RunReachRequirement).</summary>
    internal static class RunReachEvaluator
    {
        // The "bus" reach is mod-tracked per-run state (vault bundles paid this run), not a
        // simple Game1 read, so ModEntry wires a live RunState accessor.
        private static System.Func<RunState> _runState;
        public static void AttachRunState(System.Func<RunState> runState) => _runState = runState;

        /// <summary>Null/empty requirement ⇒ always met (non-reach upgrades). Unknown metric ⇒ false.</summary>
        public static bool Meets(string requirement)
        {
            RunReachRequirement r = RunReachRequirement.Parse(requirement);
            if (r == null)
                return string.IsNullOrWhiteSpace(requirement);   // no requirement = met; malformed = not
            Farmer p = Game1.player;
            if (p == null) return false;

            int actual = r.Metric switch
            {
                "tool"     => ToolLevel(p, r.Key),
                "rod"      => RodLevel(p),
                "backpack" => BackpackTier(p),
                "skill"    => SkillLevel(p, r.Key),
                "mine"     => p.deepestMineLevel,
                "mastery"  => MasteryTrackerMenu.getCurrentMasteryLevel(),
                "scythe"   => p.mailReceived.Contains("gotGoldenScythe") ? 1 : 0,
                "building" => HasBuildingAtLeast(r.Key) ? 1 : 0,
                "house"    => p.HouseUpgradeLevel,
                "pet"      => p.hasPet() ? 1 : 0,
                "shortcuts" => Game1.MasterPlayer.mailReceived.Contains("communityUpgradeShortcuts") ? 1 : 0,
                "bus"      => (_runState?.Invoke()?.VaultBundlesPaid.Count ?? 0) > 0 ? 1 : 0,
                _          => -1,   // unknown metric fails closed
            };
            return actual >= 0 && r.IsMet(actual);
        }

        private static int ToolLevel(Farmer p, string kind)
        {
            foreach (Item it in p.Items)
            {
                switch (kind)
                {
                    case "hoe"          when it is Hoe h:          return h.UpgradeLevel;
                    case "pickaxe"      when it is Pickaxe pk:     return pk.UpgradeLevel;
                    case "axe"          when it is Axe a:          return a.UpgradeLevel;
                    case "watering_can" when it is WateringCan w:  return w.UpgradeLevel;
                }
            }
            return 0;
        }

        private static int RodLevel(Farmer p)
        {
            foreach (Item it in p.Items)
                if (it is FishingRod rod)
                    return rod.UpgradeLevel;   // 0 bamboo, 2 fiberglass, 3 iridium (1 = training rod)
            return -1;                          // no rod held — distinct from bamboo (UpgradeLevel 0)
        }

        private static int BackpackTier(Farmer p) => p.MaxItems switch
        {
            >= 36 => 2,
            >= 24 => 1,
            _     => 0,
        };

        private static int SkillLevel(Farmer p, string name) => name switch
        {
            "farming"  => p.farmingLevel.Value,
            "mining"   => p.miningLevel.Value,
            "foraging" => p.foragingLevel.Value,
            "fishing"  => p.fishingLevel.Value,
            "combat"   => p.combatLevel.Value,
            _          => 0,
        };

        // Housing chains — a higher tier satisfies the reach for a lower one (a Deluxe Coop
        // counts as having reached "Coop"/"Big Coop"). Upgrading a building changes its
        // buildingType in place, so the farm holds one entry at the current tier.
        private static readonly string[] CoopChain = { "Coop", "Big Coop", "Deluxe Coop" };
        private static readonly string[] BarnChain = { "Barn", "Big Barn", "Deluxe Barn" };

        /// <summary>True if the farm has a building of <paramref name="type"/> or a higher tier in
        /// the same chain.</summary>
        private static bool HasBuildingAtLeast(string type)
        {
            string[] chain = Array.IndexOf(CoopChain, type) >= 0 ? CoopChain
                           : Array.IndexOf(BarnChain, type) >= 0 ? BarnChain
                           : null;
            if (chain == null) return false;
            int need = Array.IndexOf(chain, type);

            Farm farm = Game1.getFarm();
            if (farm == null) return false;
            foreach (StardewValley.Buildings.Building b in farm.buildings)
            {
                int have = Array.IndexOf(chain, b.buildingType.Value);
                if (have >= need) return true;
            }
            return false;
        }
    }
}
