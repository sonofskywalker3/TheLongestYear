using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Objects;
using TheLongestYear.Core.Sabotage;

namespace TheLongestYear.Loop
{
    /// <summary>The blight front reaching into storage (Jeff, 2026-09-09): units vanish in the
    /// night, one at a time off random stacks. A night raids ONE chest (Jeff, 2026-09-21): the first
    /// chest a roll lands in is the night's chest, and the rest are safe until tomorrow. Placed
    /// machines are not chests and stay in the draw throughout. Food spoils; anything else goes
    /// missing. Every chest on every map is in the draw except the Junimo Stash and chests on a
    /// Circle of Warding. Below Extreme only plain objects are taken (never tools, weapons or big
    /// craftables). On Extreme (<paramref name="everything"/>, spec 2026-09-15 Part B, 2.5) anything
    /// in an unwarded chest can go, and machines placed on the FARM map join the pool at three units
    /// each; a machine on a circle's tiles is protected like a chest there.</summary>
    internal static class SpoilagePass
    {
        private sealed class Entry
        {
            public Chest Chest;          // null for a placed machine
            public int Slot;
            public Item Item;
            public GameLocation Location; // the chest's or the machine's map
            public Vector2 Tile;          // the chest's or the machine's tile
            public bool BigCraftable;
            public int Units => BlightRule.UnitsOf(Item.Stack, BigCraftable);
        }

        /// <summary>One unit the darkness takes tonight, chosen by <see cref="Plan"/> and removed by
        /// <see cref="Apply"/>. The scene reads it to show where the loss landed (spec 2026-09-21).</summary>
        public sealed class Hit
        {
            public Chest Chest;            // null for a placed machine
            public int Slot;
            public Item Item;
            public GameLocation Location;  // the chest's or the machine's map
            public Vector2 Tile;
            public bool Machine;
            public bool Perishable;
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
                    bool onFarm = loc == farm;
                    if (obj is Chest chest)
                    {
                        if (chest.modData.ContainsKey(JunimoStashService.StashModDataKey)) continue;
                        bool chestWarded = CircleOfWardingService.Covers(circles, loc, chest.TileLocation);
                        var items = chest.Items;
                        for (int i = 0; i < items.Count; i++)
                        {
                            Item item = items[i];
                            if (item == null || item.Stack <= 0) continue;
                            bool big = item is StardewValley.Object o && o.bigCraftable.Value;
                            bool plain = item is StardewValley.Object && !big;
                            if (!BlightRule.InStoragePool(plain, big, placedOnMap: false, onFarm, chestWarded, everything)) continue;
                            entries.Add(new Entry { Chest = chest, Slot = i, Item = item, Location = loc, Tile = pair.Key, BigCraftable = big });
                        }
                        continue;
                    }
                    bool placedWarded = CircleOfWardingService.Covers(circles, loc, pair.Key);
                    bool placedBig = obj.bigCraftable.Value;
                    if (!BlightRule.InStoragePool(!placedBig, placedBig, placedOnMap: true, onFarm, placedWarded, everything)) continue;
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

        /// <summary>Choose up to <paramref name="count"/> units to take, each off an entry picked by
        /// <paramref name="rng"/> weighted by its units. A machine costs three of the night's count
        /// (Jeff's budget rule: a night of 15 loses at most 5 machines) but counts as ONE thing in
        /// the report, because one keg went missing, not three. The night has ONE chest (Jeff,
        /// 2026-09-21): once a roll lands in a chest, every other chest leaves the pool, though
        /// machines stay in it. Reads only: nothing is removed until <see cref="Apply"/> runs.</summary>
        public static List<Hit> Plan(int count, Random rng, bool everything)
        {
            var hits = new List<Hit>();
            if (count <= 0) return hits;
            List<Entry> entries = Entries(everything);
            // The rule lives in Core so it can be tested without a game: it draws the units and
            // enforces the one-chest constraint; this side only says which chest each entry is in.
            var pool = new List<TakeCandidate>(entries.Count);
            var chests = new List<Chest>();
            foreach (Entry e in entries)
                pool.Add(new TakeCandidate(OwnerIdOf(e.Chest, chests), e.Units, e.BigCraftable));
            foreach (int index in BlightRule.PlanTake(pool, count, rng))
                hits.Add(HitFor(entries[index]));
            return hits;
        }

        /// <summary>A placed machine belongs to no chest; the planner reads a negative owner as
        /// "machine" and never locks the night onto it.</summary>
        private const int MachineOwnerId = -1;

        /// <summary>Which chest an entry sits in, as an index into <paramref name="chests"/> (a
        /// chest met for the first time is appended).</summary>
        private static int OwnerIdOf(Chest chest, List<Chest> chests)
        {
            if (chest == null) return MachineOwnerId;
            for (int i = 0; i < chests.Count; i++)
                if (ReferenceEquals(chests[i], chest)) return i;
            chests.Add(chest);
            return chests.Count - 1;
        }

        private static Hit HitFor(Entry e) => new Hit
        {
            Chest = e.Chest,
            Slot = e.Slot,
            Item = e.Item,
            Location = e.Location,
            Tile = e.Tile,
            Machine = e.Chest == null,
            Perishable = !e.BigCraftable && BlightRule.IsPerishableCategory(e.Item.Category),
        };

        /// <summary>Do the removals. An item that has already gone (stack spent, machine moved) is
        /// passed over, so a double call cannot take twice from a stack that the plan emptied.</summary>
        public static Taken Apply(List<Hit> hits)
        {
            int spoiled = 0, missing = 0;
            foreach (Hit h in hits)
            {
                if (h.Machine)
                {
                    // A placed machine vanishes with whatever it held (spec 2.5).
                    if (h.Location.objects.TryGetValue(h.Tile, out StardewValley.Object o) && ReferenceEquals(o, h.Item))
                    {
                        h.Location.objects.Remove(h.Tile);
                        missing++;
                    }
                    continue;
                }
                if (h.Item.Stack <= 0 || h.Slot >= h.Chest.Items.Count || !ReferenceEquals(h.Chest.Items[h.Slot], h.Item)) continue;
                h.Item.Stack -= 1;
                if (h.Item.Stack <= 0) h.Chest.Items[h.Slot] = null;
                if (h.Perishable) spoiled++; else missing++;
            }
            return new Taken(spoiled, missing);
        }

        /// <summary>Plan and apply in one call: the debug entry point, and the shape the night pass
        /// had before the pick and the apply were split.</summary>
        public static Taken Strike(int count, Random rng, bool everything) => Apply(Plan(count, rng, everything));
    }
}
