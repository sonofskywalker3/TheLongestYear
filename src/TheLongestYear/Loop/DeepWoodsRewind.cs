using System;
using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using StardewModdingAPI;
using StardewValley;
using TheLongestYear.Core;

namespace TheLongestYear.Loop
{
    /// <summary>
    /// Takes Deep Woods' saved progress back to a fresh game on the rewind (rules and reasons:
    /// <see cref="DeepWoodsStateReset"/>). Soft integration: nothing happens without Deep Woods.
    /// Deep Woods reads its state from SMAPI save data on SaveLoaded, which SMAPI raises again after
    /// the rewind's reload, so rewriting the saved entry is enough; it then saves what it loaded.
    /// Host only and fail open, like <see cref="SpaceCoreSkillReset"/>: an error is logged and the
    /// rest of the rewind goes on.
    /// </summary>
    internal sealed class DeepWoodsRewind
    {
        private readonly IModRegistry _registry;
        private readonly IMonitor _monitor;

        public DeepWoodsRewind(IModRegistry registry, IMonitor monitor)
        {
            _registry = registry;
            _monitor = monitor;
        }

        /// <summary>Reset Deep Woods' saved state for <paramref name="p"/>, the host.</summary>
        public void Apply(Farmer p)
        {
            try
            {
                if (p == null || !p.IsLocalPlayer || !Context.IsMainPlayer)
                    return;
                if (!_registry.IsLoaded(DeepWoodsStateReset.DeepWoodsModId))
                    return;

                // Keep Stardrop (Unicorn) owned: the claim stays, so the Unicorn does not give it again.
                bool keepUnicorn = UpgradeChecker.HasUpgrade?.Invoke(WalletKeepTable.UnicornKeepId) == true;
                int rewrote = 0;
                foreach (IDictionary<string, string> data in SavedDataFields())
                {
                    if (!data.TryGetValue(DeepWoodsStateReset.SaveDataKey, out string json))
                        continue;
                    var outcome = DeepWoodsStateReset.Reset(json, p.UniqueMultiplayerID, out string rewritten, keepUnicorn);
                    if (outcome == DeepWoodsStateReset.Outcome.Unexpected)
                    {
                        _monitor.Log("Rewind: Deep Woods' saved state is not in the expected shape; left as is, so its " +
                            "depth and Unicorn Stardrop may carry into the next loop.", LogLevel.Warn);
                        continue;
                    }
                    if (outcome != DeepWoodsStateReset.Outcome.Reset)
                        continue;
                    data[DeepWoodsStateReset.SaveDataKey] = rewritten;
                    rewrote++;
                }
                _monitor.Log($"Rewind: reset Deep Woods' saved progress (depth, Unicorn Stardrop, obelisks, orb stones) in {rewrote} place(s).", LogLevel.Info);
            }
            catch (Exception ex)
            {
                _monitor.Log($"Rewind: Deep Woods reset failed; the rest of the rewind goes on. {ex}", LogLevel.Warn);
            }
        }

        private const string SettingsTypeName = "DeepWoodsMod.DeepWoodsSettings";
        private const string StateProperty = "DeepWoodsState";
        private const string UnicornPlayersProperty = "PlayersWhoGotStardropFromUnicorn";

        /// <summary>Whether the player has had the Unicorn's Stardrop this loop: the reach for the
        /// shrine's Keep Stardrop (Unicorn). Reads Deep Woods' live state first (petting the Unicorn
        /// updates it at once; the save data only at the next save), then the saved entry. False
        /// without Deep Woods or when neither can be read.</summary>
        public static bool HasUnicornStardrop(Farmer p)
        {
            if (p == null) return false;
            try
            {
                var settings = AccessTools.TypeByName(SettingsTypeName);
                object state = settings == null ? null : AccessTools.Property(settings, StateProperty)?.GetValue(null);
                if (state != null && AccessTools.Property(state.GetType(), UnicornPlayersProperty)?.GetValue(state) is IEnumerable ids)
                {
                    foreach (object id in ids)
                        if (id is long l && l == p.UniqueMultiplayerID) return true;
                    return false;
                }
            }
            catch (Exception)
            {
                // Fall back to the saved entry below; a renamed member must not break the shrine.
            }
            foreach (IDictionary<string, string> data in SavedDataFields())
                if (data.TryGetValue(DeepWoodsStateReset.SaveDataKey, out string json))
                    return DeepWoodsStateReset.HasUnicornStardrop(json, p.UniqueMultiplayerID);
            return false;
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
