using System;
using System.Collections.Generic;
using System.Linq;
using StardewValley;
using TheLongestYear.Core;

namespace TheLongestYear.Integration
{
    /// <summary>
    /// Resolves THIS save's actual Vault (bus-repair) money-bundle indices and their gold values from
    /// the live <see cref="Game1.netWorldState"/> bundle data, instead of assuming the vanilla
    /// non-remixed layout (34–37). Remixed-bundle saves renumber every room, so the vault bundles can
    /// land at e.g. 23–26 — and the old hardcoded <see cref="VaultRules"/> indices then misclassify a
    /// real vault payment as a normal bundle completion, so <see cref="RunState.VaultBundlesPaid"/>
    /// never fills and the season gate can't be met (beta report: remixed save, vault gate stuck).
    ///
    /// Derived fresh from <c>BundleData</c> on each call (low-frequency callers: day-end reconcile +
    /// bundle-completion transitions); when the vault room is absent from the data we fall back to the
    /// canonical <see cref="VaultRules"/> values so non-remixed behaviour is unchanged.
    /// </summary>
    internal static class VaultBundleMap
    {
        private const string VaultRoomPrefix = "Vault/";
        private const string MoneyIngredientId = "-1";

        /// <summary>Map of vault bundle index → required gold for this save, derived from live bundle
        /// data. Falls back to the vanilla 34–37 layout when the data has no Vault room.</summary>
        private static IReadOnlyDictionary<int, int> GoldByIndex()
        {
            var data = Game1.netWorldState?.Value?.BundleData;
            if (data == null)
                return VanillaFallback();

            var map = new Dictionary<int, int>();
            foreach (KeyValuePair<string, string> entry in data)
            {
                // Keys are "<Room>/<index>"; we only want the Vault room.
                if (!entry.Key.StartsWith(VaultRoomPrefix, StringComparison.Ordinal))
                    continue;
                if (!int.TryParse(entry.Key.Substring(VaultRoomPrefix.Length), out int index))
                    continue;
                map[index] = ParseGold(entry.Value);
            }

            return map.Count > 0 ? map : VanillaFallback();
        }

        private static IReadOnlyDictionary<int, int> VanillaFallback()
            => VaultRules.VaultIndices.ToDictionary(i => i, VaultRules.GoldForIndex);

        /// <summary>The required gold for a money bundle, read from its ingredient field. Bundle-data
        /// format is <c>name/reward/ingredients/color/…</c> and a money bundle's ingredient list is
        /// <c>-1 &lt;amount&gt; &lt;quality&gt;</c>; we read the amount after the <c>-1</c> id.</summary>
        private static int ParseGold(string bundleValue)
        {
            string[] fields = bundleValue.Split('/');
            if (fields.Length < 3)
                return 0;
            string[] tokens = fields[2].Split(' ');
            for (int i = 0; i + 1 < tokens.Length; i++)
            {
                if (tokens[i] == MoneyIngredientId && int.TryParse(tokens[i + 1], out int gold))
                    return gold;
            }
            return 0;
        }

        /// <summary>This save's vault bundle indices, cheapest tier first.</summary>
        public static IReadOnlyList<int> Indices()
        {
            IReadOnlyDictionary<int, int> goldByIndex = GoldByIndex();
            return goldByIndex.Keys.OrderBy(i => goldByIndex[i]).ToList();
        }

        /// <summary>True if <paramref name="index"/> is a vault money bundle on this save.</summary>
        public static bool IsVaultIndex(int index) => GoldByIndex().ContainsKey(index);

        /// <summary>The gold price of a given vault bundle index on this save (0 if not a vault index).</summary>
        public static int GoldForIndex(int index)
            => GoldByIndex().TryGetValue(index, out int gold) ? gold : 0;

        /// <summary>The vault index gating a given season's checkpoint (Spring = cheapest … Winter =
        /// priciest), resolved against this save's actual indices. Used by the tly_payvault debug
        /// command; the live gate is count-based and does not use this. Returns -1 if unavailable.</summary>
        public static int IndexForSeason(Core.Season season)
        {
            IReadOnlyList<int> indices = Indices();
            int ordinal = VaultRules.SeasonOrdinal(season); // 1-based
            return ordinal >= 1 && ordinal <= indices.Count ? indices[ordinal - 1] : -1;
        }
    }
}
