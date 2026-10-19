using System;
using System.Collections.Generic;
using StardewValley;

namespace TheLongestYear.UI
{
    /// <summary>The item drawn as a focus card's icon. A tool card shows the tool at the card's
    /// tier; every other chain has one fixed item. Returns null (draw nothing) for an unknown chain
    /// or an item the registry cannot build.</summary>
    internal static class FocusIcons
    {
        private const string RodChain = "fishing_rod";
        private const int FirstRung = 1;

        private static readonly string[] MetalToolPrefixes = { "Copper", "Steel", "Gold", "Iridium" };
        private static readonly string[] RodItemIds = { "BambooPole", "FiberglassRod", "IridiumRod" };

        private static readonly IReadOnlyDictionary<string, string> MetalToolBaseIds =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["hoe"] = "Hoe",
                ["pickaxe"] = "Pickaxe",
                ["axe"] = "Axe",
                ["watering_can"] = "WateringCan",
            };

        private static readonly IReadOnlyDictionary<string, string> ChainItemIds =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["coop"] = "(O)176",
                ["barn"] = "(O)184",
                ["silo"] = "(O)178",
                ["fish_pond"] = "(O)145",
                ["house"] = "(O)194",
                ["shortcuts"] = "(O)388",
                ["green_thumb"] = "(O)770",
                ["coal_vein"] = "(O)382",
                ["foragers_eye"] = "(O)16",
                ["quick_bite"] = "(O)685",
                ["weather_sage"] = "(O)766",
                ["cart_slot"] = "(O)347",
                ["stash"] = "(BC)130",
                ["backpack"] = "(BC)130",
                ["jp_boost"] = "(O)74",
                ["shop_discount"] = "(O)388",
            };

        /// <summary>The icon for a chain. <paramref name="targetRung"/> picks the tool tier for a
        /// tool chain (rung 1 is the first upgrade) and is ignored for every other chain.</summary>
        public static Item For(string chainId, int targetRung = FirstRung)
        {
            string id = QualifiedId(chainId, targetRung);
            return id == null ? null : ItemRegistry.Create(id, allowNull: true);
        }

        private static string QualifiedId(string chainId, int targetRung)
        {
            if (chainId == null) return null;
            if (chainId == RodChain)
                return "(T)" + RodItemIds[Math.Clamp(targetRung, FirstRung, RodItemIds.Length) - 1];
            if (MetalToolBaseIds.TryGetValue(chainId, out string baseId))
                return "(T)" + MetalToolPrefixes[Math.Clamp(targetRung, FirstRung, MetalToolPrefixes.Length) - 1] + baseId;
            return ChainItemIds.TryGetValue(chainId, out string itemId) ? itemId : null;
        }
    }
}
