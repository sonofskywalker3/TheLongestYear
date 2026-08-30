using System.Collections.Generic;

namespace TheLongestYear.Core.Availability;

// ---- Raw boundary records for the Phase 2 effort rules. Ids are QUALIFIED ("(O)24"); the
// ---- glue (Loop/GameEffortData) normalises them. Objects are keyed by BARE id like the pools.

/// <summary>One Data/Objects GeodeDrops row (or one row of the code-only default table).</summary>
public sealed record RawGeodeDrop(string GeodeItemId, string ItemId, double Chance);

/// <summary>One Data/Monsters drop-table pair, with the monster it belongs to.</summary>
public sealed record RawMonsterDrop(string MonsterName, string ItemId, double Chance);

/// <summary>One Data/Locations ArtifactSpots row, expanded to a single item id.</summary>
public sealed record RawArtifactSpot(string Location, string ItemId, double Chance);

/// <summary>One Data/Machines output rule trigger: what goes in, what comes out, how long.</summary>
public sealed record RawMachineRule(
    string MachineItemId, string? RequiredItemId, IReadOnlyList<string> RequiredTags,
    IReadOnlyList<string> OutputItemIds, int MinutesUntilReady, int DaysUntilReady);

/// <summary>One Data/FarmAnimals entry reduced to what the animal-product rule reads.</summary>
public sealed record RawFarmAnimal(
    string Name, string Building, int PurchasePrice, int DaysToProduce,
    IReadOnlyList<string> ProduceIds, IReadOnlyList<string> DeluxeProduceIds);

/// <summary>One Data/Buildings entry: its name and what it upgrades from.</summary>
public sealed record RawBuilding(string Name, string? BuildingToUpgrade);

/// <summary>One Data/CookingRecipes entry: ingredient ids (a negative id is a category ref),
/// the output id and the unlock condition field.</summary>
public sealed record RawCookingRecipe(
    string Name, IReadOnlyList<string> IngredientIds, string OutputItemId, string UnlockCondition);

/// <summary>One product a fish pond can yield and the population it needs.</summary>
public sealed record RawFishPondProduct(string ItemId, int RequiredPopulation);

/// <summary>One Data/FishPondData entry: which fish (by tags) and what they produce.</summary>
public sealed record RawFishPondRule(IReadOnlyList<string> RequiredTags, IReadOnlyList<RawFishPondProduct> Products);

/// <summary>One Data/WildTrees TapItems row: which tree, what item, how many nights.</summary>
public sealed record RawTapItem(string TreeId, string ItemId, int Days);

/// <summary>One Data/Crops entry reduced to growth facts.</summary>
public sealed record RawCropGrowth(string HarvestItemId, int GrowthDays, bool Regrows, bool Trellis, IReadOnlyList<Season> Seasons)
{
    public RawCropGrowth(string harvestItemId, int growthDays, bool regrows, bool trellis)
        : this(harvestItemId, growthDays, regrows, trellis, System.Array.Empty<Season>()) { }
}

/// <summary>Everything the effort rules read, snapshotted from the game's own data tables by the
/// mod at SaveLoaded (Loop/GameEffortData). Core never touches Game1; tests hand-build these.</summary>
public sealed class EffortData
{
    public IReadOnlyDictionary<string, RawObjectEntry> Objects { get; init; } = new Dictionary<string, RawObjectEntry>();
    public IReadOnlyList<RawGeodeDrop> GeodeDrops { get; init; } = new List<RawGeodeDrop>();
    public IReadOnlyList<RawMonsterDrop> MonsterDrops { get; init; } = new List<RawMonsterDrop>();
    public IReadOnlyList<RawArtifactSpot> ArtifactSpots { get; init; } = new List<RawArtifactSpot>();
    public IReadOnlyList<RawMachineRule> MachineRules { get; init; } = new List<RawMachineRule>();
    public IReadOnlyDictionary<string, string> MachineUnlocks { get; init; } = new Dictionary<string, string>();
    public IReadOnlyDictionary<string, int> RecipePrices { get; init; } = new Dictionary<string, int>();
    public IReadOnlyList<RawFarmAnimal> Animals { get; init; } = new List<RawFarmAnimal>();
    public IReadOnlyList<RawBuilding> Buildings { get; init; } = new List<RawBuilding>();
    public IReadOnlyList<RawCookingRecipe> CookingRecipes { get; init; } = new List<RawCookingRecipe>();
    public IReadOnlyList<RawFishPondRule> FishPonds { get; init; } = new List<RawFishPondRule>();
    public IReadOnlyList<RawCropGrowth> Crops { get; init; } = new List<RawCropGrowth>();
    public IReadOnlyList<RawSpawnEntry> ForageSpawns { get; init; } = new List<RawSpawnEntry>();

    /// <summary>Per-location daily forage spawn rates, for the yield simulator. Empty for callers
    /// that only need obtainability (the effort rules never read it).</summary>
    public IReadOnlyList<RawLocationForageRate> ForageRates { get; init; } = new List<RawLocationForageRate>();
    public IReadOnlyList<RawTapItem> TapItems { get; init; } = new List<RawTapItem>();
    /// <summary>Data/TV/CookingChannel: recipe name to episode index (1 to 32).</summary>
    public IReadOnlyDictionary<string, int> CookingChannel { get; init; } = new Dictionary<string, int>();
}
