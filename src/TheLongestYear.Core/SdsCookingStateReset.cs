using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace TheLongestYear.Core
{
    /// <summary>
    /// Pure part of the rewind's reset of Seven Deadly Sins' cooking progress (2026-11-05). The mod's
    /// cooking stations keep, per player, which dishes were learned and cooked and whether each dish's
    /// kitchen recipe was handed out, in SMAPI save data, which the rewind keeps on purpose. The rewind
    /// takes the kitchen recipes (recipes rewind unless banked in the Cookbook), and the mod never
    /// hands a recipe out twice, so from the second loop those recipes could not be earned at all.
    /// The rewind therefore drops the reset player's entry; the mod makes a fresh one. Other players'
    /// entries stay: the rewind resets only the host's farmer.
    /// </summary>
    public static class SdsCookingStateReset
    {
        /// <summary>The C# part's id: the one that owns the cooking stations and their save data.</summary>
        public const string ModId = "flqzzzz.SevenDeadlySins-SMAPI";

        /// <summary>The <c>Game1.CustomData</c> key (the mod writes key "CookingSaveData").</summary>
        public static readonly string SaveDataKey = $"smapi/mod-data/{ModId}/CookingSaveData".ToLowerInvariant();

        private const string Players = "players";

        public enum Outcome { Reset, NothingToChange, Unexpected }

        /// <summary>The saved cooking data without <paramref name="playerId"/>'s entry.</summary>
        public static Outcome Reset(string? json, long playerId, out string? rewritten)
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
            if (state[Players] is not JsonObject players
                || !players.Remove(playerId.ToString(CultureInfo.InvariantCulture)))
                return Outcome.NothingToChange;
            rewritten = state.ToJsonString();
            return Outcome.Reset;
        }
    }
}
