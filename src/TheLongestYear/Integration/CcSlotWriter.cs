using StardewValley;
using TheLongestYear.Core;

namespace TheLongestYear.Integration
{
    /// <summary>Debug and sim writes to the vanilla per-slot bundle state. Under the mirrored ledger
    /// (spec 2026-08-29-per-slot-ledger) a ledger-only donation is wiped by the next re-read, so
    /// every simulated donation flips the board first. Slot indexes are positions in the
    /// Data/Bundles ingredient line, category slots included, matching NetBundles' bool[].</summary>
    internal static class CcSlotWriter
    {
        /// <summary>The first open concrete slot on the live board whose id matches, bundle order
        /// then slot order, in a themed (item) room. Null when the board is unavailable or nothing
        /// open wants the id.</summary>
        public static (int BundleIndex, int IngredientIndex)? FirstOpenSlotFor(string qualifiedItemId)
        {
            var worldState = Game1.netWorldState?.Value;
            if (worldState?.BundleData == null || worldState.Bundles?.FieldDict == null) return null;
            string wanted = BundleParsing.NormalizeItemId(qualifiedItemId);
            foreach (var kvp in worldState.BundleData)
            {
                ParsedBundle parsed = BundleParsing.Parse(kvp.Key, kvp.Value);
                if (!RoomThemeMap.TryGetTheme(parsed.Room, out _)) continue;
                if (!worldState.Bundles.FieldDict.ContainsKey(parsed.Index)) continue;
                bool[] state = worldState.Bundles[parsed.Index];
                for (int i = 0; i < parsed.Ingredients.Count && i < state.Length; i++)
                {
                    string itemRef = parsed.Ingredients[i].ItemRef;
                    if (BundleParsing.IsCategoryRef(itemRef)) continue;
                    if (state[i]) continue;
                    if (BundleParsing.NormalizeItemId(itemRef) == wanted) return (parsed.Index, i);
                }
            }
            return null;
        }

        /// <summary>Mark a slot complete on the board. True if it is complete afterwards (already
        /// complete counts); false when the bundle or the slot does not exist.</summary>
        public static bool TryFill(int bundleIndex, int ingredientIndex)
        {
            var worldState = Game1.netWorldState?.Value;
            if (worldState?.Bundles?.FieldDict == null) return false;
            if (!worldState.Bundles.FieldDict.ContainsKey(bundleIndex)) return false;
            bool[] arr = (bool[])worldState.Bundles[bundleIndex].Clone();
            if (ingredientIndex < 0 || ingredientIndex >= arr.Length) return false;
            if (arr[ingredientIndex]) return true;
            arr[ingredientIndex] = true;
            worldState.Bundles[bundleIndex] = arr;   // NetArray needs a whole-array assign
            return true;
        }
    }
}
