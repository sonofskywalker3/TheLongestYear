using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace TheLongestYear.Core
{
    /// <summary>
    /// Pure part of the rewind's Unlockable Bundles reset (Seven Deadly Sins support, 2026-11-05).
    /// Unlockable Bundles is a framework other mods use for paid unlocks: a bundle that opens part of
    /// a map or hands out an item, wallet currencies, and prize machines. It keeps all of it in one
    /// SMAPI save-data entry, which the rewind keeps on purpose (ModSaveDataCarryover). Left alone:
    /// <list type="bullet">
    /// <item>a paid map bundle stayed paid, so its area was open for free in every later loop;</item>
    /// <item>an item bundle stayed paid while its reward went with the inventory: gone for good;</item>
    /// <item>currency balances carried over, where gold does not;</item>
    /// <item>a prize machine kept its place on the reward track, so its one-time rewards were never
    /// offered again.</item>
    /// </list>
    /// So the rewind takes the entry back to a fresh game: bundle records (<c>UnlockableSaveData</c>)
    /// except the kept ones, both currency tables, the prize machines and the found dig spots. Any
    /// other field is kept as it is. Like the rest of the rewind this resets the whole farm's record:
    /// the framework's bundles are shared world state.
    /// </summary>
    public static class UnlockableBundlesStateReset
    {
        public const string ModId = "DLX.Bundles";

        /// <summary>The <c>Game1.CustomData</c> key SMAPI files the state under (DataHelper.GetSaveFileKey:
        /// <c>smapi/mod-data/{modId}/{key}</c>, lowercased; the framework writes key "main").</summary>
        public static readonly string SaveDataKey = $"smapi/mod-data/{ModId}/main".ToLowerInvariant();

        public const string Bundles = "UnlockableSaveData";
        public const string CurrencyBalances = "WalletCurrencyData";
        public const string CurrencyTotals = "WalletCurrencyTotal";
        public const string PrizeMachines = "PrizeMachineData";
        public const string DigSpots = "FoundUniqueDigSpots";
        private const string Purchased = "Purchased";

        /// <summary>The tables emptied outright (the bundle table is filtered instead).</summary>
        public static readonly IReadOnlyList<string> EmptiedTables = new[] { CurrencyBalances, CurrencyTotals, PrizeMachines, DigSpots };

        public enum Outcome
        {
            /// <summary>Something changed; the rewritten JSON is returned.</summary>
            Reset,
            /// <summary>Nothing saved, or already the state of a fresh game.</summary>
            NothingToChange,
            /// <summary>Not a JSON object: left alone, and the caller warns.</summary>
            Unexpected,
        }

        /// <summary>The saved state with the loop's progress taken out, as listed on the class.
        /// <paramref name="keptBundles"/>: bundle ids whose record stays (a bought keep).</summary>
        public static Outcome Reset(string? json, IReadOnlySet<string> keptBundles, out string? rewritten)
        {
            if (keptBundles is null) throw new ArgumentNullException(nameof(keptBundles));
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
            if (state[Bundles] is JsonObject bundles)
            {
                var drop = new List<string>();
                foreach (var (id, _) in bundles)
                    if (!keptBundles.Contains(id)) drop.Add(id);
                foreach (string id in drop)
                    changed |= bundles.Remove(id);
            }
            foreach (string table in EmptiedTables)
            {
                if (state[table] is JsonObject o && o.Count > 0)
                {
                    state[table] = new JsonObject();
                    changed = true;
                }
            }
            if (!changed)
                return Outcome.NothingToChange;
            rewritten = state.ToJsonString();
            return Outcome.Reset;
        }

        /// <summary>Whether the saved state has this bundle paid for in any location.</summary>
        public static bool IsPurchased(string? json, string bundleId)
        {
            if (string.IsNullOrWhiteSpace(json) || string.IsNullOrEmpty(bundleId)) return false;
            try
            {
                if (JsonNode.Parse(json) is JsonObject state && state[Bundles] is JsonObject bundles
                    && bundles[bundleId] is JsonObject places)
                    foreach (var (_, place) in places)
                        if (place is JsonObject record && record[Purchased] is JsonValue v && v.TryGetValue(out bool paid) && paid)
                            return true;
            }
            catch (JsonException)
            {
                // Not readable: not purchased as far as a keep's reach is concerned.
            }
            return false;
        }
    }
}
