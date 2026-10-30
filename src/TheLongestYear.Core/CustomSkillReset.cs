using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace TheLongestYear.Core
{
    /// <summary>
    /// Pure parts of the rewind's custom-skill wipe (Jeff, 2026-10-30: "Yes, reset them like
    /// vanilla skills"). SpaceCore keeps each custom skill's XP (Luck Skill, Cooking,
    /// Archaeology, ...) in its own table, not on the Farmer, so the vanilla skill wipe never
    /// reached it and every custom level survived the loop. Luck Skill then copies its level
    /// into vanilla <c>luckLevel</c>, which counts toward the mastery threshold.
    /// </summary>
    public static class CustomSkillReset
    {
        /// <summary>SpaceCore's mod id; its SMAPI save-data key for skill XP is "skills".</summary>
        public const string SpaceCoreModId = "spacechase0.SpaceCore";

        /// <summary>The <c>Game1.CustomData</c> key SMAPI files SpaceCore's skill XP under
        /// (SMAPI DataHelper.GetSaveFileKey: <c>smapi/mod-data/{modId}/{key}</c>, lowercased).
        /// Shape: <c>{ "&lt;multiplayerId&gt;": { "&lt;skillId&gt;": xp } }</c>.</summary>
        public static readonly string SpaceCoreSkillsDataKey = $"smapi/mod-data/{SpaceCoreModId}/skills".ToLowerInvariant();

        /// <summary>The skills to wipe from SpaceCore's own list: each id once, blanks dropped,
        /// in SpaceCore's order. Null (no SpaceCore API) means none.</summary>
        public static IReadOnlyList<string> SkillsToReset(IEnumerable<string>? customSkills)
        {
            if (customSkills == null)
                return Array.Empty<string>();
            return customSkills
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.Ordinal)
                .ToList();
        }

        /// <summary>The XP change that takes a skill from <paramref name="currentXp"/> to zero.
        /// SpaceCore's AddExperience adds the amount without clamping, so this is exact.</summary>
        public static int AmountToZero(int currentXp) => -currentXp;

        /// <summary>
        /// Zero one player's entries in SpaceCore's saved skill table, leaving every other
        /// player's entries as they are. SpaceCore re-reads this table on SaveLoaded, which SMAPI
        /// raises again after the rewind, so the live table alone is not enough.
        /// Returns the rewritten JSON, or null when there is nothing to change (no entry for the
        /// player, every value already zero, or JSON that is not the expected shape).
        /// <paramref name="zeroed"/> lists the skills whose saved XP was not zero.
        /// </summary>
        public static string? ZeroPlayerInSavedTable(string? json, long playerId, out IReadOnlyList<string> zeroed)
        {
            zeroed = Array.Empty<string>();
            if (string.IsNullOrWhiteSpace(json))
                return null;

            JsonObject? table;
            try
            {
                table = JsonNode.Parse(json) as JsonObject;
            }
            catch (JsonException)
            {
                return null;
            }

            if (table == null || table[playerId.ToString()] is not JsonObject skills)
                return null;

            var changed = new List<string>();
            foreach (string skill in skills.Select(kv => kv.Key).ToList())
            {
                if (skills[skill] is JsonValue value && value.TryGetValue(out int xp) && xp == 0)
                    continue;
                skills[skill] = 0;
                changed.Add(skill);
            }

            if (changed.Count == 0)
                return null;
            zeroed = changed;
            return table.ToJsonString();
        }
    }
}
