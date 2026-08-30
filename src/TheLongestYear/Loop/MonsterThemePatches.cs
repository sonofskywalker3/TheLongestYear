using System;
using System.Collections.Generic;
using HarmonyLib;
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Monsters;
using TheLongestYear.Core;

namespace TheLongestYear.Loop
{
    /// <summary>Spelunking bonus (monster_drops_double): 10% chance a slain monster drops
    /// everything twice. Postfix on GameLocation.monsterDrop (decompile GameLocation.cs:4360):
    /// snapshot the debris count before, then clone every item-bearing Debris the call added,
    /// the same shape as vanilla's own Book_Void 3% clone at the end of that method. Trinket
    /// spawns are not doubled.</summary>
    [HarmonyPatch(typeof(GameLocation), nameof(GameLocation.monsterDrop),
        new Type[] { typeof(Monster), typeof(int), typeof(int), typeof(Farmer) })]
    internal static class MonsterDropsDoublePatch
    {
        public const string BonusId = "monster_drops_double";
        private const double Chance = 0.10;
        private const string TrinketQualifier = "(TR)";

        private static void Prefix(GameLocation __instance, out int __state)
            => __state = __instance?.debris?.Count ?? -1;

        private static void Postfix(GameLocation __instance, Monster monster, int x, int y, Farmer who, int __state)
        {
            int stacks = ActiveEffectsProvider.BonusStacks(BonusId);
            if (stacks == 0) return;
            if (__state < 0 || __instance?.debris == null || monster == null || who == null) return;
            int total = __instance.debris.Count;
            if (total <= __state) return;

            // One independent roll per stack (Spelunking theme + Double Trouble boost); the best
            // roll is kept against the single threshold.
            double roll = 1.0;
            for (int s = 0; s < stacks; s++)
                roll = Math.Min(roll, Game1.random.NextDouble());
            if (roll >= Chance)
            {
                PatchLog.Trace($"{BonusId}: roll={roll:F3} >= {Chance:F2}, no double.");
                return;
            }

            try
            {
                Vector2 playerPos = Utility.PointToVector2(who.StandingPixel);
                var clones = new List<Debris>();
                for (int i = __state; i < total; i++)
                {
                    Debris d = __instance.debris[i];
                    Item clone = null;
                    if (d?.item != null)
                    {
                        if (d.item is StardewValley.Objects.Trinkets.Trinket) continue;
                        clone = d.item.getOne();
                        if (clone != null)
                        {
                            clone.Stack = d.item.Stack;
                            clone.HasBeenInInventory = false;
                        }
                    }
                    else if (!string.IsNullOrEmpty(d?.itemId?.Value))
                    {
                        if (d.itemId.Value.StartsWith(TrinketQualifier, StringComparison.Ordinal)) continue;
                        clone = ItemRegistry.Create(d.itemId.Value, 1, 0, allowNull: true);
                        if (clone != null) clone.HasBeenInInventory = false;
                    }
                    if (clone != null)
                        clones.Add(monster.ModifyMonsterLoot(new Debris(clone, new Vector2(x, y), playerPos)));
                }
                foreach (Debris c in clones)
                    __instance.debris.Add(c);
                PatchLog.Info($"{BonusId}: {monster.Name} dropped everything twice ({clones.Count} extra drop(s), roll {roll:F3}).");
            }
            catch (Exception ex)
            {
                PatchLog.Trace($"{BonusId}: clone path threw: {ex.GetType().Name}: {ex.Message}");
            }
        }
    }

    /// <summary>Kitchen liability (monster_damage_up): monsters deal 25% more damage. Prefix on
    /// Farmer.takeDamage (decompile Farmer.cs:7331); only when a monster is the damager, so
    /// environmental damage is untouched.</summary>
    [HarmonyPatch(typeof(Farmer), nameof(Farmer.takeDamage), new Type[] { typeof(int), typeof(bool), typeof(Monster) })]
    internal static class MonsterDamageUpPatch
    {
        public const string LiabilityId = "monster_damage_up";
        private const double Factor = 1.25;

        private static void Prefix(ref int damage, Monster damager)
        {
            if (damager == null || !ActiveEffectsProvider.ActiveLiability(LiabilityId)) return;
            int boosted = (int)Math.Ceiling(damage * Factor);
            PatchLog.Trace($"{LiabilityId}: {damager.Name} damage {damage} -> {boosted}.");
            damage = boosted;
        }
    }
}
