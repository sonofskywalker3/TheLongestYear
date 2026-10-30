using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using StardewModdingAPI;
using StardewValley;
using TheLongestYear.Core;
using TheLongestYear.Integration;

namespace TheLongestYear.Loop
{
    /// <summary>
    /// Wipes SpaceCore custom skills (Luck Skill, Cooking, Archaeology, ...) on the rewind, the
    /// same as the vanilla five (Jeff, 2026-10-30). Soft integration: nothing happens without
    /// SpaceCore. Shrine skill keeps cover only the vanilla five, so every custom skill goes to 0.
    ///
    /// Three places hold the XP, and all three are cleared:
    ///  1. SpaceCore's live table, through its API (AddExperienceForCustomSkill with the negative
    ///     of the current XP; it does not clamp). It also tells farmhands the new value.
    ///  2. SpaceCore's saved table in <c>Game1.CustomData</c>. loadForNewGame keeps CustomData and
    ///     SMAPI raises SaveLoaded again after the rewind, where SpaceCore replaces its live table
    ///     with the saved one. Without this step the wipe is undone a moment later.
    ///  3. SpaceCore's queue of level-up menus for tonight (reflection, best effort), the
    ///     custom-skill twin of <c>Farmer.newLevels</c>: a queued menu would hand out a profession
    ///     for a level the player no longer has.
    /// Custom professions live in <c>Farmer.professions</c> (by vanilla id), which FarmerReset
    /// already clears. SpaceCore only puts them back from the skill level (on warp, with All
    /// Professions installed), and the level is now 0.
    /// </summary>
    internal sealed class SpaceCoreSkillReset
    {
        private readonly IModRegistry _registry;
        private readonly IMonitor _monitor;

        public SpaceCoreSkillReset(IModRegistry registry, IMonitor monitor)
        {
            _registry = registry;
            _monitor = monitor;
        }

        /// <summary>Wipe every custom skill for <paramref name="p"/>. Only the local player:
        /// a farmhand's SpaceCore table is its own, on its own machine.</summary>
        public void Apply(Farmer p)
        {
            if (p == null || !p.IsLocalPlayer || !_registry.IsLoaded(CustomSkillReset.SpaceCoreModId))
                return;

            var reset = new List<string>();
            ISpaceCoreApi api = _registry.GetApi<ISpaceCoreApi>(CustomSkillReset.SpaceCoreModId);
            if (api == null)
                _monitor.Log("Rewind: SpaceCore is loaded but its API could not be read; clearing only its saved skill data.", LogLevel.Warn);
            else
            {
                foreach (string skill in CustomSkillReset.SkillsToReset(api.GetCustomSkills()))
                {
                    int xp = api.GetExperienceForCustomSkill(p, skill);
                    if (xp == 0)
                        continue;
                    api.AddExperienceForCustomSkill(p, skill, CustomSkillReset.AmountToZero(xp));
                    reset.Add($"{skill} ({xp} XP)");
                }
            }

            IReadOnlyList<string> savedZeroed = Context.IsMainPlayer ? ZeroSavedTable(p) : System.Array.Empty<string>();
            int queued = ClearQueuedLevelUps();

            string names = reset.Count > 0 ? string.Join(", ", reset) : "none had XP";
            _monitor.Log(
                $"Rewind: reset SpaceCore custom skills to 0: {names}. Saved entries zeroed: {savedZeroed.Count}" +
                (queued > 0 ? $"; dropped {queued} queued custom level-up menu(s)." : "."),
                LogLevel.Info);
        }

        /// <summary>Zero this player's entries in the saved table, in both places SMAPI reads it
        /// from (Game1.CustomData first, then SaveGame.loaded's copy).</summary>
        private static IReadOnlyList<string> ZeroSavedTable(Farmer p)
        {
            IReadOnlyList<string> zeroed = System.Array.Empty<string>();
            foreach (IDictionary<string, string> data in SavedDataFields())
            {
                if (!data.TryGetValue(CustomSkillReset.SpaceCoreSkillsDataKey, out string json))
                    continue;
                string rewritten = CustomSkillReset.ZeroPlayerInSavedTable(json, p.UniqueMultiplayerID, out IReadOnlyList<string> changed);
                if (rewritten == null)
                    continue;
                data[CustomSkillReset.SpaceCoreSkillsDataKey] = rewritten;
                if (changed.Count > zeroed.Count)
                    zeroed = changed;
            }
            return zeroed;
        }

        private static IEnumerable<IDictionary<string, string>> SavedDataFields()
        {
            if (Game1.CustomData != null)
                yield return Game1.CustomData;
            if (SaveGame.loaded?.CustomData != null && !ReferenceEquals(SaveGame.loaded.CustomData, Game1.CustomData))
                yield return SaveGame.loaded.CustomData;
        }

        /// <summary>SpaceCore.Skills.NewLevels (internal) is tonight's queue of custom level-up
        /// menus for the local player. Best effort: a renamed member only skips this step.</summary>
        private int ClearQueuedLevelUps()
        {
            try
            {
                var skillsType = AccessTools.TypeByName("SpaceCore.Skills");
                if (skillsType == null || AccessTools.Property(skillsType, "NewLevels")?.GetValue(null) is not IList queue)
                {
                    _monitor.Log("Rewind: SpaceCore's level-up queue was not found; skipped clearing it.", LogLevel.Trace);
                    return 0;
                }
                int count = queue.Count;
                queue.Clear();
                return count;
            }
            catch (System.Reflection.TargetInvocationException ex)
            {
                _monitor.Log($"Rewind: could not clear SpaceCore's level-up queue: {ex.InnerException?.Message}", LogLevel.Trace);
                return 0;
            }
        }
    }
}
