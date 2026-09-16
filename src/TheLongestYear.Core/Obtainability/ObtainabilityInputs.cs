using System;
using System.Collections.Generic;

namespace TheLongestYear.Core.Obtainability;

// ---- Plain input records. The glue (Loop/GameObtainabilityData) fills these from the live game
// ---- data assets at save load, so every rule here is testable without the game. Item ids are
// ---- QUALIFIED ("(O)24") except category references ("-75") and item queries, which travel as
// ---- their raw text and are expanded by ItemQueries (Task 4).

/// <summary>A festival's dates: passive festivals (Data/PassiveFestivals: Night Market, Squid Fest,
/// Trout Derby, Desert Festival) and day festivals (Data/Festivals/FestivalDates: Egg Festival...).</summary>
public sealed record FestivalDates(string Id, Season Season, int StartDay, int EndDay)
{
    public WeekMask Weeks => WeekMask.ForDays(
        Calendar.DayOfYear((int)Season, StartDay), Calendar.DayOfYear((int)Season, EndDay));
}

/// <summary>Data/Objects essentials. <see cref="ContextTags"/> are the item's BASE tags as the game
/// computes them (ItemContextTagManager.GetBaseContextTags), including generated ones such as
/// "category_fruits" and "id_o_24", which machine and pond rules match on.</summary>
public sealed record ObjInfo(
    string QualifiedId, string Name, int Category, int Price, IReadOnlyList<string> ContextTags, bool ExcludeFromRandomSale);

/// <summary>One Data/Locations Forage or Fish row. <see cref="ItemId"/> may be an item query.
/// <see cref="Season"/> null means any season unless <see cref="Condition"/> names one.
/// <see cref="MinFishingLevel"/> is SpawnFishData.MinFishingLevel (0 for forage). <see cref="IsRandom"/>
/// marks one entry of a RandomItemId list: the game picks one of them (ItemQueryResolver.cs 804-817).</summary>
public sealed record LocationSpawn(
    string Location, string ItemId, Season? Season, string? Condition, double Chance, int CatchLimit,
    bool RequireMagicBait, int MinFishingLevel, bool IsRandom = false, bool CanBeInherited = true);

/// <summary>One Data/Fish row, reduced: field 1 difficulty or "trap", field 5 time spans
/// ("600 1200 1800 2000"), field 7 weather ("sunny", "rainy", "both"), field 12 minimum level.</summary>
public sealed record FishRow(string ItemId, bool IsTrap, string Weather, int MinFishingLevel, string TimeSpans);

public sealed record ArtifactSpotRow(string Location, string ItemId, string? Condition, double Chance);

/// <summary>One Data/GarbageCans item (from a can's Items, or BeforeAll/AfterAll with CanId "*").</summary>
public sealed record GarbageRow(string CanId, string ItemId, string? Condition);

/// <summary>One Data/Shops stock row, or one of its RandomItemId entries (<see cref="IsRandom"/>). <see cref="ItemId"/>
/// is an id or an item query. A recipe row teaches the recipe for that item. <see cref="TradeItemId"/> is
/// ShopItemData.TradeItemId: the item is paid for with another item.</summary>
public sealed record ShopRow(string ShopId, string ItemId, string? Condition, bool IsRecipe, bool IsRandom = false, string? TradeItemId = null);

/// <summary>One Data/Monsters drop (field 6 is "id chance id chance ..."). <see cref="ItemId"/> may be a query.</summary>
public sealed record MonsterDropRow(string Monster, string ItemId, double Chance);

/// <summary>One Data/Crops row: keyed by seed, <see cref="GrowthDays"/> is the sum of DaysInPhase,
/// <see cref="RegrowDays"/> is CropData.RegrowDays (-1 or 0 means none), <see cref="Seasons"/> empty means any season.</summary>
public sealed record CropRow(string SeedId, string HarvestId, IReadOnlyList<Season> Seasons, int GrowthDays, int RegrowDays);

/// <summary>One fruit a tree grows: its own season overrides the tree's; Chance and Condition are FruitTreeFruitData's.
/// <see cref="ItemId"/> may be an item query; <see cref="IsRandom"/> marks one entry of a RandomItemId list.</summary>
public sealed record FruitRow(string ItemId, Season? Season, double Chance, string? Condition, bool IsRandom = false);

/// <summary>One Data/FruitTrees row, keyed by sapling.</summary>
public sealed record FruitTreeRow(string SaplingId, IReadOnlyList<Season> TreeSeasons, IReadOnlyList<FruitRow> Fruit);

/// <summary>Which OutputMethod the model can read past its name: the Object.cs machine helpers the
/// Seed Maker and Mushroom Log call (Object.OutputSeedMaker 2241-2272, Object.OutputMushroomLog
/// 2274-2335), and the Cask (StardewValley.Objects/Cask.cs OutputCask 78-140), which changes quality
/// only and gives no item, so the model drops it silently.</summary>
public enum OutputMethodKind { None, SeedMaker, MushroomLog, Cask, Unknown }

/// <summary>One output of a machine rule: an id or item query, "DROP_IN", or an OutputMethod (code).
/// <see cref="IsRandom"/> marks one entry of a RandomItemId list. <see cref="Method"/> is which
/// OutputMethod the model can read past its raw name, when <see cref="OutputMethod"/> is set.</summary>
public sealed record MachineOutput(
    string? ItemId, string? Condition, string? OutputMethod, bool IsRandom = false, OutputMethodKind Method = OutputMethodKind.None);

/// <summary>One Data/Machines output rule x trigger. No required item and no tags means the machine
/// needs no input (a Bee House, a Mushroom Log). Ready time: DaysUntilReady when 0 or more, else minutes.
/// <see cref="UseFirstValidOutput"/> is MachineOutputRule.UseFirstValidOutput: outputs are tried in order,
/// not picked at random.</summary>
public sealed record MachineRow(
    string MachineId, string? RequiredItemId, IReadOnlyList<string> RequiredTags, string? TriggerCondition,
    IReadOnlyList<MachineOutput> Outputs, int MinutesUntilReady, int DaysUntilReady, bool UseFirstValidOutput = false);

/// <summary>A cooking or crafting recipe. Ingredients are qualified ids or negative category numbers.
/// <see cref="Unlock"/> is the raw unlock field ("default", "none", "Farming 3", "s Farming 3", "f Robin 7", "l 4").
/// <see cref="AlternateOutputIds"/> are the other outputs of a recipe that picks one at random (CraftingRecipe.cs 57, 127-131).</summary>
public sealed record RecipeRow(
    string Name, IReadOnlyList<string> Ingredients, string OutputId, string Unlock, bool IsCooking,
    IReadOnlyList<string>? AlternateOutputIds = null);

public sealed record AnimalProduce(string ItemId, string? Condition, int MinimumFriendship);

/// <summary>One Data/FarmAnimals row. <see cref="DeluxeMinimumFriendship"/> is FarmAnimalData.DeluxeProduceMinimumFriendship.
/// <see cref="DaysToProduce"/> is FarmAnimalData.DaysToProduce (default 1; FarmAnimal.dayUpdate 1005 checks
/// daysSinceLastLay >= DaysToProduce). <see cref="DaysToMature"/> is FarmAnimalData.DaysToMature: every
/// animal waits this long after arriving before it grows up. <see cref="IncubationDays"/> is the egg's
/// incubation time in days, for an animal that only hatches (0 when it is bought grown, or sold).
/// <see cref="SoldAsAlternate"/> is true when another animal's AlternatePurchaseTypes lists this id
/// (PurchaseAnimalsMenu.cs 477-484), so it is sold even though its own PurchasePrice is -1.</summary>
public sealed record AnimalRow(
    string AnimalId, string House, int PurchasePrice, IReadOnlyList<AnimalProduce> Produce, IReadOnlyList<AnimalProduce> DeluxeProduce,
    int DeluxeMinimumFriendship = 200, int DaysToProduce = 1,
    int DaysToMature = 0, int IncubationDays = 0, bool SoldAsAlternate = false);

public sealed record PondProduct(string ItemId, int RequiredPopulation, double Chance, string? Condition, bool IsRandom = false);

/// <summary>One Data/FishPondData entry. A fish lives under the matching entry with the lowest Precedence.
/// <see cref="SpawnTime"/> is FishPondData.SpawnTime: days between one new fish and the next, up to capacity.</summary>
public sealed record PondRow(string Id, IReadOnlyList<string> RequiredTags, int Precedence, IReadOnlyList<PondProduct> Products, int SpawnTime = 1);

public sealed record TapRow(string TreeId, string ItemId, int DaysUntilReady, Season? Season, double Chance, string? Condition, bool IsRandom = false);

/// <summary>One Data/Objects GeodeDrops entry for a geode item.</summary>
public sealed record GeodeDropRow(string GeodeId, string ItemId, double Chance, string? Condition);

/// <summary>One Data/MonsterSlayerQuests row: MonsterSlayerQuestData.Targets, Count and RewardItemId,
/// handed over at the Adventure Guild once the kill count is reached.</summary>
public sealed record SlayerQuestRow(string Id, IReadOnlyList<string> Targets, int Count, string RewardItemId);

/// <summary>Everything the builder reads, filled by the glue at save load.</summary>
public sealed record ObtainabilityInputs
{
    public IReadOnlyDictionary<string, ObjInfo> Objects { get; init; } = new Dictionary<string, ObjInfo>();
    public IReadOnlyDictionary<string, FestivalDates> Festivals { get; init; } = new Dictionary<string, FestivalDates>();
    public IReadOnlyList<LocationSpawn> Forage { get; init; } = Array.Empty<LocationSpawn>();
    public IReadOnlyList<LocationSpawn> LocationFish { get; init; } = Array.Empty<LocationSpawn>();
    public IReadOnlyDictionary<string, FishRow> FishRows { get; init; } = new Dictionary<string, FishRow>();
    public IReadOnlyList<ArtifactSpotRow> ArtifactSpots { get; init; } = Array.Empty<ArtifactSpotRow>();
    public IReadOnlyList<GarbageRow> Garbage { get; init; } = Array.Empty<GarbageRow>();
    public IReadOnlyList<ShopRow> Shops { get; init; } = Array.Empty<ShopRow>();
    public IReadOnlyList<MonsterDropRow> MonsterDrops { get; init; } = Array.Empty<MonsterDropRow>();
    public IReadOnlyList<CropRow> Crops { get; init; } = Array.Empty<CropRow>();
    public IReadOnlyList<FruitTreeRow> FruitTrees { get; init; } = Array.Empty<FruitTreeRow>();
    public IReadOnlyList<MachineRow> Machines { get; init; } = Array.Empty<MachineRow>();
    public IReadOnlyList<RecipeRow> Recipes { get; init; } = Array.Empty<RecipeRow>();
    public IReadOnlyList<AnimalRow> Animals { get; init; } = Array.Empty<AnimalRow>();
    public IReadOnlyList<PondRow> Ponds { get; init; } = Array.Empty<PondRow>();
    public IReadOnlyList<TapRow> TapItems { get; init; } = Array.Empty<TapRow>();
    public IReadOnlyList<GeodeDropRow> GeodeDrops { get; init; } = Array.Empty<GeodeDropRow>();
    public IReadOnlyCollection<string> GeodesUsingDefaultTable { get; init; } = Array.Empty<string>();
    public IReadOnlyList<SlayerQuestRow> SlayerQuests { get; init; } = Array.Empty<SlayerQuestRow>();
    /// <summary>Building name (as animal House / "Fish Pond") to BuildDays.</summary>
    public IReadOnlyDictionary<string, int> Buildings { get; init; } = new Dictionary<string, int>();
    /// <summary>Data/TV/CookingChannel: cooking recipe name (the Data/CookingRecipes key) to the
    /// episode number that teaches it. Episode k airs on the Sunday of week k (TV.cs getWeeklyRecipe
    /// 518: whichWeek = DaysPlayed % 224 / 7), so episodes 1 to 16 are year 1 and 17 to 32 year 2.</summary>
    public IReadOnlyDictionary<string, int> CookingChannel { get; init; } = new Dictionary<string, int>();
}
