using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Characters;
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
            IReadOnlyList<string> partners = MarriageResetRules.FormerPartners(
                p.spouse,
                p.friendshipData.Pairs.Select(kv => (kv.Key,
                    kv.Value.IsMarried() || kv.Value.IsEngaged() || kv.Value.RoommateMarriage)));

            int cachesCleared = ClearModSpouseCaches(p);

            // Vanilla doDivorce: spouse null, divorce flag off. Engagement and wedding countdown
            // live on the friendship entry, which the caller clears next.
            p.spouse = null;
            p.divorceTonight.Value = false;
            int farmerKeys = RemoveMarriageModData(p.modData);

            if (partners.Count > 0 || cachesCleared > 0 || farmerKeys > 0)
                _monitor.Log(
                    $"Reset: marriage ended with the rewind. Former partner(s): [{string.Join(", ", partners)}]; " +
                    $"cleared {cachesCleared} mod spouse cache(s), {farmerKeys} farmer marriage key(s).",
                    LogLevel.Info);
            return partners;
        }

        /// <summary>World-side marriage state, run after friendshipData is cleared: send former
        /// partners (and any villager still homed in a farmhouse) back to their data home, strip
        /// marriage mod data from villagers, remove children, and redo the spouse room and patio the
        /// way vanilla doDivorce does. A failure is logged and the reset goes on.</summary>
        private void ClearMarriageWorld(Farmer p, IReadOnlyList<string> partners)
        {
            try
            {
                var sentHome = new List<string>();
                int npcKeys = 0;
                var formerPartners = new HashSet<string>(partners, StringComparer.Ordinal);
                Utility.ForEachVillager(npc =>
                {
                    npcKeys += RemoveMarriageModData(npc.modData);
                    bool wasPartner = formerPartners.Contains(npc.Name);
                    bool homedWithFarmer = MarriageResetRules.IsFarmerHomeMap(npc.DefaultMap)
                        || MarriageResetRules.IsFarmerHomeMap(npc.currentLocation?.NameOrUniqueName);
                    if ((wasPartner || homedWithFarmer) && npc.getSpouse() == null)
                    {
                        npc.PerformDivorce();
                        sentHome.Add(npc.Name);
                    }
                    return true;
                });

                int children = 0;
                Utility.ForEachLocation(loc =>
                {
                    if (!MarriageResetRules.IsFarmerHomeMap(loc.NameOrUniqueName)) return true;
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

                Utility.getHomeOfFarmer(p)?.showSpouseRoom();
                Game1.getFarm()?.UpdatePatio();

                if (sentHome.Count > 0 || npcKeys > 0 || children > 0)
                    _monitor.Log(
                        $"Reset: sent home [{string.Join(", ", sentHome)}], removed {npcKeys} villager marriage key(s), " +
                        $"{children} child(ren).",
                        LogLevel.Info);

                LogLeftoverMarriage(p);
            }
            catch (Exception ex)
            {
                _monitor.Log($"Reset: clearing the world side of the marriage failed; continuing.\n{ex}", LogLevel.Error);
            }
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
                _monitor.Log("Reset: marriage data still present after the rewind (another mod may keep it): "
                    + string.Join("; ", leftovers), LogLevel.Warn);
            else
                _monitor.Log("Reset: no marriage state left (spouse none, no villager married).", LogLevel.Trace);
        }

        private static int RemoveMarriageModData(StardewValley.Mods.ModDataDictionary modData)
        {
            if (modData == null) return 0;
            List<string> keys = modData.Keys.Where(MarriageResetRules.IsMarriageModDataKey).ToList();
            foreach (string key in keys)
                modData.Remove(key);
            return keys.Count;
        }

        private int ClearModSpouseCaches(Farmer p)
        {
            int cleared = 0;
            foreach ((string asm, string type, string field) in ModSpouseCaches)
            {
                if (FindStaticField(asm, type, field)?.GetValue(null) is IDictionary cache && cache.Contains(p.UniqueMultiplayerID))
                {
                    cache.Remove(p.UniqueMultiplayerID);
                    cleared++;
                }
            }
            foreach ((string asm, string type, string field) in ModSpouseSlots)
            {
                FieldInfo slot = FindStaticField(asm, type, field);
                if (slot != null && !slot.FieldType.IsValueType && slot.GetValue(null) != null)
                {
                    slot.SetValue(null, null);
                    cleared++;
                }
            }
            return cleared;
        }

        private FieldInfo FindStaticField(string assemblyName, string typeName, string fieldName)
        {
            try
            {
                Assembly asm = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == assemblyName);
                return asm?.GetType(typeName)?.GetField(fieldName,
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            }
            catch (Exception ex) when (ex is TypeLoadException || ex is ReflectionTypeLoadException)
            {
                _monitor.Log($"Reset: could not read {typeName}.{fieldName} ({ex.Message}).", LogLevel.Trace);
                return null;
            }
        }
    }
}
