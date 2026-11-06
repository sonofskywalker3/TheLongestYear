using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using StardewModdingAPI;
using StardewValley;
using TheLongestYear.Core;
using TheLongestYear.Core.Obtainability;

namespace TheLongestYear.Integration
{
    /// <summary>
    /// Reads the pool of seeds Item Extensions (mistyspring.ItemExtensions) keeps for Mixed Seeds: the
    /// data asset <c>Mods/mistyspring.ItemExtensions/MixedSeeds</c>, key "770", which other mods fill
    /// (Cornucopia More Crops adds six seeds a season). The asset is loaded through SMAPI as that mod
    /// loads it, with that mod's own row type made by reflection, so nothing of it is referenced or
    /// copied, and the read does not wait on that mod's own save-loaded parse.
    /// <para>
    /// The result is game data like any other row the lexicon hashes: each entry's seed, Condition and
    /// Weight, then one row for the bag itself, whose weight is how many slots the vanilla roll keeps
    /// (that mod's AddMainSeedBy). That mod's own filters are applied as it applies them
    /// (Additions/Parser.cs MixedSeeds): an entry whose HasMod names a mod that is not installed is
    /// left out, and a list holding an entry with no ItemId is dropped whole. Empty when the mod is
    /// not installed, its Mixed Seeds switch is off, or the pool holds nothing for the bag: then no
    /// row is added and nothing changes. Fail open: a read that throws is logged once and gives the
    /// empty pool.
    /// </para>
    /// </summary>
    internal static class ItemExtensionsMixedSeeds
    {
        public const string ModId = "mistyspring.ItemExtensions";
        private const string EntryType = "ItemExtensions.ModEntry";
        private const string RowType = "ItemExtensions.Models.Contained.MixedSeedData";
        private const string AssetName = "Mods/" + ModId + "/MixedSeeds";
        private const string ConfigProperty = "Config";
        private const string SwitchProperty = "MixedSeeds";
        /// <summary>Data/Objects CustomFields key: a space-separated seed list, read when the asset holds nothing for the bag.</summary>
        private const string SeedListField = ModId + "/MixedSeeds";
        /// <summary>Data/Objects CustomFields key: how many slots the bag itself takes.</summary>
        private const string OwnSlotsField = ModId + "/AddMainSeed";
        private const string CropsAnytimeModId = "Pathoschild.CropsAnytimeAnywhere";
        private const char KeyMarker = '$';
        private const char QualifierMarker = '(';
        private const int DefaultWeight = 1;

        private static readonly IReadOnlyList<MixedSeedRow> None = Array.Empty<MixedSeedRow>();
        private static IModHelper _helper;
        private static bool _warned;

        public static void Register(IModHelper helper) => _helper = helper;

        public static IReadOnlyList<MixedSeedRow> Read(IMonitor monitor)
        {
            if (_helper == null || !_helper.ModRegistry.IsLoaded(ModId)) return None;
            try
            {
                Type entry = AccessTools.TypeByName(EntryType);
                Type rowType = AccessTools.TypeByName(RowType);
                if (entry == null || rowType == null) return None;
                object config = AccessTools.Property(entry, ConfigProperty)?.GetValue(null);
                if (config != null && AccessTools.Property(config.GetType(), SwitchProperty)?.GetValue(config) is false)
                    return None;

                var rows = new List<MixedSeedRow>();
                bool fromAsset = false;
                Type assetType = typeof(Dictionary<,>).MakeGenericType(typeof(string), typeof(List<>).MakeGenericType(rowType));
                MethodInfo load = typeof(IGameContentHelper).GetMethods()
                    .First(m => m.Name == nameof(IGameContentHelper.Load) && m.IsGenericMethodDefinition
                        && m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType == typeof(string))
                    .MakeGenericMethod(assetType);
                if (load.Invoke(_helper.GameContent, new object[] { AssetName }) is IDictionary seeds
                    && seeds.Contains(MixedSeedsOutcome.MixedSeedsId) && seeds[MixedSeedsOutcome.MixedSeedsId] is IEnumerable entries)
                {
                    fromAsset = true;
                    foreach (object seed in entries)
                    {
                        if (seed == null) continue;
                        string id = AccessTools.Property(rowType, "ItemId")?.GetValue(seed) as string;
                        if (string.IsNullOrWhiteSpace(id)) { rows.Clear(); fromAsset = false; break; }   // that mod drops the whole list
                        string hasMod = AccessTools.Property(rowType, "HasMod")?.GetValue(seed) as string;
                        if (!string.IsNullOrWhiteSpace(hasMod) && !_helper.ModRegistry.IsLoaded(hasMod)) continue;
                        // That mod looks the id up in Data/Crops as written, so a qualified id is never picked.
                        if (id[0] == QualifierMarker) continue;
                        string condition = AccessTools.Property(rowType, "Condition")?.GetValue(seed) as string;
                        int weight = AccessTools.Property(rowType, "Weight")?.GetValue(seed) is int w ? w : DefaultWeight;
                        rows.Add(new MixedSeedRow(SeedId(id), string.IsNullOrWhiteSpace(condition) ? null : condition, weight));
                    }
                }

                IDictionary<string, string> fields =
                    Game1.objectData != null && Game1.objectData.TryGetValue(MixedSeedsOutcome.MixedSeedsId, out var bag) ? bag?.CustomFields : null;
                // With nothing in the asset for the bag, that mod reads a seed list from the bag's own data.
                if (!fromAsset && fields != null && fields.TryGetValue(SeedListField, out string listed) && !string.IsNullOrWhiteSpace(listed))
                    foreach (string id in ArgUtility.SplitBySpace(listed))
                        if (!string.IsNullOrWhiteSpace(id) && id[0] != QualifierMarker) rows.Add(new MixedSeedRow(SeedId(id), null, DefaultWeight));

                if (rows.Count == 0) return None;
                if (_helper.ModRegistry.IsLoaded(CropsAnytimeModId)) rows.Add(new MixedSeedRow(MixedSeedPool.AnySeasonKey, null, 0));
                rows.Add(new MixedSeedRow(MixedSeedPool.MixedSeedsQualifiedId, null, OwnSlots(fields)));
                return rows;
            }
            catch (Exception ex) when (ex is TargetInvocationException or InvalidCastException or AmbiguousMatchException
                                           or FormatException or ArgumentException or InvalidOperationException)
            {
                if (!_warned)
                    monitor?.Log($"Item Extensions' Mixed Seeds pool could not be read ({ex.GetType().Name}: {(ex.InnerException ?? ex).Message}); Mixed Seeds are read as vanilla's.", LogLevel.Warn);
                _warned = true;
                return None;
            }
        }

        /// <summary>That mod's AddMainSeedBy for a pool that holds entries: one slot when the bag's data
        /// carries no custom fields, else the count its field names, else none.</summary>
        private static int OwnSlots(IDictionary<string, string> fields)
        {
            if (fields == null || fields.Count == 0) return 1;
            return fields.TryGetValue(OwnSlotsField, out string count) ? Math.Max(0, int.Parse(count, System.Globalization.CultureInfo.InvariantCulture)) : 0;
        }

        private static string SeedId(string id)
            => id[0] == KeyMarker ? id : BundleParsing.NormalizeItemId(id);
    }
}
