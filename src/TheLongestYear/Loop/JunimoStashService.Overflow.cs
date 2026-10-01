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

        /// <summary>Marks the plain chests that hold what the stash had no room for. Not the stash
        /// key: these are ordinary player chests, only tagged so a later overflow reuses them.</summary>
        internal const string OverflowModDataKey = "tly.junimo.stash.overflow";

        /// <summary>How far, in tiles, the overflow search walks out from below the stash.</summary>
        private const int OverflowSearchRadius = 12;

        /// <summary>Put an item the stash had no room for into an ordinary chest one tile south of
        /// the stash chest (the spec's "next to the stash chest"). Ground debris is not saved, so a
        /// chest is the only place that never deletes. Reuses an overflow chest already there; when
        /// that tile is blocked or that chest is full, walks outward to the nearest tagged chest
        /// with room or free tile. Falls back to the farmhouse door when no stash is placed.</summary>
        internal void StoreInOverflowChest(Item item) => StoreInOverflowChest(Game1.getFarm(), _placedTile, item, _monitor);

        /// <summary>The same, for callers without a stash service: <paramref name="stashTile"/> null
        /// anchors at the farmhouse door.</summary>
        internal static void StoreInOverflowChest(Farm farm, Vector2? stashTile, Item item, IMonitor monitor)
        {
            if (item == null)
                return;
            if (farm == null)
            {
                monitor.Log($"JunimoStashService: no farm loaded, could not store overflow '{item.QualifiedItemId}' x{item.Stack}.", LogLevel.Warn);
                return;
            }
            Vector2 anchor = stashTile ?? (TryGetFarmHouseEntry(farm) is Point door ? new Vector2(door.X, door.Y) : Vector2.Zero);
            Vector2 start = anchor + new Vector2(0f, 1f);
            string what = $"'{item.QualifiedItemId}' x{item.Stack}";

            foreach (Vector2 tile in TilesOutward(start, OverflowSearchRadius))
            {
                if (farm.objects.TryGetValue(tile, out StardewValley.Object existing))
                {
                    if (existing is Chest tagged && tagged.modData.ContainsKey(OverflowModDataKey)
                        && tagged.addItem(item) == null)
                    {
                        monitor.Log($"JunimoStashService: stash full, put {what} in the overflow chest at ({tile.X}, {tile.Y}).", LogLevel.Info);
                        return;
                    }
                    continue;
                }
                if (!farm.isTileOnMap(tile) || !IsTilePlaceable(farm, tile))
                    continue;

                var chest = new Chest(playerChest: true, tile, itemId: "130");
                chest.modData[OverflowModDataKey] = "1";
                chest.addItem(item);
                farm.objects[tile] = chest;
                monitor.Log($"JunimoStashService: stash full, placed an overflow chest at ({tile.X}, {tile.Y}) holding {what}.", LogLevel.Info);
                return;
            }

            // No tile within reach: the ground is the last resort (logged loudly, it is not saved).
            Game1.createItemDebris(item, start * 64f + new Vector2(32f, 32f), -1, farm);
            monitor.Log($"JunimoStashService: no free tile for an overflow chest near ({start.X}, {start.Y}); dropped '{item.QualifiedItemId}' x{item.Stack} on the ground there. Pick it up before saving.", LogLevel.Warn);
        }

        /// <summary>The start tile, then each square ring around it out to the radius.</summary>
        private static IEnumerable<Vector2> TilesOutward(Vector2 start, int radius)
        {
            yield return start;
            for (int r = 1; r <= radius; r++)
                for (int dy = -r; dy <= r; dy++)
                    for (int dx = -r; dx <= r; dx++)
                        if (System.Math.Max(System.Math.Abs(dx), System.Math.Abs(dy)) == r)
                            yield return start + new Vector2(dx, dy);
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
