using System.Collections.Generic;
using StardewValley;
using StardewValley.GameData.Objects;
using StardewValley.Locations;
using TheLongestYear.Core;
using TheLongestYear.Donations;

namespace TheLongestYear.Integration
{
    /// <summary>Reads the live board and the game's object data into the Core types the loop
    /// summary is built from (spec 2026-11-10 shrine planning, amendment A). Called once, before
    /// the reset, while the old board and its completion arrays are still in place.</summary>
    internal static class OutgoingLoopReader
    {
        /// <summary>Every bundle of the board with its live progress; empty when no board is loaded.</summary>
        public static IReadOnlyList<BundleProgress> ReadBoard(RunState run)
        {
            Dictionary<string, string> data = Game1.netWorldState?.Value?.BundleData;
            if (data == null || Game1.getLocationFromName("CommunityCenter") is not CommunityCenter cc)
                return new List<BundleProgress>();
            Dictionary<int, bool[]> slots = cc.bundlesDict();
            return BoardProgress.Read(data, index => slots.TryGetValue(index, out bool[] s) ? s : null, run.VaultBundlesPaid);
        }

        public static AskKind KindOf(string itemRef) => AskKinds.Classify(itemRef, ObjectEntry);

        /// <summary>Rare or very rare by the same price bands the donation JP uses.</summary>
        public static bool IsRareFish(string qualifiedId, RarityThresholds thresholds)
            => ItemRarityResolver.Resolve(qualifiedId, thresholds) >= Rarity.Rare;

        /// <summary>The game's display name of an item, for the reasons that name it (Garlic, Red Cabbage).</summary>
        public static string DisplayName(string qualifiedId)
            => ItemRegistry.GetData(qualifiedId)?.DisplayName ?? qualifiedId;

        private static RawObjectEntry ObjectEntry(string bareId)
        {
            if (bareId == null || Game1.objectData == null || !Game1.objectData.TryGetValue(bareId, out ObjectData o) || o == null)
                return null;
            return new RawObjectEntry(o.Type ?? "", o.Category, o.Price, o.ExcludeFromRandomSale,
                (IReadOnlyList<string>)(o.ContextTags ?? new List<string>()), o.Name ?? "");
        }
    }
}
