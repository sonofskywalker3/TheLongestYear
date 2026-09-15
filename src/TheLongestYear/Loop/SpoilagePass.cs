using System;
using System.Collections.Generic;
using StardewValley;
using StardewValley.Objects;
using TheLongestYear.Core.Sabotage;

namespace TheLongestYear.Loop
{
    /// <summary>The blight front reaching into storage (Jeff, 2026-09-09): units vanish from the
    /// player's chests in the night, one at a time off random stacks. Food spoils; anything else
    /// goes missing, taken by the people and creatures the darkness works through. Every chest
    /// on every map counts except the Junimo Stash, which is the cross-loop bank and is never
    /// touched, and chests standing on a Circle of Warding. Only plain objects are taken (never
    /// tools, weapons or big craftables).</summary>
    internal static class SpoilagePass
    {
        private sealed class Stack
        {
            public Chest Chest;
            public int Slot;
            public Item Item;
        }

        public readonly struct Taken
        {
            public readonly int Spoiled;
            public readonly int Missing;
            public Taken(int spoiled, int missing) { Spoiled = spoiled; Missing = missing; }
            public int Total => Spoiled + Missing;
        }

        private static List<Stack> Stacks()
        {
            var stacks = new List<Stack>();
            var circles = CircleOfWardingService.ProtectedTiles();
            Utility.ForEachLocation(loc =>
            {
                foreach (StardewValley.Object obj in loc.objects.Values)
                {
                    if (obj is not Chest chest) continue;
                    if (chest.modData.ContainsKey(JunimoStashService.StashModDataKey)) continue;
                    if (CircleOfWardingService.Covers(circles, loc, chest.TileLocation)) continue;
                    var items = chest.Items;
                    for (int i = 0; i < items.Count; i++)
                    {
                        Item item = items[i];
                        if (item == null || item.Stack <= 0) continue;
                        if (item is not StardewValley.Object o || o.bigCraftable.Value) continue;
                        stacks.Add(new Stack { Chest = chest, Slot = i, Item = item });
                    }
                }
                return true;
            });
            return stacks;
        }

        /// <summary>Total units stored in chests (stash excluded), for the roll.</summary>
        public static int StoredUnits()
        {
            int units = 0;
            foreach (Stack s in Stacks()) units += s.Item.Stack;
            return units;
        }

        /// <summary>Task 7's signature, stubbed: on Extreme the pass reaches tools, weapons, rings,
        /// boots, hats and machines too. Until Task 7 fills it in the flag is ignored.</summary>
        public static int StoredUnits(bool everything) => StoredUnits();

        /// <summary>Task 7's signature, stubbed: see <see cref="StoredUnits(bool)"/>.</summary>
        public static Taken Strike(int count, Random rng, bool everything) => Strike(count, rng);

        /// <summary>Take up to <paramref name="count"/> units, each off a stack picked by
        /// <paramref name="rng"/> weighted by stack size.</summary>
        public static Taken Strike(int count, Random rng)
        {
            if (count <= 0) return new Taken(0, 0);
            List<Stack> stacks = Stacks();
            int spoiled = 0, missing = 0;
            for (int n = 0; n < count && stacks.Count > 0; n++)
            {
                int total = 0;
                foreach (Stack s in stacks) total += s.Item.Stack;
                if (total <= 0) break;
                int roll = rng.Next(total);
                Stack hit = stacks[0];
                foreach (Stack s in stacks)
                {
                    roll -= s.Item.Stack;
                    if (roll < 0) { hit = s; break; }
                }
                hit.Item.Stack -= 1;
                if (BlightRule.IsPerishableCategory(hit.Item.Category)) spoiled++; else missing++;
                if (hit.Item.Stack <= 0)
                {
                    hit.Chest.Items[hit.Slot] = null;
                    stacks.Remove(hit);
                }
            }
            return new Taken(spoiled, missing);
        }
    }
}
