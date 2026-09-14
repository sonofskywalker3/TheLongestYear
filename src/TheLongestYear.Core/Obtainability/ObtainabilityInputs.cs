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
    bool RequireMagicBait, int MinFishingLevel, bool IsRandom = false);

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

/// <summary>One output of a machine rule: an id or item query, "DROP_IN", or an OutputMethod (code).
/// <see cref="IsRandom"/> marks one entry of a RandomItemId list.</summary>
public sealed record MachineOutput(string? ItemId, string? Condition, string? OutputMethod, bool IsRandom = false);

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

/// <summary>One Data/FarmAnimals row. <see cref="DeluxeMinimumFriendship"/> is FarmAnimalData.DeluxeProduceMinimumFriendship.</summary>
public sealed record AnimalRow(
    string AnimalId, string House, int PurchasePrice, IReadOnlyList<AnimalProduce> Produce, IReadOnlyList<AnimalProduce> DeluxeProduce,
    int DeluxeMinimumFriendship = 200);

public sealed record PondProduct(string ItemId, int RequiredPopulation, double Chance, string? Condition, bool IsRandom = false);

/// <summary>One Data/FishPondData entry. A fish lives under the matching entry with the lowest Precedence.</summary>
public sealed record PondRow(string Id, IReadOnlyList<string> RequiredTags, int Precedence, IReadOnlyList<PondProduct> Products);

public sealed record TapRow(string TreeId, string ItemId, int DaysUntilReady, Season? Season, double Chance, string? Condition, bool IsRandom = false);

/// <summary>One Data/Objects GeodeDrops entry for a geode item.</summary>
public sealed record GeodeDropRow(string GeodeId, string ItemId, double Chance, string? Condition);
