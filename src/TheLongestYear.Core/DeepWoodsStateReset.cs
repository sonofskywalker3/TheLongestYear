using System.Text.Json;
using System.Text.Json.Nodes;

namespace TheLongestYear.Core
{
    /// <summary>
    /// Pure part of the rewind's Deep Woods reset (Jeff, 2026-11-05: support a mod by changing TLY to
    /// work with it). Deep Woods keeps its progress in SMAPI save data, which the rewind keeps on
    /// purpose (ModSaveDataCarryover), and re-reads it on the SaveLoaded SMAPI raises after the rewind.
    /// Seen live: depth 3 reached, rewind, the new loop's save still said 3. So the rewind rewrites the
    /// saved state before that SaveLoaded:
    /// <list type="bullet">
    /// <item><c>LowestLevelReached</c> to 0 (the entrance's constructor sets it back to 1 on load):
    /// it unlocks the Woods Obelisk and its warp depths.</item>
    /// <item>The reset player's id out of <c>PlayersWhoGotStardropFromUnicorn</c>: vanilla Stardrops
    /// are collectable again every loop (FarmerReset rebuilds max stamina), so the Unicorn's must be
    /// too, or the player loses its +34 for good. Other players' ids stay: the rewind resets only the
    /// host's farmer (SpaceCoreSkillReset keeps farmhands the same way).</item>
    /// <item><c>WoodsObeliskLocations</c> and <c>WoodsObeliskNonFarmLocations</c> emptied: the rewind
    /// takes the buildings, so the obelisks they name are gone.</item>
    /// <item><c>OrbStonesSaved</c> to 0: the Max's hut puzzle is world progress like any other.</item>
    /// </list>
    /// Any other field is kept as it is.
    /// </summary>
    public static class DeepWoodsStateReset
    {
        public const string DeepWoodsModId = "maxmakesmods.deepwoodsmod";

        /// <summary>The <c>Game1.CustomData</c> key SMAPI files Deep Woods' state under (DataHelper.GetSaveFileKey:
        /// <c>smapi/mod-data/{modId}/{key}</c>, lowercased; Deep Woods writes key "data").</summary>
        public static readonly string SaveDataKey = $"smapi/mod-data/{DeepWoodsModId}/data".ToLowerInvariant();

        private const string LowestLevel = "LowestLevelReached";
        private const string UnicornPlayers = "PlayersWhoGotStardropFromUnicorn";
        private const string ObeliskFarm = "WoodsObeliskLocations";
        private const string ObeliskOther = "WoodsObeliskNonFarmLocations";
        private const string OrbStones = "OrbStonesSaved";

        public enum Outcome
        {
            /// <summary>Something changed; the rewritten JSON is returned.</summary>
            Reset,
            /// <summary>Nothing saved, or already the state of a fresh game for this player.</summary>
            NothingToChange,
            /// <summary>Not a JSON object: left alone, and the caller warns.</summary>
            Unexpected,
        }

        /// <summary>The saved state with this loop's progress taken out, as listed on the class.
        /// <paramref name="keepUnicornStardrop"/>: the player owns the shrine's Keep Stardrop (Unicorn)
        /// row, so their claim stays and the Unicorn does not give it again.</summary>
        public static Outcome Reset(string? json, long playerId, out string? rewritten, bool keepUnicornStardrop = false)
        {
            rewritten = null;
            if (string.IsNullOrWhiteSpace(json))
                return Outcome.NothingToChange;
            JsonNode? root;
            try
            {
                root = JsonNode.Parse(json);
            }
            catch (JsonException)
            {
                return Outcome.Unexpected;
            }
            if (root is not JsonObject state)
                return Outcome.Unexpected;

            bool changed = false;
            changed |= SetZero(state, LowestLevel);
            changed |= SetZero(state, OrbStones);
            changed |= Empty(state, ObeliskFarm, new JsonArray());
            changed |= Empty(state, ObeliskOther, new JsonObject());
            if (!keepUnicornStardrop && state[UnicornPlayers] is JsonArray players)
            {
                for (int i = players.Count - 1; i >= 0; i--)
                {
                    if (players[i] is JsonValue v && v.TryGetValue(out long id) && id == playerId)
                    {
                        players.RemoveAt(i);
                        changed = true;
                    }
                }
            }

            if (!changed)
                return Outcome.NothingToChange;
            rewritten = state.ToJsonString();
            return Outcome.Reset;
        }

        /// <summary>Whether the saved state lists the player as having had the Unicorn's Stardrop.</summary>
        public static bool HasUnicornStardrop(string? json, long playerId)
        {
            if (string.IsNullOrWhiteSpace(json)) return false;
            try
            {
                if (JsonNode.Parse(json) is not JsonObject state || state[UnicornPlayers] is not JsonArray players)
                    return false;
                foreach (JsonNode? n in players)
                    if (n is JsonValue v && v.TryGetValue(out long id) && id == playerId)
                        return true;
                return false;
            }
            catch (JsonException)
            {
                return false;
            }
        }

        private static bool SetZero(JsonObject state, string field)
        {
            if (state[field] is JsonValue v && v.TryGetValue(out int n) && n == 0)
                return false;
            if (!state.ContainsKey(field))
                return false;
            state[field] = 0;
            return true;
        }

        private static bool Empty(JsonObject state, string field, JsonNode empty)
        {
            switch (state[field])
            {
                case JsonArray { Count: 0 }:
                case JsonObject { Count: 0 }:
                case null:
                    return false;
                default:
                    state[field] = empty;
                    return true;
            }
        }
    }
}
