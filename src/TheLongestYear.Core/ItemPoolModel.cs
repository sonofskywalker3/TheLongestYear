using System;
using System.Collections.Generic;

namespace TheLongestYear.Core;

/// <summary>One candidate item of a generation pool. Seasons empty = obtainable in every
/// season; Locations populated only for fish (spawn locations, used to keep a fish
/// bundle's habitat identity when re-rolling). Category is the Data/Objects category
/// (-4 = a real fish; 0 when unknown or hand-built). Ids are QUALIFIED ("(O)24").</summary>
public sealed record PoolItem(
    string ItemId,
    int Price,
    int Weight,
    IReadOnlyList<Season> Seasons,
    IReadOnlyList<string> Locations,
    int Category = 0);

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

    /// <summary>Qualified ids that can carry a silver/gold quality ask: crop harvests,
    /// rod-caught non-jelly fish, and spawned forage in a forage category (the game's own
    /// isForage() test). Null = no eligibility data (hand-built pools in tests): the filler
    /// then allows quality as before. Nexus 1122358 follow-ups (gold Fiber, gold River
    /// Jelly, silver Tea Leaves), 2026-08-25.</summary>
    public IReadOnlySet<string>? QualityEligibleIds { get; init; }

    /// <summary>Qualified ids of every Data/Fish "trap" row (crab-pot catches). The weekly-goal
    /// sampler allows at most one of these per theme list (Jeff, 2026-08-28).</summary>
    public IReadOnlySet<string> TrapFishIds { get; init; } = new HashSet<string>(StringComparer.Ordinal);

    /// <summary>Qualified ids of every fruit a Data/FruitTrees tree grows. The weekly-goal
    /// sampler allows at most one of these per theme list (Jeff, 2026-08-29).</summary>
    public IReadOnlySet<string> FruitTreeFruitIds { get; init; } = new HashSet<string>(StringComparer.Ordinal);

    /// <summary>Raw Data/Fish rows keyed by UNQUALIFIED item id, for the availability model.
    /// The pools themselves carry qualified ids; this table mirrors the game's own keying.</summary>
    public IReadOnlyDictionary<string, RawFishEntry> FishRows { get; init; }
        = new Dictionary<string, RawFishEntry>();
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
/// (empty = any season, mirroring CropData.Seasons' empty default). HarvestMaxQuality
/// mirrors CropData.HarvestMaxQuality: null = uncapped (vanilla default), 0 = the game
/// clamps the harvest to base quality (e.g. Fiber) so it can never carry a quality ask.</summary>
public sealed record RawCropEntry(
    string HarvestItemId, IReadOnlyList<Season> Seasons, int? HarvestMaxQuality = null);

/// <summary>One Data/Locations spawn entry (LocationData.Forage or LocationData.Fish):
/// Season null = any season unless the Condition string names seasons.</summary>
public sealed record RawSpawnEntry(string ItemId, Season? Season, string? Condition, string Location);

/// <summary>One monster drop-table item (Data/Monsters field 6; chance not needed —
/// pools care about obtainability, not drop rate).</summary>
public sealed record RawMonsterDropEntry(string ItemId);

/// <summary>One Data/FruitTrees entry's sapling item id (unqualified) — feeds the
/// Saplings pool — plus the fruit item ids the tree grows (weekly-goal fruit cap). Fruit tree saplings are shop items, not seasonal spawns, so no
/// season data travels with this record (empty/any season).</summary>
public sealed record RawFruitTreeEntry(string SaplingItemId, IReadOnlyList<string>? FruitItemIds = null);

/// <summary>One geode drop-table item id (unqualified) — feeds the GeodeMinerals pool,
/// merged with a curated default-mineral list (the vanilla default geode table is code,
/// not data) and then filtered to exclude gem-category items.</summary>
public sealed record RawGeodeDropEntry(string ItemId);

/// <summary>One Data/Fish row, reduced to the fields the availability model gates on.
///
/// Field indices verified against the decompiled Android source, GameLocation.
/// CheckGenericFishRequirements: 1 = difficulty or the literal "trap", 5 = time spans,
/// 7 = weather, 9 = max depth, 12 = minimum fishing level. Do not reorder from memory.
///
/// Parse never throws. A row the game itself would reject degrades to zeros, and the fish then
/// scores as an easy year-round catch, which is the lenient direction.</summary>
public sealed record RawFishEntry(
    string ItemId, bool IsTrap, int Difficulty, string RawTimeSpans,
    string Weather, int MaxDepth, int MinFishingLevel)
{
    private const int DifficultyIndex = 1;
    private const int TimeSpansIndex = 5;
    private const int WeatherIndex = 7;
    private const int MaxDepthIndex = 9;
    private const int MinFishingLevelIndex = 12;
    private const string TrapMarker = "trap";
    /// <summary>Game clock for 6pm: the earliest a "night" biting window may open.</summary>
    public const int NightStart = 1800;

    /// <summary>True when every biting window opens at or after <paramref name="nightStart"/>,
    /// i.e. the fish cannot be caught earlier in the day. A row with no parseable window is
    /// open all day, so false; a trap fish has no window, so false.</summary>
    public bool IsNightOnly(int nightStart = NightStart)
    {
        string[] parts = (RawTimeSpans ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
        bool anyWindow = false;
        for (int i = 0; i + 1 < parts.Length; i += 2)
        {
            if (!int.TryParse(parts[i], out int start))
                return false;
            anyWindow = true;
            if (start < nightStart)
                return false;
        }
        return anyWindow;
    }

    public static RawFishEntry Parse(string itemId, string? row)
    {
        string[] fields = (row ?? "").Split('/');
        bool isTrap = Field(fields, DifficultyIndex) == TrapMarker;
        return new RawFishEntry(
            ItemId: itemId,
            IsTrap: isTrap,
            Difficulty: isTrap ? 0 : Int(fields, DifficultyIndex),
            RawTimeSpans: isTrap ? "" : Field(fields, TimeSpansIndex),
            Weather: isTrap ? "" : Field(fields, WeatherIndex),
            MaxDepth: isTrap ? 0 : Int(fields, MaxDepthIndex),
            MinFishingLevel: isTrap ? 0 : Int(fields, MinFishingLevelIndex));
    }

    private static string Field(string[] fields, int index)
        => index < fields.Length ? fields[index] : "";

    private static int Int(string[] fields, int index)
        => int.TryParse(Field(fields, index), out int value) ? value : 0;
}
