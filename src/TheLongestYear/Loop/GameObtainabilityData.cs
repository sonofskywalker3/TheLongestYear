using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using StardewModdingAPI;
using StardewValley;
using StardewValley.GameData;
using StardewValley.GameData.Crops;
using StardewValley.GameData.FarmAnimals;
using StardewValley.GameData.FishPonds;
using StardewValley.GameData.FruitTrees;
using StardewValley.GameData.GarbageCans;
using StardewValley.GameData.Locations;
using StardewValley.GameData.Machines;
using StardewValley.GameData.Objects;
using StardewValley.GameData.Shops;
using StardewValley.GameData.WildTrees;
using TheLongestYear.Core;
using TheLongestYear.Core.Obtainability;
using CoreSeason = TheLongestYear.Core.Season;

namespace TheLongestYear.Loop
{
    /// <summary>Reads the live game data (including other mods' edits) into the obtainability model's
    /// input records (spec 2026-09-14-item-obtainability). Phase 1 is blind: this class reads Data
    /// assets only and never consults the mod's own item tables.</summary>
    internal sealed class GameObtainabilityData
    {
        private const int FishDifficultyField = 1;
        private const int FishTimeField = 5;
        private const int FishWeatherField = 7;
        private const int FishMinLevelField = 12;
        private const string TrapMarker = "trap";
        private const int MonsterDropField = 6;
        private const int RecipeIngredientsField = 0;
        private const int RecipeOutputField = 2;
        private const int CookingUnlockField = 3;
        private const int CraftingBigCraftableField = 3;
        private const int CraftingUnlockField = 4;
        private const string PreviousOutputTapId = "PREVIOUS_OUTPUT_ID";
        private const string AnyCan = "*";

        private readonly IMonitor _monitor;

        public GameObtainabilityData(IMonitor monitor) => _monitor = monitor;

        public ObtainabilityInputs Build()
        {
            var objects = new Dictionary<string, ObjInfo>(StringComparer.Ordinal);
            var geodeDrops = new List<GeodeDropRow>();
            var defaultGeodes = new List<string>();
            var festivals = new Dictionary<string, FestivalDates>(StringComparer.Ordinal);
            var forage = new List<LocationSpawn>();
            var fish = new List<LocationSpawn>();
            var artifactSpots = new List<ArtifactSpotRow>();
            var fishRows = new Dictionary<string, FishRow>(StringComparer.Ordinal);
            var monsterDrops = new List<MonsterDropRow>();
            var crops = new List<CropRow>();
            var fruitTrees = new List<FruitTreeRow>();
            var shops = new List<ShopRow>();
            var machines = new List<MachineRow>();
            var recipes = new List<RecipeRow>();
            var animals = new List<AnimalRow>();
            var ponds = new List<PondRow>();
            var taps = new List<TapRow>();
            var garbage = new List<GarbageRow>();

            Section("Objects", () =>
            {
                foreach (var kv in Game1.content.Load<Dictionary<string, ObjectData>>("Data/Objects"))
                {
                    ObjectData o = kv.Value;
                    if (o == null) continue;
                    string id = BundleParsing.NormalizeItemId(kv.Key);
                    var tags = ItemContextTagManager.GetBaseContextTags(id)?.ToList() ?? new List<string>();
                    objects[id] = new ObjInfo(id, o.Name ?? "", o.Category, o.Price, tags, o.ExcludeFromRandomSale);
                    if (o.GeodeDropsDefaultItems) defaultGeodes.Add(id);
                    foreach (ObjectGeodeDropData drop in o.GeodeDrops ?? new List<ObjectGeodeDropData>())
                        foreach ((string item, _) in Entries(drop?.ItemId, drop?.RandomItemId))
                            geodeDrops.Add(new GeodeDropRow(id, item, drop.Chance, drop.Condition));   // geode contents are chance already
                }
            });

            Section("PassiveFestivals", () =>
            {
                foreach (var kv in Game1.content.Load<Dictionary<string, PassiveFestivalData>>("Data/PassiveFestivals"))
                    if (kv.Value != null)
                        festivals[kv.Key] = new FestivalDates(kv.Key, (CoreSeason)(int)kv.Value.Season, kv.Value.StartDay, kv.Value.EndDay);
            });

            Section("Festivals/FestivalDates", () =>
            {
                // Keys are "<season><day>" ("spring13"); the value is a display name and is not used.
                foreach (string key in Game1.content.Load<Dictionary<string, string>>("Data/Festivals/FestivalDates").Keys)
                {
                    string seasonName = new string(key.TakeWhile(char.IsLetter).ToArray());
                    if (Enum.TryParse(seasonName, ignoreCase: true, out CoreSeason season)
                        && int.TryParse(key.Substring(seasonName.Length), out int day))
                        festivals[key] = new FestivalDates(key, season, day, day);
                }
            });

            Section("Locations", () =>
            {
                foreach (var kv in Game1.content.Load<Dictionary<string, LocationData>>("Data/Locations"))
                {
                    LocationData loc = kv.Value;
                    if (loc == null) continue;
                    foreach (SpawnForageData f in loc.Forage ?? new List<SpawnForageData>())
                        foreach ((string item, bool random) in Entries(f?.ItemId, f?.RandomItemId))
                            forage.Add(new LocationSpawn(kv.Key, item, MapSeason(f.Season), f.Condition, f.Chance, 0, false, 0, random));
                    foreach (SpawnFishData f in loc.Fish ?? new List<SpawnFishData>())
                        foreach ((string item, bool random) in Entries(f?.ItemId, f?.RandomItemId))
                            fish.Add(new LocationSpawn(kv.Key, item, MapSeason(f.Season), f.Condition, f.Chance,
                                Math.Max(0, f.CatchLimit), f.RequireMagicBait, f.MinFishingLevel, random)); // CatchLimit defaults to -1
                    foreach (ArtifactSpotDropData a in loc.ArtifactSpots ?? new List<ArtifactSpotDropData>())
                        foreach ((string item, _) in Entries(a?.ItemId, a?.RandomItemId))   // artifact spot drops are chance already
                            artifactSpots.Add(new ArtifactSpotRow(kv.Key, item, a.Condition, a.Chance));
                }
            });

            Section("Fish", () =>
            {
                foreach (var kv in Game1.content.Load<Dictionary<string, string>>("Data/Fish"))
                {
                    string[] fields = (kv.Value ?? "").Split('/');
                    string id = BundleParsing.NormalizeItemId(kv.Key);
                    bool trap = Field(fields, FishDifficultyField) == TrapMarker;
                    int level = trap ? 0 : (int.TryParse(Field(fields, FishMinLevelField), out int l) ? l : 0);
                    fishRows[id] = new FishRow(id, trap, trap ? "" : Field(fields, FishWeatherField), level,
                        trap ? "" : Field(fields, FishTimeField));
                }
            });

            Section("Monsters", () =>
            {
                foreach (var kv in Game1.content.Load<Dictionary<string, string>>("Data/Monsters"))
                {
                    string[] pairs = Field((kv.Value ?? "").Split('/'), MonsterDropField).Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    for (int i = 0; i + 1 < pairs.Length; i += 2)
                        if (double.TryParse(pairs[i + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out double chance))
                            monsterDrops.Add(new MonsterDropRow(kv.Key, pairs[i], chance));
                }
            });

            Section("Crops", () =>
            {
                foreach (var kv in Game1.content.Load<Dictionary<string, CropData>>("Data/Crops"))
                {
                    CropData c = kv.Value;
                    if (c?.HarvestItemId == null) continue;
                    crops.Add(new CropRow(BundleParsing.NormalizeItemId(kv.Key), BundleParsing.NormalizeItemId(c.HarvestItemId),
                        MapSeasons(c.Seasons), (c.DaysInPhase ?? new List<int>()).Sum(), c.RegrowDays));
                }
            });

            Section("FruitTrees", () =>
            {
                foreach (var kv in Game1.content.Load<Dictionary<string, FruitTreeData>>("Data/FruitTrees"))
                {
                    if (kv.Value == null) continue;
                    var fruit = new List<FruitRow>();
                    foreach (FruitTreeFruitData f in kv.Value.Fruit ?? new List<FruitTreeFruitData>())
                        foreach ((string item, bool random) in Entries(f?.ItemId, f?.RandomItemId))
                            fruit.Add(new FruitRow(item, MapSeason(f.Season), f.Chance, f.Condition, random));   // may be a query; Core emits it
                    fruitTrees.Add(new FruitTreeRow(BundleParsing.NormalizeItemId(kv.Key), MapSeasons(kv.Value.Seasons), fruit));
                }
            });

            Section("Shops", () =>
            {
                foreach (var kv in Game1.content.Load<Dictionary<string, ShopData>>("Data/Shops"))
                    foreach (ShopItemData item in kv.Value?.Items ?? new List<ShopItemData>())
                        foreach ((string entry, bool random) in Entries(item?.ItemId, item?.RandomItemId))
                            shops.Add(new ShopRow(kv.Key, entry, item.Condition, item.IsRecipe, random,
                                string.IsNullOrWhiteSpace(item.TradeItemId) ? null : item.TradeItemId));
            });

            Section("Machines", () =>
            {
                foreach (var kv in Game1.content.Load<Dictionary<string, MachineData>>("Data/Machines"))
                    foreach (MachineOutputRule rule in kv.Value?.OutputRules ?? new List<MachineOutputRule>())
                    {
                        var outputs = new List<MachineOutput>();
                        foreach (MachineItemOutput o in rule.OutputItem ?? new List<MachineItemOutput>())
                        {
                            if (o == null) continue;
                            if (!string.IsNullOrWhiteSpace(o.OutputMethod)) { outputs.Add(new MachineOutput(null, o.Condition, o.OutputMethod)); continue; }
                            foreach ((string entry, bool random) in Entries(o.ItemId, o.RandomItemId))
                                outputs.Add(new MachineOutput(entry, o.Condition, null, random));
                        }
                        if (outputs.Count == 0) continue;
                        // A rule with no triggers never fires; nothing is synthesized for it.
                        foreach (MachineOutputTriggerRule t in rule.Triggers ?? new List<MachineOutputTriggerRule>())
                        {
                            if (t == null || t.Trigger == MachineOutputTrigger.None) continue;
                            bool noInput = string.IsNullOrEmpty(t.RequiredItemId) && (t.RequiredTags == null || t.RequiredTags.Count == 0);
                            // "Any item placed in" has no id or tags to read; skipping it keeps it from
                            // reading as a machine that needs no input (a known limitation).
                            if (noInput && t.Trigger.HasFlag(MachineOutputTrigger.ItemPlacedInMachine)) continue;
                            machines.Add(new MachineRow(kv.Key,
                                string.IsNullOrEmpty(t.RequiredItemId) ? null : t.RequiredItemId,
                                (IReadOnlyList<string>)(t.RequiredTags ?? new List<string>()),
                                t.Condition, outputs, rule.MinutesUntilReady, rule.DaysUntilReady, rule.UseFirstValidOutput));
                        }
                    }
            });

            Section("CookingRecipes", () =>
            {
                foreach (var kv in Game1.content.Load<Dictionary<string, string>>("Data/CookingRecipes"))
                    if (Recipe(kv.Key, kv.Value, cooking: true) is RecipeRow r) recipes.Add(r);
            });

            Section("CraftingRecipes", () =>
            {
                foreach (var kv in Game1.content.Load<Dictionary<string, string>>("Data/CraftingRecipes"))
                    if (Recipe(kv.Key, kv.Value, cooking: false) is RecipeRow r) recipes.Add(r);
            });

            Section("FarmAnimals", () =>
            {
                foreach (var kv in Game1.content.Load<Dictionary<string, FarmAnimalData>>("Data/FarmAnimals"))
                {
                    FarmAnimalData a = kv.Value;
                    if (a == null) continue;
                    animals.Add(new AnimalRow(kv.Key, string.IsNullOrEmpty(a.RequiredBuilding) ? (a.House ?? "") : a.RequiredBuilding,
                        a.PurchasePrice, Produce(a.ProduceItemIds), Produce(a.DeluxeProduceItemIds), a.DeluxeProduceMinimumFriendship));
                }
            });

            Section("FishPondData", () =>
            {
                foreach (FishPondData pond in Game1.content.Load<List<FishPondData>>("Data/FishPondData") ?? new List<FishPondData>())
                {
                    if (pond == null) continue;
                    var products = new List<PondProduct>();
                    // FishPondReward has only ItemId, RequiredPopulation, Chance and quantities: no condition, no random list.
                    foreach (FishPondReward reward in pond.ProducedItems ?? new List<FishPondReward>())
                        if (!string.IsNullOrWhiteSpace(reward?.ItemId))
                            products.Add(new PondProduct(reward.ItemId.Trim(), reward.RequiredPopulation, reward.Chance, null));
                    ponds.Add(new PondRow(pond.Id ?? "", (IReadOnlyList<string>)(pond.RequiredTags ?? new List<string>()), pond.Precedence, products));
                }
            });

            Section("WildTrees", () =>
            {
                foreach (var kv in Game1.content.Load<Dictionary<string, WildTreeData>>("Data/WildTrees"))
                    foreach (WildTreeTapItemData tap in kv.Value?.TapItems ?? new List<WildTreeTapItemData>())
                        foreach ((string item, bool random) in Entries(tap?.ItemId, tap?.RandomItemId))
                            if (item != PreviousOutputTapId)
                                taps.Add(new TapRow(kv.Key, item, tap.DaysUntilReady, MapSeason(tap.Season), tap.Chance, tap.Condition, random));
            });

            Section("GarbageCans", () =>
            {
                GarbageCanData data = Game1.content.Load<GarbageCanData>("Data/GarbageCans");
                void AddAll(string can, List<GarbageCanItemData> items)
                {
                    foreach (GarbageCanItemData g in items ?? new List<GarbageCanItemData>())
                        foreach ((string item, _) in Entries(g?.ItemId, g?.RandomItemId))   // garbage is chance already
                            garbage.Add(new GarbageRow(can, item, g.Condition));
                }
                AddAll(AnyCan, data?.BeforeAll);
                AddAll(AnyCan, data?.AfterAll);
                foreach (var kv in data?.GarbageCans ?? new Dictionary<string, GarbageCanEntryData>())
                    AddAll(kv.Key, kv.Value?.Items);
            });

            return new ObtainabilityInputs
            {
                Objects = objects, Festivals = festivals, Forage = forage, LocationFish = fish, FishRows = fishRows,
                ArtifactSpots = artifactSpots, Garbage = garbage, Shops = shops, MonsterDrops = monsterDrops,
                Crops = crops, FruitTrees = fruitTrees, Machines = machines, Recipes = recipes, Animals = animals,
                Ponds = ponds, TapItems = taps, GeodeDrops = geodeDrops, GeodesUsingDefaultTable = defaultGeodes,
            };
        }

        private void Section(string asset, Action read)
        {
            try { read(); }
            catch (Exception ex)
            {
                _monitor?.Log($"Obtainability: reading Data/{asset} failed ({ex.GetType().Name}: {ex.Message}); that part of the model is missing.", LogLevel.Warn);
            }
        }

        private static RecipeRow Recipe(string name, string row, bool cooking)
        {
            string[] fields = (row ?? "").Split('/');
            int unlockField = cooking ? CookingUnlockField : CraftingUnlockField;
            if (fields.Length <= unlockField) return null;
            string[] ingredientPairs = fields[RecipeIngredientsField].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var ingredients = new List<string>();
            for (int i = 0; i + 1 < ingredientPairs.Length; i += 2)
                ingredients.Add(int.TryParse(ingredientPairs[i], out int n) && n < 0 ? ingredientPairs[i] : BundleParsing.NormalizeItemId(ingredientPairs[i]));
            // The output field is "id count id count ..."; with several ids the game picks one at random
            // each craft (CraftingRecipe.cs 127-131, 192).
            string[] outputPairs = fields[RecipeOutputField].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            bool bigCraftable = !cooking && string.Equals(fields[CraftingBigCraftableField].Trim(), "true", StringComparison.OrdinalIgnoreCase);
            var outputIds = new List<string>();
            for (int i = 0; i < outputPairs.Length; i += 2)
                outputIds.Add(bigCraftable && !outputPairs[i].StartsWith("(", StringComparison.Ordinal) ? "(BC)" + outputPairs[i] : BundleParsing.NormalizeItemId(outputPairs[i]));
            if (outputIds.Count == 0) return null;
            return new RecipeRow(name, ingredients, outputIds[0], fields[unlockField].Trim(), cooking,
                outputIds.Count > 1 ? outputIds.Skip(1).ToList() : null);
        }

        private static IReadOnlyList<AnimalProduce> Produce(List<FarmAnimalProduce> produce)
            => (produce ?? new List<FarmAnimalProduce>())
                .Where(p => !string.IsNullOrEmpty(p?.ItemId))
                .Select(p => new AnimalProduce(BundleParsing.NormalizeItemId(p.ItemId), p.Condition, p.MinimumFriendship)).ToList();

        /// <summary>What a spawn entry can give, ids and item queries alike. A non-empty RandomItemId replaces
        /// ItemId (ItemQueryResolver.cs 804-817) and one entry is picked, so each is random when there are
        /// several; otherwise the ItemId is the one fixed result.</summary>
        private static IEnumerable<(string Id, bool IsRandom)> Entries(string itemId, List<string> randomItemIds)
        {
            List<string> random = (randomItemIds ?? new List<string>())
                .Where(id => !string.IsNullOrWhiteSpace(id)).Select(id => id.Trim()).ToList();
            if (random.Count > 0)
            {
                foreach (string id in random) yield return (id, random.Count > 1);
                yield break;
            }
            if (!string.IsNullOrWhiteSpace(itemId)) yield return (itemId.Trim(), false);
        }

        private static string Field(string[] fields, int index) => index < fields.Length ? fields[index] : "";

        private static CoreSeason? MapSeason(StardewValley.Season? season)
            => season is StardewValley.Season s ? (CoreSeason)(int)s : null;

        private static IReadOnlyList<CoreSeason> MapSeasons(List<StardewValley.Season> seasons)
            => (seasons ?? new List<StardewValley.Season>()).Select(s => (CoreSeason)(int)s).Distinct().ToList();
    }
}
