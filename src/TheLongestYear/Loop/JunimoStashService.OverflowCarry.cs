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
        /// into the stash where they fit, the rest into a fresh overflow chest beside it. A container
        /// the stash nesting rule refuses goes straight to the overflow chest.</summary>
        internal void ReturnOverflowCarry(List<Item> carried)
        {
            if (carried == null || carried.Count == 0)
                return;
            int toStash = 0;
            foreach (Item item in carried)
            {
                try
                {
                    if (StashNestingRefusal.Blocked(item).Count > 0)
                    {
                        StoreInOverflowChest(item);
                        continue;
                    }
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
                    _monitor.Log($"JunimoStashService: could not put back overflow item '{item.QualifiedItemId}' x{item.Stack}; trying the overflow chest. {ex.GetType().Name}: {ex.Message}", LogLevel.Error);
                    LastResortOverflow(item);
                }
            }
            _monitor.Log($"JunimoStashService: {carried.Count} carried overflow item(s) back: {toStash} into the stash, {carried.Count - toStash} into the overflow chest.", LogLevel.Info);
        }

        /// <summary>Inside a catch that would otherwise drop an item: one more try at the overflow
        /// chest, itself guarded. If that throws too, the loss is logged as an Error.</summary>
        internal void LastResortOverflow(Item item) => LastResortOverflow(Game1.getFarm(), _placedTile, item, _monitor);

        internal static void LastResortOverflow(Farm farm, Microsoft.Xna.Framework.Vector2? stashTile, Item item, IMonitor monitor)
        {
            if (item == null)
                return;
            try
            {
                StoreInOverflowChest(farm, stashTile, item, monitor);
            }
            catch (System.Exception ex)
            {
                // Exception on purpose: this is the last guard. Logged loudly with the item.
                monitor.Log($"JunimoStashService: the last-resort overflow chest also failed; '{item.QualifiedItemId}' x{item.Stack} is lost. {ex.GetType().Name}: {ex.Message}", LogLevel.Error);
            }
        }
    }
}
