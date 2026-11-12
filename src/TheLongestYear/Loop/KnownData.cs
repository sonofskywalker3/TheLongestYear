using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using StardewValley;
using TheLongestYear.Core.Lexicon;

namespace TheLongestYear.Loop
{
    /// <summary>The game data as the lexicon's readers see it (<see cref="GameDataPools"/>,
    /// <see cref="GameEffortData"/>, <see cref="GameObtainabilityData"/>): each asset with only the
    /// entries and rows vanilla or an installed supported mod owns (<see cref="KnownIds"/>), so content
    /// from a mod with no layer is never read (spec 2026-11-12-ignore-unsupported-mods). With no ids
    /// loaded (<see cref="Known"/> null) every asset is the game's own, unfiltered.
    /// <para>The game's objects are never changed. A filtered copy is kept per asset for as long as the
    /// game hands back the same object, so the three readers share one copy and a patched asset is
    /// filtered again.</para></summary>
    internal static class KnownData
    {
        /// <summary>The id family of loaded maps (<c>Game1.locations</c>), which are not an asset.</summary>
        public const string LocationsAsset = "Game/Locations";

        private static readonly ConditionalWeakTable<object, object> Filtered = new();
        private static readonly Dictionary<string, Func<object>> SeenAssets = new(StringComparer.Ordinal);
        private static readonly HashSet<string> DroppedIds = new(StringComparer.Ordinal);

        /// <summary>The ids in force for this session, or null for no filtering. Setting it forgets every copy.</summary>
        public static KnownIds Known
        {
            get => _known;
            set
            {
                _known = value;
                Filtered.Clear();
                DroppedIds.Clear();
            }
        }
        private static KnownIds _known;

        /// <summary>Set while a layer or ids file is being written: the readers must see everything.</summary>
        public static bool Unfiltered { get; set; }

        /// <summary>Every asset a reader has loaded through here this session (what <c>tly_lexicon ids</c> walks).</summary>
        public static IReadOnlyCollection<string> Assets => SeenAssets.Keys;

        /// <summary>Every id left out since <see cref="Known"/> was set, for the log.</summary>
        public static IReadOnlyCollection<string> Dropped => DroppedIds;

        public static T Load<T>(string asset) where T : class
        {
            T raw = Game1.content.Load<T>(asset);
            SeenAssets[asset] = () => Game1.content.Load<T>(asset);
            if (_known == null || Unfiltered || raw == null) return raw;
            if (Filtered.TryGetValue(raw, out object had)) return (T)had;
            T kept = AssetIds.Filter(asset, raw, _known, id => DroppedIds.Add(id));
            Filtered.Add(raw, kept);
            return kept;
        }

        private const int MaxOwnersInLine = 12;
        private static readonly char[] Digits = "0123456789".ToCharArray();
        private static readonly char[] OwnerEnd = { '.', '_' };

        /// <summary>One line for the log: how many entries were left out, and whose they look like
        /// (the part of each key before its first "." or "_", a guess: the game does not record which
        /// mod added an entry).</summary>
        public static string DroppedLine()
        {
            var owners = DroppedIds
                .Select(id => id.Split(KnownIds.Separator))
                .Select(parts => (parts.Length > 2 ? parts[2].Split(KnownIds.PathSeparator).Last() : parts.Length > 1 ? parts[1] : "").TrimStart('('))
                .Select(key => key.IndexOfAny(OwnerEnd) is int cut and > 0 ? key[..cut] : key)
                .Where(owner => owner.Length > 0)
                .GroupBy(owner => owner, StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.Ordinal)
                .Select(g => g.Key).ToList();
            string names = string.Join(", ", owners.Take(MaxOwnersInLine)) + (owners.Count > MaxOwnersInLine ? ", ..." : "");
            return $"Lexicon: ignored {DroppedIds.Count:N0} data entries from mods The Longest Year has no support for ({names}). "
                + "Their items are not asked for and their sources are not counted.";
        }

        /// <summary>The rows of one list of a known entry that <see cref="Load{T}"/> left out: what a
        /// mod with no layer added there. Empty when nothing is filtered.</summary>
        public static IEnumerable<T> UnknownRows<T>(string asset, string key, string list, IEnumerable<T> rawRows)
        {
            if (_known == null || Unfiltered || rawRows == null) yield break;
            string entryId = KnownIds.EntryId(asset, key);
            int index = 0;
            foreach (T row in rawRows)
                if (!_known.Has(KnownIds.RowId(entryId, list + KnownIds.PathSeparator + AssetIds.RowKey(row, index++))))
                    yield return row;
        }

        /// <summary>The game's own asset, unfiltered, for reading what <see cref="UnknownRows{T}"/> compares.</summary>
        public static T Raw<T>(string asset) where T : class => Game1.content.Load<T>(asset);

        private const string ObjectsAsset = "Data/Objects";
        private const string ObjectQualifier = "(O)";

        /// <summary>Whether an item is vanilla's or an installed supported mod's. Only objects are
        /// judged (the readers load no other item table); anything else, and a category, is known.</summary>
        public static bool KnowsItem(string qualifiedId)
        {
            if (_known == null || Unfiltered || string.IsNullOrEmpty(qualifiedId)) return true;
            if (!qualifiedId.StartsWith(ObjectQualifier, StringComparison.Ordinal)) return true;
            return _known.HasEntry(ObjectsAsset, qualifiedId[ObjectQualifier.Length..]);
        }

        /// <summary>The loaded maps the readers walk: the known ones.</summary>
        public static IEnumerable<GameLocation> Locations()
            => Game1.locations.Where(l => l?.Name != null && KnowsLocation(l.Name));

        public static bool KnowsLocation(string name)
        {
            if (_known == null || Unfiltered || _known.HasEntry(LocationsAsset, name)) return true;
            // A save's numbered copies of a known map (Cellar2 to Cellar8 for cabins) are that map.
            string stem = name.TrimEnd(Digits);
            if (stem.Length > 0 && stem.Length < name.Length && _known.HasEntry(LocationsAsset, stem)) return true;
            DroppedIds.Add(KnownIds.EntryId(LocationsAsset, name));
            return false;
        }

        /// <summary>Every id in the live game for the assets the readers load, and the loaded maps.</summary>
        public static IReadOnlyList<string> LiveIds()
        {
            var ids = new List<string>();
            foreach (var (asset, load) in SeenAssets.OrderBy(a => a.Key, StringComparer.Ordinal))
                ids.AddRange(AssetIds.Ids(asset, load()));
            ids.AddRange(Game1.locations.Where(l => l?.Name != null).Select(l => KnownIds.EntryId(LocationsAsset, l.Name)));
            return ids;
        }
    }
}
