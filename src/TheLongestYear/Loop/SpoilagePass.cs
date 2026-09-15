using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Objects;
using TheLongestYear.Core.Sabotage;

namespace TheLongestYear.Loop
{
    /// <summary>The blight front reaching into storage (Jeff, 2026-09-09): units vanish from the
    /// player's chests in the night, one at a time off random stacks. Food spoils; anything else
    /// goes missing. Every chest on every map counts except the Junimo Stash and chests on a Circle
    /// of Warding. Below Extreme only plain objects are taken (never tools, weapons or big
    /// craftables). On Extreme (<paramref name="everything"/>, spec 2026-09-15 Part B, 2.5) anything
    /// in an unwarded chest can go, and machines placed on the FARM map join the pool at three
    /// units each; a machine on a circle's tiles is protected like a chest there.</summary>
    internal static class SpoilagePass
    {
        private sealed class Entry
        {
            public Chest Chest;          // null for a placed machine
            public int Slot;
            public Item Item;
            public GameLocation Location; // placed machine only
            public Vector2 Tile;          // placed machine only
            public bool BigCraftable;
            public int Units => BlightRule.UnitsOf(Item.Stack, BigCraftable);
        }

        public readonly struct Taken
        {
            public readonly int Spoiled;
            public readonly int Missing;
            public Taken(int spoiled, int missing) { Spoiled = spoiled; Missing = missing; }
            public int Total => Spoiled + Missing;
        }

        private static List<Entry> Entries(bool everything)
        {
            var entries = new List<Entry>();
            var circles = CircleOfWardingService.ProtectedTiles();
            Farm farm = Game1.getFarm();
            Utility.ForEachLocation(loc =>
            {
                foreach (KeyValuePair<Vector2, StardewValley.Object> pair in loc.objects.Pairs)
                {
                    StardewValley.Object obj = pair.Value;
                    if (obj is Chest chest)
                    {
                        if (chest.modData.ContainsKey(JunimoStashService.StashModDataKey)) continue;
                        if (CircleOfWardingService.Covers(circles, loc, chest.TileLocation)) continue;
                        var items = chest.Items;
                        for (int i = 0; i < items.Count; i++)
                        {
                            Item item = items[i];
                            if (item == null || item.Stack <= 0) continue;
                            bool big = item is StardewValley.Object o && o.bigCraftable.Value;
                            bool plain = item is StardewValley.Object && !big;
                            if (!everything && !plain) continue;
                            entries.Add(new Entry { Chest = chest, Slot = i, Item = item, BigCraftable = big });
                        }
                        continue;
                    }
                    if (!everything || loc != farm) continue;
                    if (!obj.bigCraftable.Value) continue;
                    if (CircleOfWardingService.Covers(circles, loc, pair.Key)) continue;
                    entries.Add(new Entry { Item = obj, Location = loc, Tile = pair.Key, BigCraftable = true });
                }
                return true;
            });
            return entries;
        }

        /// <summary>Total units at stake (stash excluded), for the roll.</summary>
        public static int StoredUnits(bool everything)
        {
            int units = 0;
            foreach (Entry e in Entries(everything)) units += e.Units;
            return units;
        }

        /// <summary>Take up to <paramref name="count"/> units, each off an entry picked by
        /// <paramref name="rng"/> weighted by its units. A machine costs three of the count.</summary>
        public static Taken Strike(int count, Random rng, bool everything)
        {
            if (count <= 0) return new Taken(0, 0);
            List<Entry> entries = Entries(everything);
            int spoiled = 0, missing = 0, taken = 0;
            while (taken < count && entries.Count > 0)
            {
                int total = 0;
                foreach (Entry e in entries) total += e.Units;
                if (total <= 0) break;
                int roll = rng.Next(total);
                Entry hit = entries[0];
                foreach (Entry e in entries)
                {
                    roll -= e.Units;
                    if (roll < 0) { hit = e; break; }
                }
                int cost = BlightRule.UnitsOf(1, hit.BigCraftable);
                taken += cost;
                if (hit.BigCraftable || !BlightRule.IsPerishableCategory(hit.Item.Category)) missing += cost; else spoiled += cost;
                if (hit.Chest != null)
                {
                    hit.Item.Stack -= 1;
                    if (hit.Item.Stack <= 0) { hit.Chest.Items[hit.Slot] = null; entries.Remove(hit); }
                }
                else
                {
                    // A placed machine vanishes with whatever it held (spec 2.5).
                    hit.Location.objects.Remove(hit.Tile);
                    entries.Remove(hit);
                }
            }
            return new Taken(spoiled, missing);
        }
    }
}
