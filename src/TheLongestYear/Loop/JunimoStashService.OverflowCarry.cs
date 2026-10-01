using System.Collections.Generic;
using System.Linq;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Objects;

namespace TheLongestYear.Loop
{
    internal sealed partial class JunimoStashService
    {
        /// <summary>Before loadForNewGame discards the farm (step 0h): take the items out of every
        /// overflow chest on it, so they are not wiped with the farm. Held in memory and handed back
        /// by <see cref="ReturnOverflowCarry"/>. A chest that throws is logged and skipped.</summary>
        internal List<Item> LiftOverflowChests()
        {
            var lifted = new List<Item>();
            Farm farm = Game1.getFarm();
            if (farm == null)
                return lifted;
            foreach (var pair in farm.objects.Pairs.ToList())
            {
                if (pair.Value is not Chest chest || !chest.modData.ContainsKey(OverflowModDataKey))
                    continue;
                List<Item> items = chest.Items.Where(i => i != null).ToList();
                try
                {
                    chest.Items.Clear();
                    lifted.AddRange(items);
                }
                catch (System.Exception ex)
                {
                    // Exception on purpose: a mod's inventory hook. Carry whatever already left the
                    // chest; what is still in it stays with the chest.
                    lifted.AddRange(items.Where(i => !chest.Items.Contains(i)));
                    _monitor.Log($"JunimoStashService: emptying the overflow chest at ({pair.Key.X}, {pair.Key.Y}) before the rewind threw. {ex.GetType().Name}: {ex.Message}", LogLevel.Warn);
                }
            }
            if (lifted.Count > 0)
                _monitor.Log($"JunimoStashService: carried {lifted.Count} item(s) out of the overflow chest(s) for the rewind.", LogLevel.Info);
            return lifted;
        }

        /// <summary>After the stash and kept decor are back (step 13b): the carried overflow items go
        /// into the stash where they fit, the rest into a fresh overflow chest beside it.</summary>
        internal void ReturnOverflowCarry(List<Item> carried)
        {
            if (carried == null || carried.Count == 0)
                return;
            int toStash = 0;
            foreach (Item item in carried)
            {
                try
                {
                    Item left = TryDeposit(item);
                    if (left == null)
                    {
                        toStash++;
                        continue;
                    }
                    StoreInOverflowChest(left);
                }
                catch (System.Exception ex)
                {
                    _monitor.Log($"JunimoStashService: could not put back overflow item '{item.QualifiedItemId}' x{item.Stack}. {ex.GetType().Name}: {ex.Message}", LogLevel.Error);
                }
            }
            _monitor.Log($"JunimoStashService: {carried.Count} carried overflow item(s) back: {toStash} into the stash, {carried.Count - toStash} into the overflow chest.", LogLevel.Info);
        }
    }
}
