using System;
using System.Collections.Generic;
using System.Linq;
using StardewModdingAPI;
using StardewValley;
using StardewValley.GameData.Bundles;
using TheLongestYear.Core;

namespace TheLongestYear.Loop
{
    /// <summary>
    /// Builds per-room, per-position bundle CANDIDATE pools from the game's own
    /// <c>Data/Bundles</c> (the standard set — every entry is candidate #0 of its position) and
    /// <c>Data/RandomBundles</c> (every matching per-position variant appended as a further
    /// candidate). <see cref="RemixSelector"/> then picks one candidate per position.
    ///
    /// This is pure read-side glue over live game data — no writes, no RunActivation gate needed
    /// here (callers, e.g. <see cref="BundleEngine"/> and whatever schedules a (re)generation,
    /// own the dormant-per-save gate; see MEMORY tly-dormant-per-save-gate-runactivation).
    ///
    /// Data model verified against the decompile
    /// (StardewValley.GameData.Bundles.RandomBundleData/BundleSetData/BundleData) and against
    /// vanilla's own consumer, <c>BundleGenerator.Generate</c> (StardewValley/BundleGenerator.cs):
    ///   - <c>Data/Bundles</c> is Dictionary&lt;string,string&gt; keyed "Room/index" — same
    ///     slash-delimited value format <see cref="BundleParsing"/> already parses.
    ///   - <c>Data/RandomBundles</c> is List&lt;RandomBundleData&gt;; one entry per room
    ///     (<c>AreaName</c> matches the room's <c>Data/Bundles</c> key prefix EXACTLY — that's how
    ///     vanilla's own generator overwrites the right key). Each entry carries <c>BundleSets</c>
    ///     (whole alternate sets, matched by position) and a flat <c>Bundles</c> pool (also
    ///     matched by position, or Index == -1 for "any position").
    ///   - Within a <c>BundleData</c> entry, <c>Index</c> is a POSITION marker (0-based, within
    ///     the room) — NOT the final absolute bundle index. <c>Items</c> is a comma-separated list
    ///     of "stack [QQ] itemIdOrName" entries (space-separated); <c>Pick</c>/<c>RequiredItems</c>
    ///     of -1 mean "all" (mirrored from <c>ParseItemList</c>'s own -1 fallback chain).
    ///
    /// Simplifications vs. vanilla's own <c>BundleGenerator</c> (documented, not required for
    /// glue fidelity — see task-5-report.md):
    ///   - Every <c>BundleSets</c> entry's per-position bundle is folded in as ONE MORE candidate
    ///     for that position (vanilla instead randomly commits to a single whole SET). Our own
    ///     picker is <see cref="RemixSelector"/>, which already makes the per-position choice, so
    ///     there is no need to also pre-select a whole set here — this only widens the pool.
    ///   - <c>Items</c>' <c>Pick</c> field is NOT used to randomly trim the ingredient list (that
    ///     trim has no seed available at pool-build time); every parsed ingredient becomes a slot
    ///     and <c>RequiredItems</c> (falling back to <c>Pick</c>, then to the full count) becomes
    ///     <see cref="BundleSpec.NumberOfSlots"/> — i.e. the bundle may show MORE candidate items
    ///     than vanilla's own remix would, while still requiring the same number donated.
    ///   - "[a|b|c]" random-tag brackets in <c>Items</c> deterministically resolve to their FIRST
    ///     option (no RNG available here either) instead of vanilla's random pick.
    /// </summary>
    internal sealed class VanillaBundlePool
    {
        private const string DataBundlesAssetName = "Data/Bundles";
        private const string DataRandomBundlesAssetName = "Data/RandomBundles";

        private const int QualityNormal = 0;
        private const int QualitySilver = 1;
        private const int QualityGold = 2;
        private const int QualityIridium = 4; // project convention (BundleSlotSpec doc) — vanilla's
                                               // own ParseItemString mismaps "IQ" to 3; we keep 4 for
                                               // consistency with QualityTags/BundleRequirement.

        private readonly IMonitor _monitor;

        public VanillaBundlePool(IMonitor monitor)
        {
            _monitor = monitor;
        }

        /// <summary>Per room: position-indexed candidate pools (position 0..n-1, count fixed by
        /// how many standard bundles <c>Data/Bundles</c> defines for that room).</summary>
        public IReadOnlyDictionary<string, IReadOnlyList<IReadOnlyList<BundleSpec>>> BuildRoomPools()
        {
            Dictionary<string, string> standard = Game1.content.Load<Dictionary<string, string>>(DataBundlesAssetName);
            List<RandomBundleData> random = Game1.content.Load<List<RandomBundleData>>(DataRandomBundlesAssetName);

            // room -> (original absolute index -> that position's growing candidate list), ordered
            // ascending by absolute index so position order matches vanilla's own room ordering.
            var perRoom = new Dictionary<string, SortedDictionary<int, List<BundleSpec>>>(StringComparer.Ordinal);

            foreach (KeyValuePair<string, string> entry in standard)
            {
                BundleSpec spec = SpecFromStandardEntry(entry.Key, entry.Value);
                if (!perRoom.TryGetValue(spec.Room, out SortedDictionary<int, List<BundleSpec>> byIndex))
                    perRoom[spec.Room] = byIndex = new SortedDictionary<int, List<BundleSpec>>();
                if (!byIndex.TryGetValue(spec.Index, out List<BundleSpec> candidates))
                    byIndex[spec.Index] = candidates = new List<BundleSpec>();
                candidates.Add(spec); // candidate #0 of this position
            }

            foreach (RandomBundleData areaData in random)
            {
                if (areaData?.AreaName == null) continue;
                if (!perRoom.TryGetValue(areaData.AreaName, out SortedDictionary<int, List<BundleSpec>> byIndex))
                    continue; // no matching standard-set positions for this area — nothing to widen

                List<int> positionAbsoluteIndex = byIndex.Keys.ToList(); // ascending; position j -> absolute index

                foreach (BundleSetData set in areaData.BundleSets ?? Enumerable.Empty<BundleSetData>())
                    foreach (BundleData bundle in set?.Bundles ?? Enumerable.Empty<BundleData>())
                        AddCandidateAtPosition(bundle, areaData.AreaName, byIndex, positionAbsoluteIndex);

                foreach (BundleData bundle in areaData.Bundles ?? Enumerable.Empty<BundleData>())
                {
                    if (bundle.Index == -1)
                    {
                        // Wildcard: usable at ANY position. Vanilla consumes it for exactly one
                        // empty slot; we widen every position's pool instead (documented above).
                        for (int position = 0; position < positionAbsoluteIndex.Count; position++)
                            AddCandidateAtAbsoluteIndex(bundle, areaData.AreaName, positionAbsoluteIndex[position], byIndex);
                    }
                    else
                    {
                        AddCandidateAtPosition(bundle, areaData.AreaName, byIndex, positionAbsoluteIndex);
                    }
                }
            }

            var result = new Dictionary<string, IReadOnlyList<IReadOnlyList<BundleSpec>>>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, SortedDictionary<int, List<BundleSpec>>> roomEntry in perRoom)
            {
                var positions = new List<IReadOnlyList<BundleSpec>>();
                int position = 0;
                foreach (KeyValuePair<int, List<BundleSpec>> posEntry in roomEntry.Value)
                {
                    if (posEntry.Value.Count == 0)
                    {
                        _monitor?.Log(
                            $"VanillaBundlePool: room '{roomEntry.Key}' position {position} (index {posEntry.Key}) " +
                            "has no candidates -- skipped.",
                            LogLevel.Warn);
                        position++;
                        continue;
                    }
                    positions.Add(posEntry.Value);
                    position++;
                }
                if (positions.Count > 0)
                    result[roomEntry.Key] = positions;
            }
            return result;
        }

        private void AddCandidateAtPosition(
            BundleData bundle, string room,
            SortedDictionary<int, List<BundleSpec>> byIndex, List<int> positionAbsoluteIndex)
        {
            if (bundle == null || bundle.Index < 0 || bundle.Index >= positionAbsoluteIndex.Count)
                return;
            AddCandidateAtAbsoluteIndex(bundle, room, positionAbsoluteIndex[bundle.Index], byIndex);
        }

        private void AddCandidateAtAbsoluteIndex(
            BundleData bundle, string room, int absoluteIndex, SortedDictionary<int, List<BundleSpec>> byIndex)
        {
            BundleSpec spec = SpecFromRandomEntry(room, absoluteIndex, bundle);
            if (spec == null)
                return; // unresolvable ingredients -- logged in SpecFromRandomEntry, skip this candidate only
            if (!byIndex.TryGetValue(absoluteIndex, out List<BundleSpec> candidates))
                byIndex[absoluteIndex] = candidates = new List<BundleSpec>();
            candidates.Add(spec);
        }

        private static BundleSpec SpecFromStandardEntry(string key, string value)
        {
            ParsedBundle parsed = BundleParsing.Parse(key, value);
            string[] fields = value.Split('/');
            string reward = fields.Length > 1 ? fields[1] : "";
            int color = fields.Length > 3 && int.TryParse(fields[3], out int c) ? c : 0;
            string displayName = fields.Length > 6 && !string.IsNullOrEmpty(fields[6]) ? fields[6] : parsed.Name;

            List<BundleSlotSpec> slots = parsed.Ingredients
                .Select(ing => new BundleSlotSpec(ing.ItemRef, ing.Stack, ing.Quality))
                .ToList();

            return new BundleSpec(parsed.Room, parsed.Index, parsed.Name, displayName, reward, color, parsed.NumberOfSlots, slots);
        }

        private BundleSpec SpecFromRandomEntry(string room, int index, BundleData bundle)
        {
            IReadOnlyList<BundleSlotSpec> slots = ParseRandomItems(bundle.Items, room, bundle.Name);
            if (slots.Count == 0)
            {
                _monitor?.Log(
                    $"VanillaBundlePool: room '{room}' candidate '{bundle.Name}' resolved zero usable " +
                    "ingredients -- skipped.",
                    LogLevel.Trace);
                return null;
            }

            int color = ColorNameToIndex(bundle.Color);
            int numberOfSlots = bundle.RequiredItems >= 0 ? bundle.RequiredItems
                : bundle.Pick >= 0 ? bundle.Pick
                : slots.Count;
            string reward = ResolveReward(bundle.Reward);

            return new BundleSpec(room, index, bundle.Name, bundle.Name, reward, color, numberOfSlots, slots);
        }

        private IReadOnlyList<BundleSlotSpec> ParseRandomItems(string itemsField, string room, string bundleName)
        {
            var result = new List<BundleSlotSpec>();
            if (string.IsNullOrWhiteSpace(itemsField))
                return result;

            string resolved = ResolveRandomTags(itemsField);
            foreach (string rawEntry in resolved.Split(','))
            {
                string entryText = rawEntry.Trim();
                if (entryText.Length == 0)
                    continue;

                string[] parts = entryText.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2 || !int.TryParse(parts[0], out int stack))
                {
                    _monitor?.Log(
                        $"VanillaBundlePool: room '{room}' candidate '{bundleName}' -- malformed item " +
                        $"entry '{entryText}', skipping that ingredient.",
                        LogLevel.Trace);
                    continue;
                }

                int fieldIndex = 1;
                int quality = QualityNormal;
                switch (parts[fieldIndex])
                {
                    case "NQ": quality = QualityNormal; fieldIndex++; break;
                    case "SQ": quality = QualitySilver; fieldIndex++; break;
                    case "GQ": quality = QualityGold; fieldIndex++; break;
                    case "IQ": quality = QualityIridium; fieldIndex++; break;
                }

                string itemText = string.Join(" ", parts.Skip(fieldIndex));
                string itemId = ResolveItemId(itemText, stack);
                if (itemId == null)
                {
                    _monitor?.Log(
                        $"VanillaBundlePool: room '{room}' candidate '{bundleName}' -- couldn't resolve " +
                        $"item text '{itemText}', skipping that ingredient.",
                        LogLevel.Trace);
                    continue;
                }

                result.Add(new BundleSlotSpec(itemId, stack, quality));
            }
            return result;
        }

        /// <summary>Deterministically resolves "[a|b|c]" random-tag groups to their FIRST option
        /// (no RNG is available at pool-build time — see class doc).</summary>
        private static string ResolveRandomTags(string data)
        {
            int openIndex;
            while ((openIndex = data.LastIndexOf('[')) >= 0)
            {
                int closeIndex = data.IndexOf(']', openIndex);
                if (closeIndex == -1)
                    break;
                string inner = data.Substring(openIndex + 1, closeIndex - openIndex - 1);
                string chosen = inner.Split('|')[0];
                data = data.Remove(openIndex, closeIndex - openIndex + 1).Insert(openIndex, chosen);
            }
            return data;
        }

        private static string ResolveItemId(string itemText, int stack)
        {
            if (string.IsNullOrEmpty(itemText))
                return null;

            if (char.IsDigit(itemText[0]))
            {
                Item item = ItemRegistry.Create("(O)" + itemText, stack, allowNull: true);
                return item?.ItemId;
            }

            if (itemText.EndsWith("category", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    System.Reflection.FieldInfo field = typeof(StardewValley.Object).GetField(itemText);
                    if (field != null)
                        return ((int)field.GetValue(null)).ToString();
                }
                catch (Exception)
                {
                    // Fall through to fuzzy search below.
                }
            }

            try
            {
                Item item = Utility.fuzzyItemSearch(itemText, stack);
                return item?.ItemId;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static string ResolveReward(string rawReward)
        {
            if (string.IsNullOrEmpty(rawReward) || !char.IsDigit(rawReward[0]))
                return rawReward ?? "";

            try
            {
                string[] parts = rawReward.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2 || !int.TryParse(parts[0], out int stackCount))
                    return rawReward;

                string query = string.Join(" ", parts.Skip(1));
                Item item = Utility.fuzzyItemSearch(query, stackCount);
                if (item == null)
                    return rawReward;

                // Mirrors vanilla's own BundleGenerator.Generate() reward resolution exactly
                // (StardewValley/BundleGenerator.cs) -- it's the only function that turns a
                // resolved Item back into the Data/Bundles reward-string format, and it's the
                // same obsolete-but-still-load-bearing call vanilla itself makes here.
#pragma warning disable CS0618 // Utility.getStandardDescriptionFromItem is Obsolete
                return Utility.getStandardDescriptionFromItem(item, item.Stack);
#pragma warning restore CS0618
            }
            catch (Exception)
            {
                return rawReward;
            }
        }

        /// <summary>Mirrors BundleGenerator's own Color-name switch (StardewValley/BundleGenerator.cs).</summary>
        private static int ColorNameToIndex(string colorName)
        {
            switch (colorName)
            {
                case "Red": return 4;
                case "Blue": return 5;
                case "Green": return 0;
                case "Orange": return 2;
                case "Purple": return 1;
                case "Teal": return 6;
                case "Yellow": return 3;
                default: return 0;
            }
        }
    }
}
