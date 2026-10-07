using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Inventories;
using StardewValley.Objects;
using TheLongestYear.Core.Sabotage;

namespace TheLongestYear.Loop
{
    /// <summary>The blight front reaching into storage (Jeff, 2026-09-09): units vanish in the
    /// night, one at a time off random stacks. A night raids ONE chest (Jeff, 2026-09-21): the first
    /// chest a roll lands in is the night's chest, and the rest are safe until tomorrow. Placed
    /// machines are not chests and stay in the draw throughout. Food spoils; anything else goes
    /// missing. Every chest on every map is in the draw except the Junimo Stash, Mini-Shipping Bins
    /// (shipped overnight) and chests on a Circle of Warding. Junimo Chests are in (Jeff,
    /// 2026-10-07): they all show one shared inventory, which is drawn once and taken from once, so
    /// a unit stolen there is gone from every Junimo Chest. Below Extreme only plain objects are
    /// taken (never tools, weapons or big
    /// craftables). On Extreme (<paramref name="everything"/>, spec 2026-09-15 Part B, 2.5) anything
    /// in an unwarded chest can go, and machines placed on the FARM map join the pool at three units
    /// each; a machine on a circle's tiles is protected like a chest there. While the thief scene is
    /// still due this loop, only chests it can show (<see cref="OnSceneMap"/>) are in the draw
    /// (designer, 2026-10-07); once it has played, every chest is.</summary>
    internal static class SpoilagePass
    {
        private sealed class Entry
        {
            public Chest Chest;          // null for a placed machine
            public IInventory Inventory; // what the chest shows: its own, or the shared one
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
            public IInventory Inventory;   // the stock the unit leaves: shared by every Junimo Chest
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

        /// <summary>One object met on the walk over the maps, before the shared inventories are
        /// sorted out: a chest (with the stock it shows) or a placed machine.</summary>
        private sealed class Seen
        {
            public GameLocation Location;
            public Vector2 Tile;
            public StardewValley.Object Object;
            public Chest Chest;
            public IInventory Inventory;
            public bool OnFarm;
            public bool Warded;
        }

        /// <summary>The maps the thief scene can show (<see cref="TheLongestYear.Scenes.ThiefScene"/>):
        /// the Farm, the FarmHouse, the Cellar and a Shed.</summary>
        internal static bool OnSceneMap(GameLocation loc)
            => loc is Farm || loc is StardewValley.Locations.FarmHouse || loc is StardewValley.Locations.Cellar || loc is StardewValley.Shed;

        /// <param name="everything">Extreme: anything in a chest, and the farm's placed machines.</param>
        /// <param name="thiefSceneDue">The thief scene has not played this loop yet: only chests it
        /// can show are in (<see cref="BlightRule.InThiefDraw"/>). Placed machines are only ever in
        /// on the Farm map, which the scene shows.</param>
        private static List<Entry> Entries(bool everything, bool thiefSceneDue)
        {
            var circles = CircleOfWardingService.ProtectedTiles();
            Farm farm = Game1.getFarm();
            var seen = new List<Seen>();
            Utility.ForEachLocation(loc =>
            {
                foreach (KeyValuePair<Vector2, StardewValley.Object> pair in loc.objects.Pairs)
                {
                    StardewValley.Object obj = pair.Value;
                    if (obj is Chest chest)
                    {
                        if (chest.modData.ContainsKey(JunimoStashService.StashModDataKey)) continue;
                        // A Mini-Shipping Bin ships overnight before the strike lands.
                        if (!BlightRule.ChestInDraw(shipsOvernight: chest.SpecialChestType == Chest.SpecialChestTypes.MiniShippingBin)) continue;
                        seen.Add(new Seen
                        {
                            Location = loc, Tile = pair.Key, Object = obj, Chest = chest,
                            // A Junimo Chest's own Items list is empty: its stock is the team's
                            // shared inventory, which GetItemsForPlayer returns (Chest.cs:990).
                            Inventory = chest.GetItemsForPlayer(),
                            OnFarm = Filmable(loc, pair.Key, thiefSceneDue),
                            Warded = CircleOfWardingService.Covers(circles, loc, chest.TileLocation),
                        });
                        continue;
                    }
                    // A placed machine is only ever at stake on the Farm itself; while the thief scene
                    // is due, only one he can stand beside (review I1).
                    if (thiefSceneDue && loc == farm && !BlightRule.MachineInThiefDraw(TheLongestYear.Scenes.ThiefScene.HasWayIn(loc, pair.Key), thiefSceneDue)) continue;
                    seen.Add(new Seen { Location = loc, Tile = pair.Key, Object = obj, OnFarm = loc == farm, Warded = CircleOfWardingService.Covers(circles, loc, pair.Key) });
                }
                return true;
            });

            // Chests that show one inventory are one chest in the draw (the rule is in Core).
            var inventories = new List<IInventory>();
            var seats = new List<ChestSeat>();
            var seatOf = new Dictionary<Seen, int>();
            foreach (Seen s in seen)
            {
                if (s.Chest == null) continue;
                seatOf[s] = seats.Count;
                seats.Add(new ChestSeat(InventoryIdOf(s.Inventory, inventories), s.OnFarm, s.Warded));
            }
            IReadOnlyList<ChestHost> hosts = BlightRule.ChestHosts(seats);

            var entries = new List<Entry>();
            foreach (Seen s in seen)
            {
                if (s.Chest != null)
                {
                    ChestHost host = hosts[seatOf[s]];
                    if (!BlightRule.InThiefDraw(host, thiefSceneDue)) continue;
                    bool onFarmMap = s.Location == farm;
                    for (int i = 0; i < s.Inventory.Count; i++)
                    {
                        Item item = s.Inventory[i];
                        if (item == null || item.Stack <= 0) continue;
                        bool big = item is StardewValley.Object o && o.bigCraftable.Value;
                        bool plain = item is StardewValley.Object && !big;
                        if (!BlightRule.InStoragePool(plain, big, placedOnMap: false, onFarmMap, host.Warded, everything)) continue;
                        entries.Add(new Entry { Chest = s.Chest, Inventory = s.Inventory, Slot = i, Item = item, Location = s.Location, Tile = s.Tile, BigCraftable = big });
                    }
                    continue;
                }
                bool placedBig = s.Object.bigCraftable.Value;
                if (!BlightRule.InStoragePool(!placedBig, placedBig, placedOnMap: true, s.OnFarm, s.Warded, everything)) continue;
                entries.Add(new Entry { Item = s.Object, Location = s.Location, Tile = s.Tile, BigCraftable = true });
            }
            return entries;
        }

        /// <summary>Can the thief scene show a chest here? On a scene map, and, while the scene is due,
        /// with a tile beside it he can stand on (review I1: the scene's own staging check at pick
        /// time). When the scene is not due the walk is not asked: no scene will stage.</summary>
        private static bool Filmable(GameLocation loc, Vector2 tile, bool thiefSceneDue)
            => BlightRule.SeatFilmable(OnSceneMap(loc), !thiefSceneDue || TheLongestYear.Scenes.ThiefScene.HasWayIn(loc, tile));

        /// <summary>An index standing for <paramref name="inventory"/>: equal for every chest that
        /// shows the very same inventory object.</summary>
        private static int InventoryIdOf(IInventory inventory, List<IInventory> inventories)
        {
            for (int i = 0; i < inventories.Count; i++)
                if (ReferenceEquals(inventories[i], inventory)) return i;
            inventories.Add(inventory);
            return inventories.Count - 1;
        }

        /// <summary>Total units at stake (stash excluded), for the roll. With
        /// <paramref name="thiefSceneDue"/>, only what the thief scene can show.</summary>
        public static int StoredUnits(bool everything, bool thiefSceneDue = false)
        {
            int units = 0;
            foreach (Entry e in Entries(everything, thiefSceneDue)) units += e.Units;
            return units;
        }

        /// <summary>Choose up to <paramref name="count"/> units to take, each off an entry picked by
        /// <paramref name="rng"/> weighted by its units. A machine costs three of the night's count
        /// (Jeff's budget rule: a night of 15 loses at most 5 machines) but counts as ONE thing in
        /// the report, because one keg went missing, not three. The night has ONE chest (Jeff,
        /// 2026-09-21): once a roll lands in a chest, every other chest leaves the pool, though
        /// machines stay in it. Reads only: nothing is removed until <see cref="Apply"/> runs.</summary>
        public static List<Hit> Plan(int count, Random rng, bool everything, bool thiefSceneDue = false)
        {
            var hits = new List<Hit>();
            if (count <= 0) return hits;
            List<Entry> entries = Entries(everything, thiefSceneDue);
            // The rule lives in Core so it can be tested without a game: it draws the units and
            // enforces the one-chest constraint. This side only says which chest each entry is in.
            var pool = new List<TakeCandidate>(entries.Count);
            var chests = new List<Chest>();
            foreach (Entry e in entries)
                pool.Add(new TakeCandidate(OwnerIdOf(e.Chest, chests), e.Units, e.BigCraftable));
            foreach (int index in BlightRule.PlanTake(pool, count, rng))
                hits.Add(HitFor(entries[index]));
            return hits;
        }

        /// <summary>A placed machine belongs to no chest. The planner reads a negative owner as
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
            Inventory = e.Inventory,
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
                // The stock the plan read: a chest's own, or the one every Junimo Chest shows, so a
                // unit taken there is gone from all of them.
                IInventory stock = h.Inventory ?? h.Chest.GetItemsForPlayer();
                if (h.Item.Stack <= 0 || h.Slot >= stock.Count || !ReferenceEquals(stock[h.Slot], h.Item)) continue;
                h.Item.Stack -= 1;
                if (h.Item.Stack <= 0) stock[h.Slot] = null;
                if (h.Perishable) spoiled++; else missing++;
            }
            return new Taken(spoiled, missing);
        }

        /// <summary>Plan and apply in one call: the debug entry point, and the shape the night pass
        /// had before the pick and the apply were split.</summary>
        public static Taken Strike(int count, Random rng, bool everything) => Apply(Plan(count, rng, everything, thiefSceneDue: false));
    }
}
