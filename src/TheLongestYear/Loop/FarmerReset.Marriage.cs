using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Characters;
using StardewValley.Locations;
using TheLongestYear.Core;

namespace TheLongestYear.Loop
{
    /// <summary>
    /// A marriage or roommate arrangement ends at the rewind (user ruling 2026-10-19). The reset used
    /// to wipe friendshipData (hearts, and with it the Married status) but never Farmer.spouse, and
    /// vanilla's NPC.isMarried/getSpouse read only Farmer.spouse. So the old spouse still counted as
    /// married: NPC.OnDayStarted ran marriageDuties and moved them into the farmhouse on the new
    /// loop, the house kept the spouse room, and the player was "married" at zero hearts (player
    /// report: married Sebastian, roommate Krobus, PolySweet installed; Sebastian in the house on
    /// spring 2 of loop 2). This clears everything vanilla's own Farmer.doDivorce clears, plus the
    /// engagement and mod-data side, and sends every former partner back to their own home.
    /// Every step logs and swallows its own failure: the marriage clear must never abort the reset.
    /// </summary>
    internal sealed partial class FarmerReset
    {
        /// <summary>Known mod-side caches that remember spouses outside the save, keyed by farmer
        /// id. Read by name only (no assembly reference); a missing type or field is skipped.
        /// PolySweet (ApryllForever.PolyamorySweetLove) rebuilds these from Farmer.spouse and
        /// friendshipData only on DayStarted, and in between its Farmer.spouse getter postfix
        /// hands back the first cached spouse and writes it into Farmer.spouse again.</summary>
        private static readonly (string Assembly, string Type, string Field)[] ModSpouseCaches =
        {
            ("PolyamorySweetLove", "PolyamorySweetLove.ModEntry", "currentSpouses"),
            ("PolyamorySweetLove", "PolyamorySweetLove.ModEntry", "currentUnofficialSpouses"),
        };

        /// <summary>Static single-NPC fields in the same mod (temporary official spouse, patio
        /// spouse) that would point at a former spouse until the next DayStarted.</summary>
        private static readonly (string Assembly, string Type, string Field)[] ModSpouseSlots =
        {
            ("PolyamorySweetLove", "PolyamorySweetLove.ModEntry", "tempOfficialSpouse"),
            ("PolyamorySweetLove", "PolyamorySweetLove.NPCPatches", "PatioSpouse"),
        };

        /// <summary>Farmer-side marriage state, run before friendshipData is cleared so the
        /// married and engaged entries can still be read. Returns every former partner.</summary>
        private IReadOnlyList<string> ClearMarriageState(Farmer p)
        {
            IReadOnlyList<string> partners = Array.Empty<string>();
            try
            {
                partners = MarriageResetRules.FormerPartners(
                    p.spouse,
                    p.friendshipData.Pairs.Select(kv => (kv.Key, IsPartnerStatus(kv.Value))));

                // Vanilla doDivorce: spouse null, divorce flag off. Engagement and wedding countdown
                // live on the friendship entry, which the caller clears next.
                p.spouse = null;
                p.divorceTonight.Value = false;
                List<string> farmerKeys = RemoveMarriageModData(p.modData);
                int cachesCleared = ClearModSpouseCaches(p);

                if (partners.Count > 0 || cachesCleared > 0 || farmerKeys.Count > 0)
                    _monitor.Log(
                        $"Reset: marriage ended with the rewind. Former partner(s): [{string.Join(", ", partners)}]; " +
                        $"cleared {cachesCleared} mod spouse cache(s), farmer keys [{string.Join(", ", farmerKeys)}].",
                        LogLevel.Info);
            }
            catch (Exception ex)
            {
                _monitor.Log($"Reset: clearing the farmer's marriage failed; continuing the reset.\n{ex}", LogLevel.Error);
            }
            return partners;
        }

        /// <summary>Saves already hit by the bug (married on paper, hearts wiped): at load, a
        /// Farmer.spouse with no Married, Engaged or roommate friendship behind it is left over
        /// from an earlier loop. Clear it and send that villager home. A real marriage keeps its
        /// Married status, so it never matches.</summary>
        public void RepairOrphanedMarriage(Farmer p)
        {
            try
            {
                string spouse = p.spouse;
                bool hasEntry = spouse != null && p.friendshipData.TryGetValue(spouse, out Friendship f) && f != null;
                bool partner = hasEntry && IsPartnerStatus(p.friendshipData[spouse]);
                if (!MarriageResetRules.IsOrphanedSpouse(spouse, hasEntry, partner))
                    return;

                p.spouse = null;
                p.divorceTonight.Value = false;
                int cachesCleared = ClearModSpouseCaches(p);
                _monitor.Log(
                    $"Load: {spouse} was still recorded as the spouse with no marriage behind it (left over from an " +
                    $"earlier loop); cleared it, {cachesCleared} mod spouse cache(s).",
                    LogLevel.Warn);
                ClearMarriageWorld(p, new[] { spouse }, wholeWorld: false);
            }
            catch (Exception ex)
            {
                _monitor.Log($"Load: repairing a left-over spouse failed.\n{ex}", LogLevel.Error);
            }
        }

        private static bool IsPartnerStatus(Friendship f)
            => f != null && (f.IsMarried() || f.IsEngaged() || f.RoommateMarriage);

        /// <summary>World-side marriage state, run after friendshipData is cleared: send former
        /// partners back to their data home, strip marriage mod data, and redo the spouse room and
        /// patio the way vanilla doDivorce does. With <paramref name="wholeWorld"/> (the rewind) it
        /// also sends home any villager still homed in a farmhouse and removes children.</summary>
        private void ClearMarriageWorld(Farmer p, IReadOnlyList<string> partners, bool wholeWorld = true)
        {
            try
            {
                var formerPartners = new HashSet<string>(partners, StringComparer.Ordinal);
                var toSendHome = new List<NPC>();
                var clearedKeys = new List<string>();

                // Collect first: PerformDivorce warps the NPC, which removes it from the location's
                // character list ForEachVillager is walking ("Collection was modified").
                Utility.ForEachVillager(npc =>
                {
                    bool wasPartner = formerPartners.Contains(npc.Name);
                    if (!wasPartner && !wholeWorld) return true;
                    foreach (string key in RemoveMarriageModData(npc.modData))
                        clearedKeys.Add($"{npc.Name}:{key}");
                    bool homedWithFarmer = IsFarmerHome(Game1.getLocationFromName(npc.DefaultMap))
                        || IsFarmerHome(npc.currentLocation);
                    if ((wasPartner || (wholeWorld && homedWithFarmer)) && npc.getSpouse() == null)
                        toSendHome.Add(npc);
                    return true;
                });

                var sentHome = new List<string>();
                foreach (NPC npc in toSendHome)
                {
                    try
                    {
                        npc.PerformDivorce();
                        sentHome.Add(npc.Name);
                    }
                    catch (Exception ex)
                    {
                        _monitor.Log($"Reset: could not send {npc.Name} home.\n{ex}", LogLevel.Error);
                    }
                }

                int children = wholeWorld ? RemoveChildren() : 0;

                try
                {
                    Utility.getHomeOfFarmer(p)?.showSpouseRoom();
                    Game1.getFarm()?.UpdatePatio();
                }
                catch (Exception ex)
                {
                    _monitor.Log($"Reset: could not refresh the spouse room or patio.\n{ex}", LogLevel.Error);
                }

                if (sentHome.Count > 0 || clearedKeys.Count > 0 || children > 0)
                    _monitor.Log(
                        $"Marriage clear: sent home [{string.Join(", ", sentHome)}], removed mod data " +
                        $"[{string.Join(", ", clearedKeys)}], {children} child(ren).",
                        LogLevel.Info);

                LogLeftoverMarriage(p);
            }
            catch (Exception ex)
            {
                _monitor.Log($"Reset: clearing the world side of the marriage failed; continuing.\n{ex}", LogLevel.Error);
            }
        }

        /// <summary>FarmHouse covers the cabins (Cabin derives from FarmHouse); IslandFarmHouse is
        /// the Ginger Island house. A modded map that is merely named like a cabin does not count.</summary>
        private static bool IsFarmerHome(GameLocation loc) => loc is FarmHouse || loc is IslandFarmHouse;

        private static int RemoveChildren()
        {
            int children = 0;
            Utility.ForEachLocation(loc =>
            {
                if (!IsFarmerHome(loc)) return true;
                for (int i = loc.characters.Count - 1; i >= 0; i--)
                {
                    if (loc.characters[i] is Child)
                    {
                        loc.characters.RemoveAt(i);
                        children++;
                    }
                }
                return true;
            });
            return children;
        }

        /// <summary>Read the marriage back through the public getters (which other mods may patch)
        /// and warn about anything that still says married, so a mod that keeps spouses somewhere
        /// we do not know shows up in the log.</summary>
        private void LogLeftoverMarriage(Farmer p)
        {
            var leftovers = new List<string>();
            if (!string.IsNullOrEmpty(p.spouse))
                leftovers.Add($"Farmer.spouse={p.spouse}");
            if (p.isMarriedOrRoommates() || p.isEngaged())
                leftovers.Add("farmer reads married or engaged");
            Utility.ForEachVillager(npc =>
            {
                if (npc.isMarriedOrEngaged())
                    leftovers.Add($"{npc.Name} reads married");
                return true;
            });
            if (leftovers.Count > 0)
                _monitor.Log("Marriage clear: marriage data still present (another mod may keep it): "
                    + string.Join("; ", leftovers), LogLevel.Warn);
            else
                _monitor.Log("Marriage clear: no marriage state left (spouse none, no villager married).", LogLevel.Trace);
        }

        private static List<string> RemoveMarriageModData(StardewValley.Mods.ModDataDictionary modData)
        {
            if (modData == null) return new List<string>();
            List<string> keys = modData.Keys.Where(MarriageResetRules.IsMarriageModDataKey).ToList();
            foreach (string key in keys)
                modData.Remove(key);
            return keys;
        }

        private int ClearModSpouseCaches(Farmer p)
        {
            int cleared = 0;
            foreach ((string asm, string type, string field) in ModSpouseCaches)
            {
                try
                {
                    if (FindStaticField(asm, type, field)?.GetValue(null) is IDictionary cache && cache.Contains(p.UniqueMultiplayerID))
                    {
                        cache.Remove(p.UniqueMultiplayerID);
                        cleared++;
                    }
                }
                catch (Exception ex)
                {
                    _monitor.Log($"Marriage clear: could not clear {type}.{field} ({ex.GetType().Name}: {ex.Message}).", LogLevel.Warn);
                }
            }
            foreach ((string asm, string type, string field) in ModSpouseSlots)
            {
                try
                {
                    FieldInfo slot = FindStaticField(asm, type, field);
                    if (slot != null && !slot.IsInitOnly && !slot.FieldType.IsValueType && slot.GetValue(null) != null)
                    {
                        slot.SetValue(null, null);
                        cleared++;
                    }
                }
                catch (Exception ex)
                {
                    _monitor.Log($"Marriage clear: could not clear {type}.{field} ({ex.GetType().Name}: {ex.Message}).", LogLevel.Warn);
                }
            }
            return cleared;
        }

        private static FieldInfo FindStaticField(string assemblyName, string typeName, string fieldName)
        {
            Assembly asm = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == assemblyName);
            return asm?.GetType(typeName)?.GetField(fieldName,
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        }
    }
}
