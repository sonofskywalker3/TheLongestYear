using System;
using System.Collections.Generic;
using StardewValley;
using StardewValley.Objects;
using TheLongestYear.Core.Sabotage;

namespace TheLongestYear.Loop
{
    /// <summary>The blight front reaching indoors (Jeff, 2026-09-09): perishables in the player's
    /// chests spoil in the night, one unit at a time off random stacks. Every chest on every map
    /// counts except the Junimo Stash, which is the cross-loop bank and is never touched.</summary>
    internal static class SpoilagePass
    {
        private sealed class Stack
        {
            public Chest Chest;
            public int Slot;
            public Item Item;
        }

        /// <summary>Every perishable stack in a chest that is not the stash, in a stable order.</summary>
        private static List<Stack> PerishableStacks()
        {
            var stacks = new List<Stack>();
            Utility.ForEachLocation(loc =>
            {
                foreach (StardewValley.Object obj in loc.objects.Values)
                {
                    if (obj is not Chest chest) continue;
                    if (chest.modData.ContainsKey(JunimoStashService.StashModDataKey)) continue;
                    var items = chest.Items;
                    for (int i = 0; i < items.Count; i++)
                    {
                        Item item = items[i];
                        if (item == null || item.Stack <= 0) continue;
                        if (!BlightRule.IsPerishableCategory(item.Category)) continue;
                        stacks.Add(new Stack { Chest = chest, Slot = i, Item = item });
                    }
                }
                return true;
            });
            return stacks;
        }

        /// <summary>Total perishable units stored, for the roll.</summary>
        public static int PerishableUnits()
        {
            int units = 0;
            foreach (Stack s in PerishableStacks()) units += s.Item.Stack;
            return units;
        }

        /// <summary>Spoil up to <paramref name="count"/> units, each off a stack picked by
        /// <paramref name="rng"/> weighted by stack size. Returns how many were removed.</summary>
        public static int Strike(int count, Random rng)
        {
            if (count <= 0) return 0;
            List<Stack> stacks = PerishableStacks();
            int spoiled = 0;
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
                spoiled++;
                if (hit.Item.Stack <= 0)
                {
                    hit.Chest.Items[hit.Slot] = null;
                    stacks.Remove(hit);
                }
            }
            return spoiled;
        }
    }
}
