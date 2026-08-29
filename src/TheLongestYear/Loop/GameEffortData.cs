using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using StardewModdingAPI;
using StardewValley;
using StardewValley.GameData.Buildings;
using StardewValley.GameData.Crops;
using StardewValley.GameData.FarmAnimals;
using StardewValley.GameData.FishPonds;
using StardewValley.GameData.Locations;
using StardewValley.GameData.Machines;
using StardewValley.GameData.Objects;
using StardewValley.GameData.Shops;
using StardewValley.GameData.WildTrees;
using TheLongestYear.Core;
using TheLongestYear.Core.Availability;

namespace TheLongestYear.Loop
{
    /// <summary>Reads the tables the Phase 2 effort rules need (activity-themes spec 2026-08-28)
    /// into the Core EffortData snapshot: Data/Objects (names, geode drops), Data/Monsters (drop
    /// chances), Data/Locations (artifact spots, forage), Data/Machines + Data/CraftingRecipes,
    /// Data/FarmAnimals + Data/Buildings, Data/CookingRecipes, Data/FishPondData, Data/Crops.
    /// Same contract as GameDataPools: everything is the game's OWN data, so modded content joins
    /// automatically, and a failed read degrades to a partial snapshot, never a throw.</summary>
    internal sealed class GameEffortData
    {
        private const int MonsterDropListFieldIndex = 6;
        private const int RecipeIngredientsField = 0;
        private const int RecipeOutputField = 2;
        private const int CraftingBigCraftableField = 3;
        private const int CraftingUnlockField = 4;
        private const int CookingUnlockField = 3;
        private const string BigCraftableQualifier = "(BC)";
        private const string PreviousOutputTapId = "PREVIOUS_OUTPUT_ID";

        private readonly IMonitor _monitor;

        public GameEffortData(IMonitor monitor) => _monitor = monitor;

        public EffortData Build(IReadOnlyList<string> excludedLocationMarkers)
        {
            var objects = new Dictionary<string, RawObjectEntry>(StringComparer.Ordinal);
            var geodeDrops = new List<RawGeodeDrop>();
            var monsterDrops = new List<RawMonsterDrop>();
            var artifactSpots = new List<RawArtifactSpot>();
            var forage = new List<RawSpawnEntry>();
            var machineRules = new List<RawMachineRule>();
            var machineUnlocks = new Dictionary<string, string>(StringComparer.Ordinal);
            var recipePrices = new Dictionary<string, int>(StringComparer.Ordinal);
            var animals = new List<RawFarmAnimal>();
            var buildings = new List<RawBuilding>();
            var cooking = new List<RawCookingRecipe>();
            var ponds = new List<RawFishPondRule>();
            var crops = new List<RawCropGrowth>();
            var tapItems = new List<RawTapItem>();
            var cookingChannel = new Dictionary<string, int>(StringComparer.Ordinal);

            try
            {
                foreach (var kv in Game1.content.Load<Dictionary<string, ObjectData>>("Data/Objects"))
                {
                    ObjectData o = kv.Value;
                    if (o == null) continue;
                    objects[kv.Key] = new RawObjectEntry(
                        o.Type ?? "", o.Category, o.Price, o.ExcludeFromRandomSale,
                        (IReadOnlyList<string>)(o.ContextTags ?? new List<string>()), o.Name ?? "");
                    string geodeId = BundleParsing.NormalizeItemId(kv.Key);
                    if (o.GeodeDropsDefaultItems)
                        geodeDrops.AddRange(GeodeAvailability.DefaultTableDrops(geodeId));
                    foreach (ObjectGeodeDropData drop in o.GeodeDrops ?? new List<ObjectGeodeDropData>())
                        foreach (string id in SpawnIds(drop?.ItemId, drop?.RandomItemId))
                            geodeDrops.Add(new RawGeodeDrop(geodeId, id, drop.Chance));
                }

                foreach (var kv in Game1.content.Load<Dictionary<string, string>>("Data/Monsters"))
                {
                    string[] fields = (kv.Value ?? "").Split('/');
                    if (fields.Length <= MonsterDropListFieldIndex) continue;
                    string[] pairs = fields[MonsterDropListFieldIndex].Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    for (int i = 0; i + 1 < pairs.Length; i += 2)
                    {
                        if (!double.TryParse(pairs[i + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out double chance))
                            continue;
                        foreach (string id in ItemQueryIds.Expand(pairs[i]))
                            monsterDrops.Add(new RawMonsterDrop(kv.Key, id, chance));
                    }
                }

                foreach (var kv in Game1.content.Load<Dictionary<string, LocationData>>("Data/Locations"))
                {
                    LocationData loc = kv.Value;
                    if (loc == null) continue;
                    foreach (ArtifactSpotDropData spot in loc.ArtifactSpots ?? new List<ArtifactSpotDropData>())
                        foreach (string id in SpawnIds(spot?.ItemId, spot?.RandomItemId))
                            artifactSpots.Add(new RawArtifactSpot(kv.Key, id, spot.Chance));
                    if (ItemPoolBuilder.IsExcludedLocation(kv.Key, excludedLocationMarkers))
                        continue;
                    foreach (SpawnForageData f in loc.Forage ?? new List<SpawnForageData>())
                        foreach (string id in SpawnIds(f?.ItemId, f?.RandomItemId))
                            forage.Add(new RawSpawnEntry(id, MapSeason(f.Season), f.Condition, kv.Key));
                }

                foreach (var kv in Game1.content.Load<Dictionary<string, MachineData>>("Data/Machines"))
                {
                    foreach (MachineOutputRule rule in kv.Value?.OutputRules ?? new List<MachineOutputRule>())
                    {
                        var outputs = (rule.OutputItem ?? new List<MachineItemOutput>())
                            .SelectMany(o => SpawnIds(o?.ItemId, o?.RandomItemId))
                            .Distinct(StringComparer.Ordinal)
                            .ToList();
                        if (outputs.Count == 0) continue;
                        var triggers = rule.Triggers ?? new List<MachineOutputTriggerRule>();
                        if (triggers.Count == 0)
                            triggers = new List<MachineOutputTriggerRule> { new MachineOutputTriggerRule() };
                        foreach (MachineOutputTriggerRule trigger in triggers)
                            machineRules.Add(new RawMachineRule(
                                kv.Key, trigger?.RequiredItemId,
                                (IReadOnlyList<string>)(trigger?.RequiredTags ?? new List<string>()),
                                outputs, rule.MinutesUntilReady, rule.DaysUntilReady));
                    }
                }

                foreach (var kv in Game1.content.Load<Dictionary<string, string>>("Data/CraftingRecipes"))
                {
                    string[] fields = (kv.Value ?? "").Split('/');
                    if (fields.Length <= CraftingUnlockField) continue;
                    if (!string.Equals(fields[CraftingBigCraftableField].Trim(), "true", StringComparison.OrdinalIgnoreCase))
                        continue;
                    string outputId = fields[RecipeOutputField].Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
                    if (string.IsNullOrEmpty(outputId)) continue;
                    string machineId = outputId.StartsWith("(", StringComparison.Ordinal) ? outputId : BigCraftableQualifier + outputId;
                    machineUnlocks[machineId] = fields[CraftingUnlockField].Trim();
                }

                // Recipe shop rows. A row with a Condition is stocked only when that game-state
                // query passes (a heart level, a quest, a year), so its price is not a price the
                // player can count on and it must not become an item's shop-price week. The rest
                // dedupe by the CHEAPEST price across shops, not the first row seen: the same
                // recipe sells at different prices in different shops and the cheapest is the one
                // that decides when a player can afford it.
                int skippedConditionRecipeRows = 0;
                foreach (var kv in Game1.content.Load<Dictionary<string, ShopData>>("Data/Shops"))
                {
                    foreach (ShopItemData item in kv.Value?.Items ?? new List<ShopItemData>())
                    {
                        if (item == null || !item.IsRecipe || item.Price <= 0 || string.IsNullOrEmpty(item.ItemId)) continue;
                        if (!string.IsNullOrWhiteSpace(item.Condition)) { skippedConditionRecipeRows++; continue; }
                        string key = BundleParsing.NormalizeItemId(item.ItemId);
                        recipePrices[key] = recipePrices.TryGetValue(key, out int existing)
                            ? Math.Min(existing, item.Price)
                            : item.Price;
                    }
                }
                if (skippedConditionRecipeRows > 0)
                    _monitor?.Log(
                        $"Recipe shop rows skipped for having a Condition (not reliably stocked): {skippedConditionRecipeRows}.",
                        LogLevel.Trace);

                foreach (var kv in Game1.content.Load<Dictionary<string, string>>("Data/CookingRecipes"))
                {
                    string[] fields = (kv.Value ?? "").Split('/');
                    if (fields.Length <= CookingUnlockField) continue;
                    string[] ingredientPairs = fields[RecipeIngredientsField].Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    var ingredients = new List<string>();
                    for (int i = 0; i + 1 < ingredientPairs.Length; i += 2)
                        ingredients.Add(int.TryParse(ingredientPairs[i], out int n) && n < 0
                            ? ingredientPairs[i]
                            : BundleParsing.NormalizeItemId(ingredientPairs[i]));
                    string output = fields[RecipeOutputField].Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
                    if (string.IsNullOrEmpty(output)) continue;
                    cooking.Add(new RawCookingRecipe(kv.Key, ingredients, BundleParsing.NormalizeItemId(output), fields[CookingUnlockField].Trim()));
                }

                foreach (var kv in Game1.content.Load<Dictionary<string, FarmAnimalData>>("Data/FarmAnimals"))
                {
                    FarmAnimalData a = kv.Value;
                    if (a == null) continue;
                    animals.Add(new RawFarmAnimal(
                        kv.Key, string.IsNullOrEmpty(a.RequiredBuilding) ? (a.House ?? "") : a.RequiredBuilding,
                        a.PurchasePrice, a.DaysToProduce,
                        ProduceIds(a.ProduceItemIds), ProduceIds(a.DeluxeProduceItemIds)));
                }

                foreach (var kv in Game1.content.Load<Dictionary<string, BuildingData>>("Data/Buildings"))
                    buildings.Add(new RawBuilding(kv.Key, kv.Value?.BuildingToUpgrade));

                foreach (FishPondData pond in Game1.content.Load<List<FishPondData>>("Data/FishPondData") ?? new List<FishPondData>())
                {
                    if (pond == null) continue;
                    var products = new List<RawFishPondProduct>();
                    foreach (FishPondReward reward in pond.ProducedItems ?? new List<FishPondReward>())
                        foreach (string id in SpawnIds(reward?.ItemId, reward?.RandomItemId))
                            products.Add(new RawFishPondProduct(id, reward.RequiredPopulation));
                    ponds.Add(new RawFishPondRule((IReadOnlyList<string>)(pond.RequiredTags ?? new List<string>()), products));
                }

                foreach (var kv in Game1.content.Load<Dictionary<string, CropData>>("Data/Crops"))
                {
                    CropData c = kv.Value;
                    if (c?.HarvestItemId == null) continue;
                    // StardewValley.Season and Core.Season share Spring=0..Winter=3.
                    var seasons = (c.Seasons ?? new List<StardewValley.Season>())
                        .Select(season => (TheLongestYear.Core.Season)(int)season).Distinct().ToList();
                    crops.Add(new RawCropGrowth(
                        BundleParsing.NormalizeItemId(c.HarvestItemId),
                        (c.DaysInPhase ?? new List<int>()).Sum(), c.RegrowDays > 0, c.IsRaised, seasons));
                }

                foreach (var kv in Game1.content.Load<Dictionary<string, WildTreeData>>("Data/WildTrees"))
                {
                    foreach (WildTreeTapItemData tap in kv.Value?.TapItems ?? new List<WildTreeTapItemData>())
                    {
                        if (tap == null || string.IsNullOrEmpty(tap.ItemId)) continue;
                        if (tap.ItemId == PreviousOutputTapId || tap.ItemId.Contains(' ')) continue;
                        tapItems.Add(new RawTapItem(kv.Key, BundleParsing.NormalizeItemId(tap.ItemId), tap.DaysUntilReady));
                    }
                }

                foreach (var kv in Game1.content.Load<Dictionary<string, string>>("Data/TV/CookingChannel"))
                {
                    if (!int.TryParse(kv.Key, out int episode)) continue;
                    string name = (kv.Value ?? "").Split('/', StringSplitOptions.None).FirstOrDefault() ?? "";
                    if (string.IsNullOrEmpty(name)) continue;
                    cookingChannel[name] = episode;
                }
            }
            catch (Exception ex)
            {
                _monitor?.Log(
                    $"GameEffortData: data read failed ({ex.GetType().Name}: {ex.Message}); the effort model is partial.",
                    LogLevel.Warn);
            }

            _monitor?.Log(
                $"GameEffortData: objects {objects.Count}, geode drops {geodeDrops.Count}, monster drops {monsterDrops.Count}, "
                + $"artifact spots {artifactSpots.Count}, forage {forage.Count}, machine rules {machineRules.Count}, "
                + $"machine recipes {machineUnlocks.Count}, recipe prices {recipePrices.Count}, animals {animals.Count}, "
                + $"buildings {buildings.Count}, cooking recipes {cooking.Count}, ponds {ponds.Count}, crops {crops.Count}, "
                + $"tap items {tapItems.Count}, cooking channel episodes {cookingChannel.Count}.",
                LogLevel.Trace);

            return new EffortData
            {
                Objects = objects, GeodeDrops = geodeDrops, MonsterDrops = monsterDrops,
                ArtifactSpots = artifactSpots, ForageSpawns = forage, MachineRules = machineRules,
                MachineUnlocks = machineUnlocks, RecipePrices = recipePrices, Animals = animals, Buildings = buildings,
                CookingRecipes = cooking, FishPonds = ponds, Crops = crops, TapItems = tapItems,
                CookingChannel = cookingChannel,
            };
        }

        private static IReadOnlyList<string> ProduceIds(List<FarmAnimalProduce> produce)
            => (produce ?? new List<FarmAnimalProduce>())
                .Where(p => p != null && !string.IsNullOrEmpty(p.ItemId))
                .Select(p => BundleParsing.NormalizeItemId(p.ItemId))
                .ToList();

        /// <summary>Object ids from an item id or query plus its RandomItemId list.</summary>
        private static IEnumerable<string> SpawnIds(string itemId, List<string> randomItemId)
        {
            foreach (string id in ItemQueryIds.Expand(itemId))
                yield return id;
            foreach (string raw in randomItemId ?? new List<string>())
                foreach (string id in ItemQueryIds.Expand(raw))
                    yield return id;
        }

        private static Core.Season? MapSeason(StardewValley.Season? season)
            => season == null ? null : Enum.Parse<Core.Season>(season.Value.ToString(), ignoreCase: true);
    }
}
