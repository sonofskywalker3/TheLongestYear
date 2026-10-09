using System;
using System.Collections.Generic;
using System.Linq;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.Characters;
using StardewValley.Events;
using StardewValley.GameData.FarmAnimals;
using StardewValley.GameData.Pets;
using TheLongestYear.Core;
using TheLongestYear.Loop;

namespace TheLongestYear.DebugCommands
{
    /// <summary>Debug readouts for the animal powers (spec 2026-10-09, "Diagnostics and tests"). Every
    /// subcommand calls the game's own methods, so the Harmony patches run exactly as in play.</summary>
    internal static class AnimalPowersDebugCommand
    {
        public const string Name = "tly_animalpowers";
        public const string Description =
            "Debug: animal powers. Usage: tly_animalpowers [list] | produce <type> <n> | dig <n> | speed | pets | incubate | incubators | grow [new|<name>] | revoke <id> | feed | sethappy <0-255> | setfriend <0-1000> | births <n> | birthnight | doors | truffles | enter <coop|barn> [index]";

        private const int DefaultSamples = 200;
        private const string RegularEggQid = "(O)176";

        public static void Run(IMonitor monitor, MetaState meta, string[] args)
        {
            if (!Context.IsWorldReady || meta == null) { monitor.Log("Load a save first.", LogLevel.Warn); return; }
            string sub = args.Length == 0 ? "list" : args[0].ToLowerInvariant();
            try
            {
                switch (sub)
                {
                    case "list": List(monitor, meta); break;
                    case "produce" when args.Length >= 2:
                        Produce(monitor, args[1].Replace('_', ' '), Count(args, 2)); break;
                    case "dig": Dig(monitor, Count(args, 1)); break;
                    case "speed": Speed(monitor, meta); break;
                    case "pets": Pets(monitor); break;
                    case "incubate": Incubate(monitor); break;
                    case "grow":
                    {
                        // grow: every animal; grow new: only animals that arrived today; grow <name>: that animal.
                        string which = args.Length >= 2 ? args[1] : null;
                        int grown = 0;
                        foreach (FarmAnimal a in Game1.getFarm().getAllFarmAnimals())
                        {
                            if (which == "new" ? a.daysOwned.Value >= 0 : which != null && a.Name != which) continue;
                            a.growFully(new Random(1));
                            grown++;
                        }
                        monitor.Log($"{Name}: {grown} farm animal(s) grown up ({which ?? "all"}).", LogLevel.Info);
                        break;
                    }
                    case "revoke" when args.Length >= 2:
                        // Debug only: un-own a power so a control run can follow a run with it.
                        monitor.Log($"{Name}: revoke {args[1]}: {(meta.OwnedUpgrades.Remove(args[1]) ? "removed" : "was not owned")}.", LogLevel.Info);
                        break;
                    case "incubators": Incubators(monitor); break;
                    case "feed":
                        foreach (FarmAnimal a in Game1.getFarm().getAllFarmAnimals()) { a.fullness.Value = 255; a.happiness.Value = 255; }
                        monitor.Log($"{Name}: every farm animal fed and content (fullness 255, happiness 255).", LogLevel.Info);
                        break;
                    case "sethappy" when args.Length >= 2 && int.TryParse(args[1], out int h):
                        foreach (FarmAnimal a in Game1.getFarm().getAllFarmAnimals()) a.happiness.Value = (byte)Math.Clamp(h, 0, 255);
                        monitor.Log($"{Name}: every farm animal's happiness set to {h}.", LogLevel.Info);
                        break;
                    case "setfriend" when args.Length >= 2 && int.TryParse(args[1], out int f):
                        foreach (FarmAnimal a in Game1.getFarm().getAllFarmAnimals()) a.friendshipTowardFarmer.Value = Math.Clamp(f, 0, 1000);
                        monitor.Log($"{Name}: every farm animal's friendship set to {f}.", LogLevel.Info);
                        break;
                    case "births": Births(monitor, meta, Count(args, 1)); break;
                    case "birthnight":
                        GrowingHerdPatch.ForceNextBirth = true;
                        Game1.farmEventOverride = new QuestionEvent(QuestionEvent.barnBirth);
                        monitor.Log($"{Name}: tonight's farm event is a barn birth with a certain roll (sleep now).", LogLevel.Info);
                        break;
                    case "doors": Doors(monitor); break;
                    case "truffles": Truffles(monitor); break;
                    case "enter" when args.Length >= 2: Enter(monitor, args[1], args.Length >= 3 && int.TryParse(args[2], out int i) ? i : 0); break;
                    default: monitor.Log(Description, LogLevel.Warn); break;
                }
            }
            catch (Exception ex)
            {
                monitor.Log($"{Name} {sub}: threw {ex.GetType().Name}: {ex.Message}", LogLevel.Error);
            }
        }

        private static int Count(string[] args, int index)
            => args.Length > index && int.TryParse(args[index], out int n) && n > 0 ? n : DefaultSamples;

        private static void List(IMonitor monitor, MetaState meta)
        {
            IEnumerable<string> owned = AnimalPowers.AllIds.Where(meta.HasUpgrade);
            monitor.Log($"{Name}: owned powers: {string.Join(", ", owned.DefaultIfEmpty("none"))}. " +
                        $"Warm Welcome floor {AnimalPowers.WarmWelcomeFloor(meta.HasUpgrade)}. Keep Horse {meta.HasUpgrade(AnimalPowers.KeepHorse)}.",
                LogLevel.Info);
            foreach (FarmAnimal a in Game1.getFarm().getAllFarmAnimals())
            {
                FarmAnimalData data = a.GetAnimalData();
                int days = data?.DaysToProduce ?? -1;
                int target = AnimalPowers.ProduceTarget(a.type.Value, meta.HasUpgrade) ?? days;
                monitor.Log($"  {a.displayName} ({a.type.Value}) in {a.home?.buildingType.Value ?? "?"}: age {a.age.Value}/{data?.DaysToMature}, " +
                            $"daysOwned {a.daysOwned.Value}, daysSinceLastLay {a.daysSinceLastLay.Value}, produce days {days} -> {target}, " +
                            $"friendship {a.friendshipTowardFarmer.Value}, happiness {a.happiness.Value}, fullness {a.fullness.Value}, " +
                            $"wasAutoPet {a.wasAutoPet.Value}, currentProduce {a.currentProduce.Value ?? "none"}",
                    LogLevel.Info);
            }
        }

        /// <summary>Calls the patched GetProduceID on n throwaway adults with distinct ids (Molting Season'
        /// roll is seeded per animal and day, so one animal would give the same answer n times).</summary>
        private static void Produce(IMonitor monitor, string type, int n)
        {
            var tally = new Dictionary<string, int>();
            for (int i = 0; i < n; i++)
            {
                var a = new FarmAnimal(type, (long)Utility.RandomLong(), Game1.player.UniqueMultiplayerID);
                a.growFully(new Random(i));
                a.friendshipTowardFarmer.Value = 0;
                string id = a.GetProduceID(new Random(i)) ?? "null";
                tally[id] = tally.TryGetValue(id, out int c) ? c + 1 : 1;
            }
            monitor.Log($"{Name}: produce {type} x{n}: " +
                        string.Join(", ", tally.OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key}={kv.Value}")), LogLevel.Info);
        }

        /// <summary>n throwaway pigs (distinct ids) each dig one truffle beside the farmer through the
        /// patched DigUpProduce on the current map; tallies the TrufflesFound delta of each dig.</summary>
        private static void Dig(IMonitor monitor, int n)
        {
            GameLocation here = Game1.player.currentLocation;
            var perDig = new int[4];
            for (int i = 0; i < n; i++)
            {
                var pig = new FarmAnimal(AnimalPowers.PigType, (long)Utility.RandomLong(), Game1.player.UniqueMultiplayerID);
                pig.growFully(new Random(i));
                pig.currentLocation = here;
                // Spread the pigs over the farm (20 x 10 grid from tile 8,8) so spawnObjectAround finds open ground.
                pig.Position = new Microsoft.Xna.Framework.Vector2(8 + i % 20 * 3, 8 + i / 20 % 10 * 5) * Game1.tileSize;
                uint before = Game1.stats.TrufflesFound;
                pig.DigUpProduce(here, ItemRegistry.Create<StardewValley.Object>(AnimalPowers.TruffleQid));
                perDig[Math.Min(3, (int)(Game1.stats.TrufflesFound - before))]++;
            }
            int found = perDig[1] + perDig[2];
            monitor.Log($"{Name}: dig x{n}: no truffle {perDig[0]}, one {perDig[1]}, two {perDig[2]}, more {perDig[3]}; " +
                        $"doubled {(found == 0 ? 0 : 100.0 * perDig[2] / found):F1}% of digs that found one.", LogLevel.Info);
        }

        private static void Speed(IMonitor monitor, MetaState meta)
        {
            Farmer p = Game1.player;
            if (!p.isRidingHorse())
            {
                Horse horse = Utility.findHorseForPlayer(p.UniqueMultiplayerID)
                    ?? Game1.getFarm().characters.OfType<Horse>().FirstOrDefault();
                if (horse == null) { monitor.Log($"{Name}: no horse on the farm.", LogLevel.Warn); return; }
                if (string.IsNullOrEmpty(horse.Name)) horse.Name = "Swifty";
                if (string.IsNullOrEmpty(p.horseName.Value)) p.horseName.Value = horse.Name;
                if (horse.currentLocation != p.currentLocation)
                    Game1.warpCharacter(horse, p.currentLocation, p.Tile);
                horse.Position = p.Position;
                horse.checkAction(p, p.currentLocation);
                monitor.Log($"{Name}: mounting {horse.Name}; run 'speed' again in a second. Walking speed now {p.getMovementSpeed():F3}.", LogLevel.Info);
                return;
            }
            float actual = p.getMovementSpeed();
            int ms = Game1.currentGameTime.ElapsedGameTime.Milliseconds;
            float vanilla = Math.Max(1f, (p.speed + p.addedSpeed + 4.6f + (p.mount.ateCarrotToday ? 0.4f : 0f)
                + (p.stats.Get("Book_Horse") != 0 ? 0.5f : 0f)) * p.movementMultiplier * ms);
            monitor.Log($"{Name}: riding, getMovementSpeed {actual:F3}, vanilla riding sum {vanilla:F3} " +
                        $"(speed {p.speed} + added {p.addedSpeed} + 4.6, x{p.movementMultiplier} x {ms} ms), " +
                        $"difference {actual - vanilla:F3}, Swift Horse owned {meta.HasUpgrade(AnimalPowers.SwiftHorse)} " +
                        $"(expected +{AnimalPowers.SwiftHorseBonus(p.movementMultiplier, ms, false):F3}, {100.0 * (actual - vanilla) / vanilla:F1}%).",
                LogLevel.Info);
        }

        private static void Pets(IMonitor monitor)
        {
            var pets = Game1.content.Load<Dictionary<string, PetData>>("Data/Pets");
            foreach (var kv in pets)
            {
                int min = kv.Value.Gifts?.Count > 0 ? kv.Value.Gifts.Min(g => g.MinimumFriendshipThreshold) : -1;
                int max = kv.Value.Gifts?.Count > 0 ? kv.Value.Gifts.Max(g => g.MinimumFriendshipThreshold) : -1;
                monitor.Log($"{Name}: pet {kv.Key}: GiftChance {kv.Value.GiftChance}, gift thresholds {min}..{max} ({kv.Value.Gifts?.Count ?? 0} gifts).", LogLevel.Info);
            }
            foreach (Pet pet in Game1.getFarm().characters.OfType<Pet>().Concat(Utility.getHomeOfFarmer(Game1.player).characters.OfType<Pet>()))
                monitor.Log($"{Name}: {pet.Name} ({pet.petType.Value}) friendship {pet.friendshipTowardFarmer.Value}.", LogLevel.Info);
        }

        /// <summary>Growing Herd: runs the (patched) barn-birth setUp n times and counts the nights that would
        /// have had a birth, by parent type. Each success's birth dialogue is closed at once; no animal is born.
        /// Compare a run before buying the power with one after.</summary>
        private static void Births(IMonitor monitor, MetaState meta, int n)
        {
            var byType = new Dictionary<string, int>();
            int births = 0;
            for (int i = 0; i < n; i++)
            {
                var ev = new QuestionEvent(QuestionEvent.barnBirth);
                bool none = ev.setUp();
                if (!none && ev.animal != null)
                {
                    births++;
                    string type = ev.animal.type.Value;
                    byType[type] = byType.TryGetValue(type, out int c) ? c + 1 : 1;
                }
                if (Game1.activeClickableMenu is StardewValley.Menus.DialogueBox) Game1.activeClickableMenu = null;
                Game1.dialogueUp = false;
                Game1.messagePause = false;
            }
            var barns = new List<string>();
            foreach (Building b in Game1.getFarm().buildings)
                if (b.AllowsAnimalPregnancy() && b.GetIndoors() is AnimalHouse h)
                    barns.Add($"{b.buildingType.Value} {h.animalsThatLiveHere.Count}/{h.animalLimit.Value}");
            monitor.Log($"{Name}: births x{n} (Growing Herd owned {meta.HasUpgrade(AnimalPowers.GrowingHerd)}): {births} " +
                        $"({100.0 * births / n:F2}% of barn-birth nights; vanilla only rolls on half the quiet nights). By parent: " +
                        $"{string.Join(", ", byType.Select(kv => $"{kv.Key}={kv.Value}").DefaultIfEmpty("none"))}. Barns: {string.Join(", ", barns)}.",
                LogLevel.Info);
        }

        /// <summary>Opens every closed coop and barn animal door through the building's own toggle.</summary>
        private static void Doors(IMonitor monitor)
        {
            int opened = 0;
            foreach (Building b in Game1.getFarm().buildings)
                if (b.GetIndoors() is AnimalHouse && !b.animalDoorOpen.Value)
                {
                    b.ToggleAnimalDoor(Game1.player);
                    opened++;
                }
            monitor.Log($"{Name}: opened {opened} animal door(s).", LogLevel.Info);
        }

        /// <summary>Truffle Hog: the truffles-found stat, truffles lying on the farm, and the pigs outdoors.</summary>
        private static void Truffles(IMonitor monitor)
        {
            Farm farm = Game1.getFarm();
            int onGround = farm.objects.Values.Count(o => o.QualifiedItemId == AnimalPowers.TruffleQid);
            var pigs = farm.getAllFarmAnimals().Where(a => a.type.Value == AnimalPowers.PigType).ToList();
            int outside = pigs.Count(a => a.currentLocation == farm);
            int withProduce = pigs.Count(a => a.currentProduce.Value != null);
            monitor.Log($"{Name}: TrufflesFound {Game1.stats.TrufflesFound}, truffles on the farm {onGround}, pigs {pigs.Count} " +
                        $"(outdoors {outside}, still holding a truffle {withProduce}), time {Game1.timeOfDay}, raining {farm.IsRainingHere()}, " +
                        $"Truffle Hog owned {UpgradeChecker.HasUpgrade?.Invoke(AnimalPowers.TruffleNose) == true}.",
                LogLevel.Info);
        }

        /// <summary>Warps the farmer into the index-th coop or barn, one tile inside its door (entering a
        /// coop is what starts vanilla's hatch event for a ready incubator).</summary>
        private static void Enter(IMonitor monitor, string kind, int index)
        {
            var houses = Game1.getFarm().buildings
                .Where(b => b.GetIndoors() is AnimalHouse && b.buildingType.Value.Contains(kind, StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (index < 0 || index >= houses.Count) { monitor.Log($"{Name}: no {kind} number {index} ({houses.Count} found).", LogLevel.Warn); return; }
            GameLocation indoors = houses[index].GetIndoors();
            var door = indoors.warps.FirstOrDefault();
            int x = door?.X ?? 3, y = (door?.Y ?? 10) - 1;
            Game1.warpFarmer(indoors.NameOrUniqueName, x, y, 0);
            monitor.Log($"{Name}: warping into {houses[index].buildingType.Value} at ({x},{y}).", LogLevel.Info);
        }

        /// <summary>Fast Hatch: every incubator's egg and minutes left.</summary>
        private static void Incubators(IMonitor monitor)
        {
            foreach (Building b in Game1.getFarm().buildings)
                if (b.GetIndoors() is AnimalHouse house)
                    foreach (StardewValley.Object o in house.objects.Values)
                        if (o.bigCraftable.Value && o.GetMachineData()?.IsIncubator == true)
                            monitor.Log($"{Name}: {o.Name} in {b.buildingType.Value} ({b.tileX.Value},{b.tileY.Value}): " +
                                        $"egg {o.heldObject.Value?.QualifiedItemId ?? "none"}, MinutesUntilReady {o.MinutesUntilReady}, " +
                                        $"ready {o.heldObject.Value != null && o.MinutesUntilReady <= 0}, time {Game1.timeOfDay}.", LogLevel.Info);
        }
        /// <summary>Drops an Egg into the first empty incubator in any coop through the machine's own
        /// drop-in path (the one a click takes); MachineSpeedPatch logs the time it set.</summary>
        private static void Incubate(IMonitor monitor)
        {
            foreach (Building b in Game1.getFarm().buildings)
            {
                GameLocation indoors = b.GetIndoors();
                if (indoors == null) continue;
                foreach (StardewValley.Object o in indoors.objects.Values)
                {
                    if (!o.bigCraftable.Value || o.GetMachineData()?.IsIncubator != true || o.heldObject.Value != null) continue;
                    Item egg = ItemRegistry.Create(RegularEggQid);
                    bool ok = o.performObjectDropInAction(egg, false, Game1.player);
                    monitor.Log($"{Name}: egg into {o.Name} in {b.buildingType.Value}: accepted {ok}, MinutesUntilReady {o.MinutesUntilReady}.", LogLevel.Info);
                    return;
                }
            }
            monitor.Log($"{Name}: no empty incubator in any building.", LogLevel.Warn);
        }
    }
}
