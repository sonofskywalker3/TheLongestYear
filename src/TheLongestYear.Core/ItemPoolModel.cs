using System.Collections.Generic;

namespace TheLongestYear.Core;

/// <summary>One candidate item of a generation pool. Seasons empty = obtainable in every
/// season; Locations populated only for fish (spawn locations, used to keep a fish
/// bundle's habitat identity when re-rolling). Ids are QUALIFIED ("(O)24").</summary>
public sealed record PoolItem(
    string ItemId,
    int Price,
    int Weight,
    IReadOnlyList<Season> Seasons,
    IReadOnlyList<string> Locations);

/// <summary>The per-domain candidate pools for one generation, derived from the game's
/// own data tables at generation time (SVE-proof by construction — spec). Lists are
/// ordinal-ordered by ItemId so seeded sampling is deterministic across runs.</summary>
public sealed class ItemPools
{
    public IReadOnlyList<PoolItem> Crops { get; init; } = new List<PoolItem>();
    public IReadOnlyList<PoolItem> Fish { get; init; } = new List<PoolItem>();
    public IReadOnlyList<PoolItem> CrabPot { get; init; } = new List<PoolItem>();
    public IReadOnlyList<PoolItem> Forage { get; init; } = new List<PoolItem>();
    public IReadOnlyList<PoolItem> MonsterDrops { get; init; } = new List<PoolItem>();
    public IReadOnlyList<PoolItem> Metals { get; init; } = new List<PoolItem>();
    public IReadOnlyList<PoolItem> ArtisanGoods { get; init; } = new List<PoolItem>();
    public IReadOnlyList<PoolItem> Artifacts { get; init; } = new List<PoolItem>();
    public IReadOnlyList<PoolItem> Books { get; init; } = new List<PoolItem>();
    public IReadOnlyList<PoolItem> Saplings { get; init; } = new List<PoolItem>();
    public IReadOnlyList<PoolItem> GeodeMinerals { get; init; } = new List<PoolItem>();
    public IReadOnlyList<PoolItem> Cooking { get; init; } = new List<PoolItem>();
    public IReadOnlyList<PoolItem> TapperGoods { get; init; } = new List<PoolItem>();

    /// <summary>Item -> earliest obtainable season, for every pool item whose earliest
    /// season is later than Spring. Feeds the season-gate ramp clamp so re-rolled
    /// bundles can never be demanded before their items exist (spec safety rule).</summary>
    public IReadOnlyDictionary<string, Season> DerivedSeasonPins { get; init; }
        = new Dictionary<string, Season>();
}

/// <summary>Which item pool a picked bundle's slots re-roll from. None = keep the
/// bundle's vanilla slots (safe default; SlotTrimmer still applies).</summary>
public enum PoolDomain
{
    None, SeasonalCrops, QualityCrops, Fish, CrabPot, SeasonalForage,
    MonsterDrops, Metals, ArtisanGoods,
}

/// <summary>A classified bundle: its domain plus the season filter for seasonal domains
/// (null = no season restriction).</summary>
public sealed record DomainMatch(PoolDomain Domain, Season? Season);

// ---- Raw boundary records: glue (GameDataPools) fills these from live game assets so
// ---- Core never references Game1/SMAPI types and the builder is fully unit-testable
// ---- (review-carried pool-provider-tests requirement).

/// <summary>Data/Objects entry essentials (key of the containing dictionary is the
/// UNQUALIFIED object id).</summary>
public sealed record RawObjectEntry(
    string Type, int Category, int Price, bool ExcludeFromRandomSale,
    IReadOnlyList<string> ContextTags);

/// <summary>Data/Crops entry: what harvesting yields + which seasons it grows in
/// (empty = any season, mirroring CropData.Seasons' empty default).</summary>
public sealed record RawCropEntry(string HarvestItemId, IReadOnlyList<Season> Seasons);

/// <summary>One Data/Locations spawn entry (LocationData.Forage or LocationData.Fish):
/// Season null = any season unless the Condition string names seasons.</summary>
public sealed record RawSpawnEntry(string ItemId, Season? Season, string? Condition, string Location);

/// <summary>One monster drop-table item (Data/Monsters field 6; chance not needed —
/// pools care about obtainability, not drop rate).</summary>
public sealed record RawMonsterDropEntry(string ItemId);

/// <summary>One Data/FruitTrees entry's sapling item id (unqualified) — feeds the
/// Saplings pool. Fruit tree saplings are shop items, not seasonal spawns, so no
/// season data travels with this record (empty/any season).</summary>
public sealed record RawFruitTreeEntry(string SaplingItemId);

/// <summary>One geode drop-table item id (unqualified) — feeds the GeodeMinerals pool,
/// merged with a curated default-mineral list (the vanilla default geode table is code,
/// not data) and then filtered to exclude gem-category items.</summary>
public sealed record RawGeodeDropEntry(string ItemId);
