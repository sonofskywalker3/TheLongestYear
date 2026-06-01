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
                    return rod.UpgradeLevel;   // 1 bamboo, 2 fiberglass, 3 iridium
            return 0;
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
    }
}
