using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Objects;
using StardewValley.Objects.Trinkets;

namespace TheLongestYear.Loop
{
    /// <summary>tly_stashnest: drive the stash nesting rule and the identity round trip from the
    /// debug bridge (spec 2026-10-01 live checks). Developer-only.</summary>
    internal static class StashNestingDebug
    {
        private static readonly Color DyeRed = new(200, 40, 40);
        private const int FillHatCount = 60;
        private const string TrinketSlotsStat = "trinketSlots";

        public static void Run(string[] args, IMonitor monitor, JunimoStashService stash)
        {
            Chest chest = stash?.FindStashChest();
            if (chest == null) { monitor.Log("tly_stashnest: no stash chest. Own stash_1 and reload first.", LogLevel.Warn); return; }
            string mode = args.Length > 0 ? args[0] : "check";
            switch (mode)
            {
                case "hats": Deposit(chest, Dresser(monitor, withRing: false), monitor); break;
                case "ring": Deposit(chest, Dresser(monitor, withRing: true), monitor); break;
                case "gear": foreach (Item i in Gear()) Deposit(chest, i, monitor); break;
                case "legacy":
                    chest.Items.Add(Dresser(monitor, withRing: true));
                    monitor.Log("tly_stashnest: legacy dresser (hat, shirt, pants, ring) placed straight in the stash.", LogLevel.Info);
                    break;
                case "fill": Fill(stash, monitor); break;
                case "wear": Wear(monitor); break;
                case "worn": LogWorn(monitor); break;
                default:
                    foreach (Item i in chest.Items.Where(i => i != null))
                        monitor.Log("tly_stashnest: " + Describe(i), LogLevel.Info);
                    break;
            }
        }

        private static void Deposit(Chest chest, Item item, IMonitor monitor)
        {
            if (item == null) return;
            Item back = chest.addItem(item);
            if (back != null) Game1.player.addItemToInventory(back);
            monitor.Log($"tly_stashnest: {(back == null ? "accepted" : "refused")} {Describe(item)}", LogLevel.Info);
        }

        private static Item Dresser(IMonitor monitor, bool withRing)
        {
            StorageFurniture dresser = null;
            foreach (string key in DataLoader.Furniture(Game1.content).Keys)
                if (ItemRegistry.Create("(F)" + key, allowNull: true) is StorageFurniture sf && sf is not FishTankFurniture)
                {
                    dresser = sf;
                    break;
                }
            if (dresser == null) { monitor.Log("tly_stashnest: no dresser in Data/Furniture.", LogLevel.Warn); return null; }
            monitor.Log($"tly_stashnest: using dresser {dresser.QualifiedItemId}.", LogLevel.Info);
            dresser.heldItems.Add(ItemRegistry.Create("(H)0"));
            var shirt = ItemRegistry.Create<Clothing>("(S)1000");
            shirt.clothesColor.Value = DyeRed;
            dresser.heldItems.Add(shirt);
            dresser.heldItems.Add(ItemRegistry.Create("(P)0"));
            if (withRing) dresser.heldItems.Add(ItemRegistry.Create("(O)529"));
            return dresser;
        }

        private static List<Item> Gear()
        {
            var pants = ItemRegistry.Create<Clothing>("(P)0");
            pants.clothesColor.Value = DyeRed;
            var boots = ItemRegistry.Create<Boots>("(B)504");               // Sneakers...
            boots.applyStats(ItemRegistry.Create<Boots>("(B)514"));       // ...tailored with Space Boots
            var ring = ItemRegistry.Create<CombinedRing>("(O)880");
            ring.combinedRings.Add(ItemRegistry.Create<Ring>("(O)529"));
            ring.combinedRings.Add(ItemRegistry.Create<Ring>("(O)530"));
            var trinket = ItemRegistry.Create<Trinket>("(TR)ParrotEgg");
            return new List<Item> { pants, boots, ring, trinket };
        }

        private static void Fill(JunimoStashService stash, IMonitor monitor)
        {
            int added = 0;
            for (int i = 0; i < FillHatCount; i++)
            {
                Item hat = ItemRegistry.Create("(H)" + i, allowNull: true);
                if (hat == null) continue;
                if (stash.TryDeposit(hat) != null) break;
                added++;
            }
            monitor.Log($"tly_stashnest: filled {added} slot(s); the stash is full.", LogLevel.Info);
        }

        private static void Wear(IMonitor monitor)
        {
            Farmer p = Game1.player;
            List<Item> gear = Gear();
            p.Equip((Boots)gear[1], p.boots);
            p.Equip((Ring)gear[2], p.leftRing);
            p.Equip(ItemRegistry.Create<Ring>("(O)529"), p.rightRing);
            p.stats.Set(TrinketSlotsStat, 1);
            p.trinketItems.Add((Trinket)gear[3]);
            LogWorn(monitor);
        }

        private static void LogWorn(IMonitor monitor)
        {
            Farmer p = Game1.player;
            var worn = new List<Item> { p.boots.Value, p.leftRing.Value, p.rightRing.Value };
            worn.AddRange(p.trinketItems);
            monitor.Log($"tly_stashnest: worn {string.Join(" | ", worn.Select(Describe))}; trinketSlots={p.stats.Get(TrinketSlotsStat)}.", LogLevel.Info);
        }

        private static string Describe(Item item)
        {
            if (item == null) return "none";
            var parts = new List<string> { item.QualifiedItemId };
            if (item is Clothing c) parts.Add($"colour={c.clothesColor.Value.PackedValue}");
            if (item is Boots b) parts.Add($"boots={b.appliedBootSheetIndex.Value}/{b.defenseBonus.Value}/{b.immunityBonus.Value}");
            if (item is CombinedRing r) parts.Add($"rings=[{string.Join(",", r.combinedRings.Select(x => x.QualifiedItemId))}]");
            if (item is Trinket t) parts.Add($"seed={t.generationSeed.Value}");
            if (StashItemCodec.ContainerItems(item) is IList<Item> inside && inside.Any(i => i != null))
                parts.Add($"contents=[{string.Join(", ", inside.Where(i => i != null).Select(Describe))}]");
            return string.Join(" ", parts);
        }
    }
}
