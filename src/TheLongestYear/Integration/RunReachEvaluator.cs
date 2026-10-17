using System;
using System.Collections.Generic;
using System.Linq;
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

        /// <summary>Optional debug sink (wired by ModEntry) for the #3 keep-tool-upgrades
        /// investigation: dumps what the live tool reach actually sees when a shrine builds.</summary>
        public static System.Action<string> DebugLog;

        /// <summary>Log every tool the player owns (bag, Clint's counter, owned chests) with its
        /// UpgradeLevel and where it sits, plus what <see cref="ToolLevel"/> resolves per kind.
        /// Diagnoses a missing "Keep &lt;tool&gt;" row. Call when the shrine's keep list is built.</summary>
        public static void LogToolSnapshot()
        {
            if (DebugLog == null) return;
            Farmer p = Game1.player;
            if (p == null) { DebugLog("[reach-snapshot] no player loaded."); return; }

            var sb = new System.Text.StringBuilder("[reach-snapshot] tools owned: ");
            foreach ((string slug, Tool tool, string source) in ToolTierScan.OwnedTools(p))
                sb.Append($"{slug}(L{tool.UpgradeLevel}@{source}) ");
            sb.Append($"| ToolLevel resolves: hoe={ToolLevel(p, "hoe")} pickaxe={ToolLevel(p, "pickaxe")} ")
              .Append($"axe={ToolLevel(p, "axe")} watering_can={ToolLevel(p, "watering_can")} rod={RodLevel(p)}");
            DebugLog(sb.ToString());
        }

        // One world walk per menu build, not one per keep row: the peaks are cached for the tick
        // they were read on (a shrine evaluates every tool keep in the same tick).
        private static IReadOnlyDictionary<string, int> _peaks;
        private static uint _peaksTick = uint.MaxValue;
        private static Farmer _peaksFarmer;

        private static IReadOnlyDictionary<string, int> Peaks(Farmer p)
        {
            uint tick = (uint)Game1.ticks;
            if (_peaks == null || _peaksTick != tick || !ReferenceEquals(_peaksFarmer, p))
            {
                _peaks = ToolTierScan.Peaks(p);
                _peaksTick = tick;
                _peaksFarmer = p;
            }
            return _peaks;
        }

        /// <summary>The Vault bundles TLY recorded as paid this run (debug readout of the bus reach).</summary>
        internal static System.Collections.Generic.List<int> PaidThisRun()
            => _runState?.Invoke()?.VaultBundlesPaid?.ToList() ?? new System.Collections.Generic.List<int>();

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
                "book"     => p.stats.Get(r.Key) != 0 ? 1 : 0,   // vanilla Book_* flag, set by Object.readBook
                "mail"     => HasMail(p, r.Key) ? 1 : 0,            // wallet items / Stardrop source markers
                "room"     => RoomComplete(r.Key) ? 1 : 0,          // Gifts of the Junimos: the CC room's bundles all done
                "event"    => r.Key != null && p.eventsSeen.Contains(r.Key) ? 1 : 0, // Data/Powers SEEN_EVENT grants
                "stardrop_mines" => (HasMail(p, "CF_Mines")
                                     || p.chestConsumedMineLevels.GetValueOrDefault(100, false)) ? 1 : 0,
                "scythe"   => p.mailReceived.Contains("gotGoldenScythe") ? 1 : 0,
                "building" => HasBuildingAtLeast(r.Key) ? 1 : 0,
                "house"    => p.HouseUpgradeLevel,
                "pet"      => p.hasPet() ? 1 : 0,
                "shortcuts" => Game1.MasterPlayer.mailReceived.Contains("communityUpgradeShortcuts") ? 1 : 0,
                // Keep Bus Unlocked (Jeff, 2026-10-09): every Vault bundle on this board paid this
                // run, or the Vault room repaired, whichever comes first.
                VaultRules.BusReachMetric => VaultRules.IsBusRepaired(
                    _runState?.Invoke()?.VaultBundlesPaid ?? new System.Collections.Generic.List<int>(),
                    VaultBundleMap.Indices().ToArray(), VaultRoomRepaired()) ? 1 : 0,
                // The run reached Fall 2, the day vanilla opens the board. Read straight from
                // DaysPlayed, NOT the (patched) IsSpecialOrdersBoardUnlocked. DaysPlayed only drops
                // back to 1 inside PerformReset, which runs after the perk screen closes, so the
                // Fail-morning shrine still sees this run's count (no run-state record needed).
                "special_orders" => SpecialOrdersBoardKeep.ReachValue(Game1.stats.DaysPlayed),
                _          => -1,   // unknown metric fails closed
            };
            return actual >= 0 && r.IsMet(actual);
        }

        /// <summary>Data/Bundles room name for a `room:` key (keys carry no spaces).</summary>
        private static string RoomName(string key) => key switch
        {
            "Pantry" => "Pantry",
            "CraftsRoom" => "Crafts Room",
            "FishTank" => "Fish Tank",
            "BoilerRoom" => "Boiler Room",
            "Vault" => "Vault",
            "BulletinBoard" => "Bulletin Board",
            _ => key ?? "",
        };

        /// <summary>True when every bundle of the room is complete on the live board. Read from
        /// the board rather than the completion mail so a room finished on the Fail day counts
        /// (its letter is still in mailForTomorrow when the perk screen opens).</summary>
        public static bool RoomComplete(string key)
        {
            if (Game1.getLocationFromName("CommunityCenter") is not StardewValley.Locations.CommunityCenter cc) return false;
            var data = Game1.netWorldState?.Value?.BundleData;
            var slots = Game1.netWorldState?.Value?.Bundles?.FieldDict;
            if (data == null || slots == null) return false;
            string prefix = RoomName(key) + "/";
            var slotsByIndex = cc.bundlesDict();
            bool any = false;
            foreach (string bundleKey in data.Keys)
            {
                if (!bundleKey.StartsWith(prefix, System.StringComparison.Ordinal)) continue;
                if (!int.TryParse(bundleKey.Substring(prefix.Length), out int index)) continue;
                if (!slots.ContainsKey(index) || !slotsByIndex.TryGetValue(index, out bool[] bundleSlots)) return false;
                any = true;
                // A paid Vault bundle sets only slot 0, so isBundleComplete never sees it (VaultRules).
                if (!VaultRules.IsRoomBundleDone(RoomName(key), cc.isBundleComplete(index), bundleSlots)) return false;
            }
            return any;
        }

        private const string VaultRepairedMail = "ccVault";

        /// <summary>The Vault room counts as repaired once vanilla marks the area complete or sends
        /// its completion mail (the bus repair).</summary>
        internal static bool VaultRoomRepaired()
        {
            if (Game1.getLocationFromName("CommunityCenter") is StardewValley.Locations.CommunityCenter cc
                && cc.areasComplete.Count > StardewValley.Locations.CommunityCenter.AREA_Vault
                && cc.areasComplete[StardewValley.Locations.CommunityCenter.AREA_Vault])
                return true;
            return Game1.MasterPlayer?.mailReceived.Contains(VaultRepairedMail) == true;
        }

        /// <summary>Some wallet getters read Game1.MasterPlayer (HasRustyKey, HasSkullKey, the Dwarvish
        /// guide); single-player that is the same Farmer, so check both sides to be safe.</summary>
        private static bool HasMail(Farmer p, string flag) =>
            flag != null && (p.mailReceived.Contains(flag)
                || (Game1.MasterPlayer != null && Game1.MasterPlayer.mailReceived.Contains(flag)));

        /// <summary>The HIGHEST upgrade level of that tool kind the player owns anywhere (bag,
        /// Clint's counter, owned chests). A spare basic tool can no longer hide the upgraded one,
        /// and a tool handed to Clint or parked in a chest still counts (Nexus keep-tool reports).</summary>
        private static int ToolLevel(Farmer p, string kind)
            => ToolTierPeaks.Level(Peaks(p), kind);

        /// <summary>0 bamboo, 2 fiberglass, 3 iridium (1 = training rod); -1 when no rod is owned,
        /// distinct from bamboo.</summary>
        private static int RodLevel(Farmer p)
            => ToolTierPeaks.Level(Peaks(p), ToolTierScan.RodSlug, ToolTierPeaks.NoRod);

        private static int BackpackTier(Farmer p) => p.MaxItems switch
        {
            >= 36 => 2,
            >= 24 => 1,
            _     => 0,
        };

        /// <summary>EARNED level: the live level minus levels bought with Crash Course this loop
        /// (spec 2026-08-29, 1.5: a bought level is never keepable, so no keep row may open on it).
        /// Skill indices follow vanilla: Farming 0, Fishing 1, Foraging 2, Mining 3, Combat 4.</summary>
        private static int SkillLevel(Farmer p, string name)
        {
            int index = name switch
            {
                "farming" => 0, "fishing" => 1, "foraging" => 2, "mining" => 3, "combat" => 4, _ => -1,
            };
            int current = name switch
            {
                "farming"  => p.farmingLevel.Value,
                "mining"   => p.miningLevel.Value,
                "foraging" => p.foragingLevel.Value,
                "fishing"  => p.fishingLevel.Value,
                "combat"   => p.combatLevel.Value,
                _          => 0,
            };
            int bought = 0;
            var boughtByskill = _runState?.Invoke()?.SkillLevelsBoughtThisLoop;
            if (index >= 0 && boughtByskill != null)
                boughtByskill.TryGetValue(index, out bought);
            return current - bought;
        }

        // Housing chains — a higher tier satisfies the reach for a lower one (a Deluxe Coop
        // counts as having reached "Coop"/"Big Coop"). Upgrading a building changes its
        // buildingType in place, so the farm holds one entry at the current tier.
        private static readonly string[] CoopChain = { "Coop", "Big Coop", "Deluxe Coop" };
        private static readonly string[] BarnChain = { "Barn", "Big Barn", "Deluxe Barn" };

        /// <summary>True if the farm has a building of <paramref name="type"/> or a higher tier in
        /// the same chain. Types outside the coop/barn chains (e.g. Silo) are treated as a
        /// one-element chain, so they match by exact buildingType — one loop covers both cases.</summary>
        private static bool HasBuildingAtLeast(string type)
        {
            Farm farm = Game1.getFarm();
            if (farm == null) return false;

            string[] chain = Array.IndexOf(CoopChain, type) >= 0 ? CoopChain
                           : Array.IndexOf(BarnChain, type) >= 0 ? BarnChain
                           : new[] { type };
            int need = Array.IndexOf(chain, type);

            foreach (StardewValley.Buildings.Building b in farm.buildings)
            {
                int have = Array.IndexOf(chain, b.buildingType.Value);
                if (have >= need) return true;
            }
            return false;
        }
    }
}
