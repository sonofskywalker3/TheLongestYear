using System;
using System.Collections.Generic;
using StardewValley;
using TheLongestYear.Integration;

namespace TheLongestYear.UI
{
    /// <summary>The item drawn as a focus card's icon. A tool card shows the tool at the card's
    /// tier; every other chain has one fixed item. Returns null (draw nothing) for an unknown chain
    /// or an item the registry cannot build.</summary>
    internal static class FocusIcons
    {
        private const string RodChain = "fishing_rod";
        private const int FirstRung = 1;

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
                return ToolTierItems.ItemIdFor(chainId, ToolTierItems.RodLevelForRung(targetRung));
            if (ToolTierItems.IsMetalSlug(chainId))
                return ToolTierItems.ItemIdFor(chainId, Math.Clamp(targetRung, FirstRung, ToolTierItems.MaxMetalLevel));
            return ChainItemIds.TryGetValue(chainId, out string itemId) ? itemId : null;
        }
    }
}
