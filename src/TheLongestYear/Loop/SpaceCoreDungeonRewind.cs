using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Netcode;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Network;
using TheLongestYear.Core;

namespace TheLongestYear.Loop
{
    /// <summary>
    /// Takes the deepest floor of every SpaceCore dungeon back to none on the rewind (Sword and
    /// Sorcery's Deep Dark, and any other mod's dungeon built on SpaceCore). SpaceCore keeps the
    /// floors on the farm team (an internal table, <c>SpaceCore.Dungeons.DungeonImpl.deepestLevels</c>),
    /// writes them to its save data on Saving and adds them back on SaveLoaded. The rewind keeps other
    /// mods' save data on purpose (<see cref="ModSaveDataCarryover"/>), so the elevator stops carried
    /// into every later loop, as Deep Woods' depth did (<see cref="DeepWoodsRewind"/>).
    ///
    /// Both copies are cleared: the live table in place (SpaceCore attached that dictionary to the
    /// team's net fields, so it is emptied, not replaced), so the post-rewind save writes nothing, and
    /// the saved entry, so the rewind's SaveLoaded adds nothing back. Host only and fail open: an error
    /// is logged and the rest of the rewind goes on.
    /// </summary>
    internal sealed class SpaceCoreDungeonRewind
    {
        private const string DungeonTypeName = "SpaceCore.Dungeons.DungeonImpl";
        private const string TableField = "deepestLevels";

        private readonly IModRegistry _registry;
        private readonly IMonitor _monitor;

        public SpaceCoreDungeonRewind(IModRegistry registry, IMonitor monitor)
        {
            _registry = registry;
            _monitor = monitor;
        }

        /// <summary>Clear the deepest floors for the team of <paramref name="p"/>, the host.</summary>
        public void Apply(Farmer p)
        {
            if (p == null || !p.IsLocalPlayer || !Context.IsMainPlayer)
                return;
            if (!_registry.IsLoaded(CustomSkillReset.SpaceCoreModId))
                return;

            var cleared = new List<string>();
            try
            {
                var table = AccessTools.Field(AccessTools.TypeByName(DungeonTypeName), TableField)?.GetValue(null)
                    as ConditionalWeakTable<FarmerTeam, NetStringDictionary<int, NetInt>>;
                if (table == null)
                    _monitor.Log($"Rewind: SpaceCore is loaded but {DungeonTypeName}.{TableField} was not found; its dungeons' deepest floors may carry into the next loop.", LogLevel.Warn);
                else if (p.team != null && table.TryGetValue(p.team, out NetStringDictionary<int, NetInt> levels))
                {
                    foreach (string dungeon in levels.Keys)
                        cleared.Add($"{dungeon} {levels[dungeon]}");
                    levels.Clear();
                }
            }
            catch (Exception ex)
            {
                _monitor.Log($"Rewind: clearing SpaceCore's live dungeon floors failed; the saved floors are still cleared. {ex}", LogLevel.Warn);
            }

            try
            {
                int removed = 0;
                foreach (IDictionary<string, string> data in SavedDataFields())
                    if (data.Remove(CustomSkillReset.SpaceCoreDungeonDepthDataKey))
                        removed++;
                if (cleared.Count > 0 || removed > 0)
                    _monitor.Log($"Rewind: reset SpaceCore dungeon floors ({(cleared.Count > 0 ? string.Join(", ", cleared) : "none live")}; saved entry removed in {removed} place(s)).", LogLevel.Info);
            }
            catch (Exception ex)
            {
                _monitor.Log($"Rewind: removing SpaceCore's saved dungeon floors failed; the rest of the rewind goes on. {ex}", LogLevel.Warn);
            }
        }

        private static IEnumerable<IDictionary<string, string>> SavedDataFields()
        {
            if (Game1.CustomData != null)
                yield return Game1.CustomData;
            if (SaveGame.loaded?.CustomData != null && !ReferenceEquals(SaveGame.loaded.CustomData, Game1.CustomData))
                yield return SaveGame.loaded.CustomData;
        }
    }
}
