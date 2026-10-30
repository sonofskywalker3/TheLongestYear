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

        /// <summary>What <see cref="ZeroPlayerInSavedTable"/> found in the saved table.</summary>
        public enum SavedTableOutcome
        {
            /// <summary>Some of the player's XP was not zero; the rewritten JSON is returned.</summary>
            Zeroed,
            /// <summary>Nothing saved yet, no row for this player, or every value already 0.</summary>
            NothingToChange,
            /// <summary>The JSON does not parse, or is not { id: { skill: xp } } for this player.
            /// The caller warns: SpaceCore will reload the old XP from it.</summary>
            Unexpected,
        }

        /// <summary>
        /// Zero one player's entries in SpaceCore's saved skill table, leaving every other
        /// player's entries as they are. SpaceCore re-reads this table on SaveLoaded, which SMAPI
        /// raises again after the rewind, so the live table alone is not enough.
        /// <paramref name="rewritten"/> is the new JSON when the outcome is Zeroed, else null;
        /// <paramref name="zeroed"/> lists the skills whose saved XP was not zero.
        /// </summary>
        public static SavedTableOutcome ZeroPlayerInSavedTable(
            string? json, long playerId, out string? rewritten, out IReadOnlyList<string> zeroed)
        {
            rewritten = null;
            zeroed = Array.Empty<string>();
            if (string.IsNullOrWhiteSpace(json))
                return SavedTableOutcome.NothingToChange;

            JsonNode? root;
            try
            {
                root = JsonNode.Parse(json);
            }
            catch (JsonException)
            {
                return SavedTableOutcome.Unexpected;
            }

            if (root is not JsonObject table)
                return SavedTableOutcome.Unexpected;
            JsonNode? row = table[playerId.ToString()];
            if (row == null)
                return SavedTableOutcome.NothingToChange;
            if (row is not JsonObject skills)
                return SavedTableOutcome.Unexpected;

            var changed = new List<string>();
            foreach (string skill in skills.Select(kv => kv.Key).ToList())
            {
                if (skills[skill] is JsonValue value && value.TryGetValue(out int xp) && xp == 0)
                    continue;
                skills[skill] = 0;
                changed.Add(skill);
            }

            if (changed.Count == 0)
                return SavedTableOutcome.NothingToChange;
            zeroed = changed;
            rewritten = table.ToJsonString();
            return SavedTableOutcome.Zeroed;
        }
    }
}
