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
    /// Takes Unlockable Bundles' saved progress (paid bundles, wallet currencies, prize machines) and
    /// Seven Deadly Sins' cooking progress back to a fresh game on the rewind (rules and reasons:
    /// <see cref="UnlockableBundlesStateReset"/>, <see cref="SdsCookingStateReset"/>). Soft
    /// integration: nothing happens without those mods. Both keep a live copy next to the SMAPI
    /// save data and write the live copy back at the next save, so both places are reset: the saved
    /// entry by rewriting it (as <see cref="DeepWoodsRewind"/> does), the live copy in place through
    /// reflection. Host only and fail open: an error is logged and the rest of the rewind goes on.
    /// </summary>
    internal sealed class UnlockableBundlesRewind
    {
        private const string ModDataTypeName = "Unlockable_Bundles.Lib.ModData";
        private const string InstanceField = "Instance";
        private const string ApiTypeName = "Unlockable_Bundles.API.UnlockableBundlesAPI";
        private const string ClearCacheMethod = "ClearCache";
        private const string PurchasedProperty = "Purchased";

        private const string CookingManagerTypeName = "SevenDeadlySins.CookingSystem.CookingManager";
        private const string CookingSaveDataProperty = "SaveData";
        private const string CookingPlayersProperty = "Players";

        private readonly IModRegistry _registry;
        private readonly IMonitor _monitor;

        public UnlockableBundlesRewind(IModRegistry registry, IMonitor monitor)
        {
            _registry = registry;
            _monitor = monitor;
        }

        /// <summary>Reset both mods' saved state for <paramref name="p"/>, the host.</summary>
        public void Apply(Farmer p)
        {
            if (p == null || !p.IsLocalPlayer || !Context.IsMainPlayer)
                return;
            try
            {
                if (_registry.IsLoaded(UnlockableBundlesStateReset.ModId))
                    ResetBundles();
            }
            catch (Exception ex)
            {
                _monitor.Log($"Rewind: Unlockable Bundles reset failed; the rest of the rewind goes on. {ex}", LogLevel.Warn);
            }
            try
            {
                if (_registry.IsLoaded(SdsCookingStateReset.ModId))
                    ResetCooking(p);
            }
            catch (Exception ex)
            {
                _monitor.Log($"Rewind: Seven Deadly Sins cooking reset failed; the rest of the rewind goes on. {ex}", LogLevel.Warn);
            }
        }

        private void ResetBundles()
        {
            IReadOnlySet<string> kept = SdsUnlocks.KeptBundles(id => UpgradeChecker.HasUpgrade?.Invoke(id) == true);
            int rewrote = 0;
            foreach (IDictionary<string, string> data in SavedDataFields())
            {
                if (!data.TryGetValue(UnlockableBundlesStateReset.SaveDataKey, out string json))
                    continue;
                var outcome = UnlockableBundlesStateReset.Reset(json, kept, out string rewritten);
                if (outcome == UnlockableBundlesStateReset.Outcome.Unexpected)
                {
                    _monitor.Log("Rewind: Unlockable Bundles' saved state is not in the expected shape; left as is, so paid " +
                        "bundles and their currencies may carry into the next loop.", LogLevel.Warn);
                    continue;
                }
                if (outcome != UnlockableBundlesStateReset.Outcome.Reset)
                    continue;
                data[UnlockableBundlesStateReset.SaveDataKey] = rewritten;
                rewrote++;
            }

            // The live copy: the framework reads the save data once per session and writes this
            // object back at every day end, so it must lose the same records.
            int live = 0;
            object instance = LiveState();
            if (instance != null)
            {
                if (TableOf(instance, UnlockableBundlesStateReset.Bundles) is IDictionary bundles)
                {
                    var drop = new List<object>();
                    foreach (object key in bundles.Keys)
                        if (key is not string id || !kept.Contains(id)) drop.Add(key);
                    foreach (object key in drop) bundles.Remove(key);
                    live += drop.Count;
                }
                foreach (string table in UnlockableBundlesStateReset.EmptiedTables)
                    if (TableOf(instance, table) is IDictionary entries && entries.Count > 0)
                    {
                        live += entries.Count;
                        entries.Clear();
                    }
                AccessTools.Method(AccessTools.TypeByName(ApiTypeName), ClearCacheMethod)?.Invoke(null, null);
            }
            _monitor.Log($"Rewind: reset Unlockable Bundles' progress (paid bundles, wallet currencies, prize machines) in {rewrote} saved " +
                $"place(s) and {live} live record(s); kept bundle(s): {(kept.Count == 0 ? "none" : string.Join(", ", kept))}.", LogLevel.Info);
        }

        private void ResetCooking(Farmer p)
        {
            int rewrote = 0;
            foreach (IDictionary<string, string> data in SavedDataFields())
            {
                if (!data.TryGetValue(SdsCookingStateReset.SaveDataKey, out string json))
                    continue;
                var outcome = SdsCookingStateReset.Reset(json, p.UniqueMultiplayerID, out string rewritten);
                if (outcome == SdsCookingStateReset.Outcome.Unexpected)
                {
                    _monitor.Log("Rewind: Seven Deadly Sins' cooking data is not in the expected shape; left as is.", LogLevel.Warn);
                    continue;
                }
                if (outcome != SdsCookingStateReset.Outcome.Reset)
                    continue;
                data[SdsCookingStateReset.SaveDataKey] = rewritten;
                rewrote++;
            }
            bool live = false;
            Type manager = AccessTools.TypeByName(CookingManagerTypeName);
            object saveData = manager == null ? null : AccessTools.Property(manager, CookingSaveDataProperty)?.GetValue(null);
            if (saveData != null && AccessTools.Property(saveData.GetType(), CookingPlayersProperty)?.GetValue(saveData) is IDictionary players
                && players.Contains(p.UniqueMultiplayerID))
            {
                players.Remove(p.UniqueMultiplayerID);
                live = true;
            }
            _monitor.Log($"Rewind: reset Seven Deadly Sins' cooking progress in {rewrote} saved place(s){(live ? " and the live copy" : "")}.", LogLevel.Info);
        }

        /// <summary>Whether the bundle is paid for right now: the reach for a keep row. Reads the
        /// framework's live state first (a purchase updates it at once; the save data only at day
        /// end), then the saved entry. False without the framework or when neither can be read.</summary>
        public static bool IsBundlePurchased(string bundleId)
        {
            if (string.IsNullOrEmpty(bundleId)) return false;
            try
            {
                object instance = LiveState();
                if (instance != null && TableOf(instance, UnlockableBundlesStateReset.Bundles) is IDictionary bundles)
                {
                    if (!bundles.Contains(bundleId) || bundles[bundleId] is not IDictionary places) return false;
                    foreach (object record in places.Values)
                        if (record != null && AccessTools.Property(record.GetType(), PurchasedProperty)?.GetValue(record) is bool paid && paid)
                            return true;
                    return false;
                }
            }
            catch (Exception)
            {
                // Fall back to the saved entry below; a renamed member must not break the shrine.
            }
            foreach (IDictionary<string, string> data in SavedDataFields())
                if (data.TryGetValue(UnlockableBundlesStateReset.SaveDataKey, out string json))
                    return UnlockableBundlesStateReset.IsPurchased(json, bundleId);
            return false;
        }

        private static object LiveState()
        {
            Type type = AccessTools.TypeByName(ModDataTypeName);
            return type == null ? null : AccessTools.Field(type, InstanceField)?.GetValue(null);
        }

        private static object TableOf(object instance, string name)
            => AccessTools.Property(instance.GetType(), name)?.GetValue(instance);

        private static IEnumerable<IDictionary<string, string>> SavedDataFields()
        {
            if (Game1.CustomData != null)
                yield return Game1.CustomData;
            if (SaveGame.loaded?.CustomData != null && !ReferenceEquals(SaveGame.loaded.CustomData, Game1.CustomData))
                yield return SaveGame.loaded.CustomData;
        }
    }
}
