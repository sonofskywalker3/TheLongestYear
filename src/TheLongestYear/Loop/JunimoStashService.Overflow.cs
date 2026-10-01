using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Objects;
using TheLongestYear.Core;

namespace TheLongestYear.Loop
{
    internal sealed partial class JunimoStashService
    {
        /// <summary>Put an item in the stash the way a player deposit would land, without the HUD:
        /// stack onto matching stacks first, then take a free slot under the slot cap. Returns what
        /// did not fit, null when everything went in.</summary>
        internal Item TryDeposit(Item item)
        {
            Chest chest = FindStashChest();
            if (chest == null || item == null)
                return item;

            foreach (Item slot in chest.Items)
            {
                if (slot == null || !slot.canStackWith(item)) continue;
                item.Stack = slot.addToStack(item);
                if (item.Stack <= 0)
                    return null;
            }

            if (chest.Items.Count(i => i != null) >= _meta.StashSlotCount)
                return item;
            int empty = chest.Items.IndexOf(null);
            if (empty >= 0)
                chest.Items[empty] = item;
            else
                chest.Items.Add(item);
            return null;
        }

        /// <summary>Drop an item one tile south of the stash chest (the spec's "next to the stash
        /// chest"), or at the farmhouse door if no chest is placed. Never deleted.</summary>
        internal void DropNearStash(Item item)
        {
            Farm farm = Game1.getFarm();
            if (farm == null || item == null)
                return;
            Vector2 tile = _placedTile ?? (TryGetFarmHouseEntry(farm) is Point door ? new Vector2(door.X, door.Y) : Vector2.Zero);
            Vector2 pixel = (tile + new Vector2(0f, 1f)) * 64f + new Vector2(32f, 32f);
            Game1.createItemDebris(item, pixel, -1, farm);
            _monitor.Log($"JunimoStashService: stash full, dropped '{item.QualifiedItemId}' x{item.Stack} by the stash at ({tile.X}, {tile.Y + 1}).", LogLevel.Info);
        }

        /// <summary>Before the stale chest from the save is swept: fill in banked records that a
        /// 0.18.118-or-earlier save wrote without contents or identity (see StashLegacyRescue). The
        /// save's chest and MetaState.StashItems are written by the same Saving event, so they list
        /// the same items in the same order; on any mismatch nothing is touched.</summary>
        private void RescueLegacyRecords(Chest stale)
        {
            List<Item> live = stale.Items.Where(i => i != null).ToList();
            if (live.Count != _meta.StashItems.Count)
            {
                _monitor.Log($"JunimoStashService: stale stash holds {live.Count} items, MetaState {_meta.StashItems.Count}; no legacy rescue.", LogLevel.Trace);
                return;
            }
            int rescued = 0;
            for (int i = 0; i < live.Count; i++)
            {
                StashItemRecord fresh = StashItemCodec.ToRecord(live[i]);
                if (!StashLegacyRescue.ShouldReplace(_meta.StashItems[i], fresh)) continue;
                _meta.StashItems[i] = fresh;
                rescued++;
            }
            if (rescued > 0)
                _monitor.Log($"JunimoStashService: filled in {rescued} stash record(s) written by an older version.", LogLevel.Info);
        }
    }
}
