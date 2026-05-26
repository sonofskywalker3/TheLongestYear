using StardewValley;
using TheLongestYear.Core;

namespace TheLongestYear.Donations
{
    /// <summary>Resolves a (qualified) item id to a <see cref="Rarity"/> from its sale price.</summary>
    internal static class ItemRarityResolver
    {
        public static Rarity Resolve(string qualifiedItemId, RarityThresholds thresholds)
        {
            int price = 0;
            Item item = ItemRegistry.Create(qualifiedItemId, 1, 0, allowNull: true);
            if (item != null)
                price = item.salePrice();

            return RarityTiers.FromPrice(price, thresholds);
        }
    }
}
