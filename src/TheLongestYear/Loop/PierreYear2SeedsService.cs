using System;
using System.Text.RegularExpressions;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley.GameData.Shops;

namespace TheLongestYear.Loop
{
    /// <summary>
    /// Obtainability upgrade <c>pierre_year2_seeds</c> (user ruling 2026-08-21, 10,000 JP): Pierre
    /// stocks his year-2-only seeds — Garlic (476, Spring), Red Cabbage (485, Summer), Artichoke
    /// (489, Fall) — from year 1. Implemented as a Data/Shops edit that strips the <c>YEAR n</c>
    /// clause from those three SeedShop entries' <c>Condition</c> while the upgrade is owned; the
    /// season clause (and everything else) is left intact, so the seeds still appear only in
    /// their own season. Robust to the exact vanilla condition text.
    ///
    /// Cache discipline: ownership is per save, so ModEntry invalidates Data/Shops after a TLY
    /// save loads, after the upgrade is purchased, and on return-to-title (UpgradeChecker is
    /// null there → no edit).
    /// </summary>
    internal sealed class PierreYear2SeedsService
    {
        public const string UpgradeId = "pierre_year2_seeds";
        public const string ShopAssetName = "Data/Shops";
        private const string SeedShopId = "SeedShop";
        private static readonly string[] Year2SeedIds = { "(O)476", "(O)485", "(O)489" };
        private static readonly Regex YearClause = new Regex(
            @"\s*,?\s*\bYEAR\s+\d+\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private readonly IMonitor _monitor;

        public PierreYear2SeedsService(IMonitor monitor) => _monitor = monitor;

        public static bool Owned => UpgradeChecker.HasUpgrade?.Invoke(UpgradeId) == true;

        public void OnAssetRequested(object sender, AssetRequestedEventArgs e)
        {
            if (!e.NameWithoutLocale.IsEquivalentTo(ShopAssetName)) return;
            if (!Owned) return;

            e.Edit(asset =>
            {
                var shops = asset.AsDictionary<string, ShopData>().Data;
                if (!shops.TryGetValue(SeedShopId, out ShopData seedShop) || seedShop?.Items == null)
                {
                    _monitor.Log("PierreYear2Seeds: SeedShop not found in Data/Shops — nothing edited.", LogLevel.Warn);
                    return;
                }

                int edited = 0;
                foreach (ShopItemData item in seedShop.Items)
                {
                    if (item == null || string.IsNullOrEmpty(item.Condition)) continue;
                    if (Array.IndexOf(Year2SeedIds, item.ItemId ?? "") < 0
                        && Array.IndexOf(Year2SeedIds, item.Id ?? "") < 0) continue;

                    string stripped = StripYearClause(item.Condition);
                    if (stripped == item.Condition) continue;
                    _monitor.Log($"PierreYear2Seeds: {item.ItemId} condition '{item.Condition}' -> '{stripped}'.", LogLevel.Trace);
                    item.Condition = stripped;
                    edited++;
                }
                _monitor.Log($"PierreYear2Seeds: unlocked {edited} year-2 seed line(s) at Pierre's for year 1.", LogLevel.Info);
            }, AssetEditPriority.Late);
        }

        /// <summary>Remove every <c>YEAR n</c> clause from a comma-separated game-state query;
        /// returns null when nothing else remains (null condition = always available).</summary>
        internal static string StripYearClause(string condition)
        {
            if (string.IsNullOrWhiteSpace(condition)) return condition;
            string result = YearClause.Replace(condition, "").Trim().Trim(',').Trim();
            return result.Length == 0 ? null : result;
        }
    }
}
