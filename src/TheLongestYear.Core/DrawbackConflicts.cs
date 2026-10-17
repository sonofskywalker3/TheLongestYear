using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core;

/// <summary>What a weekly goal item's realistic in-week sources look like, as far as a drawback
/// is concerned. <see cref="MineOnly"/>: every known source is inside the mines (the mine fish,
/// monster drops, cave forage), so closing the mines makes it unobtainable. <see cref="ForageOnly"/>:
/// the item only ever appears as spawned forage, so turning forage off does the same.</summary>
public sealed record ItemSourceProfile(bool MineOnly, bool ForageOnly)
{
    /// <summary>An item with at least one route no drawback closes (the default for anything
    /// the index cannot place, so an unknown item never forces a redraw).</summary>
    public static readonly ItemSourceProfile Open = new(false, false);
}

/// <summary>The rule that keeps a random drawback off a week whose goals it would make
/// impossible. Two drawbacks are HARD blocks: mines_closed (nothing from the mines all week) and
/// forage_off (no forage spawns all week). Every other drawback only slows an income stream.</summary>
public static class DrawbackConflicts
{
    public const string MinesClosed = "mines_closed";
    public const string ForageOff = "forage_off";

    public static readonly IReadOnlySet<string> HardBlocking =
        new HashSet<string>(StringComparer.Ordinal) { MinesClosed, ForageOff };

    public static bool IsHard(string? liabilityId) => liabilityId != null && HardBlocking.Contains(liabilityId);

    /// <summary>True when the drawback makes an item with this source profile unobtainable in-week.</summary>
    public static bool Blocks(string? liabilityId, ItemSourceProfile? profile)
    {
        if (profile == null) return false;
        return liabilityId switch
        {
            MinesClosed => profile.MineOnly,
            ForageOff => profile.ForageOnly,
            _ => false,
        };
    }

    /// <summary>True when the drawback hard-blocks any of the goal items.</summary>
    public static bool BlocksAny(string? liabilityId, IEnumerable<string>? goalItemIds, Func<string, ItemSourceProfile>? profileOf)
    {
        if (!IsHard(liabilityId) || goalItemIds == null || profileOf == null) return false;
        foreach (string id in goalItemIds)
            if (!string.IsNullOrEmpty(id) && Blocks(liabilityId, profileOf(id)))
                return true;
        return false;
    }
}

/// <summary>Per-item source profiles derived from the generation's item pools, so the rule reads
/// the same data the board was built from (mod fish and mod forage included). Conservative in
/// the harmless direction: a false "only source" at worst swaps one drawback for another, while a
/// missed one pairs an impossible goal with the week, so a curated mine forage list tops up what
/// the pools cannot tag.</summary>
public sealed class ItemSourceIndex
{
    /// <summary>Data/Locations keys that are inside the mines.</summary>
    public static readonly IReadOnlySet<string> MineLocationTags =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "UndergroundMine", "Mine" };

    /// <summary>Forage the game spawns from code on mine floors only (no Data/Locations row, so
    /// the forage pool carries no location for it): Cave Carrot.</summary>
    public static readonly IReadOnlySet<string> MineOnlyForageIds =
        new HashSet<string>(StringComparer.Ordinal) { "(O)78" };

    public static readonly ItemSourceIndex Empty = new(Array.Empty<string>(), Array.Empty<string>());

    private readonly HashSet<string> _mineOnly;
    private readonly HashSet<string> _forageOnly;

    public ItemSourceIndex(IEnumerable<string>? mineOnlyIds, IEnumerable<string>? forageOnlyIds)
    {
        _mineOnly = new HashSet<string>((mineOnlyIds ?? Array.Empty<string>()).Select(BundleParsing.NormalizeItemId), StringComparer.Ordinal);
        _forageOnly = new HashSet<string>((forageOnlyIds ?? Array.Empty<string>()).Select(BundleParsing.NormalizeItemId), StringComparer.Ordinal);
    }

    public ItemSourceProfile For(string? itemId)
    {
        if (string.IsNullOrEmpty(itemId)) return ItemSourceProfile.Open;
        string id = BundleParsing.NormalizeItemId(itemId);
        bool mine = _mineOnly.Contains(id), forage = _forageOnly.Contains(id);
        return mine || forage ? new ItemSourceProfile(mine, forage) : ItemSourceProfile.Open;
    }

    /// <summary>Mine-only: a fish whose every spawn row is a mine floor (plus the hard-coded mine
    /// fish), a monster drop, or curated mine forage, unless a shop sells it or another pool
    /// (crops, crab pot, artisan, cooking, saplings, tapper) reaches it. Forage-only: a forage
    /// pool item no other pool and no shop reaches.</summary>
    public static ItemSourceIndex From(ItemPools? pools)
    {
        if (pools == null) return Empty;

        static IEnumerable<string> Ids(IReadOnlyList<PoolItem>? pool)
            => (pool ?? Array.Empty<PoolItem>()).Select(p => BundleParsing.NormalizeItemId(p.ItemId));

        bool ShopSells(string id) => pools.ShopWeeks != null && pools.ShopWeeks.TryGet(id, out _);

        static bool AllMine(IReadOnlyList<string>? locations)
            => locations != null && locations.Count > 0 && locations.All(l => MineLocationTags.Contains(l ?? ""));

        var grownOrMade = new HashSet<string>(
            Ids(pools.Crops).Concat(Ids(pools.CrabPot)).Concat(Ids(pools.ArtisanGoods)).Concat(Ids(pools.Cooking))
                .Concat(Ids(pools.Saplings)).Concat(Ids(pools.TapperGoods)),
            StringComparer.Ordinal);

        IEnumerable<PoolItem> allFish = (pools.Fish ?? Array.Empty<PoolItem>()).Concat(pools.UnpooledFish ?? Array.Empty<PoolItem>());
        var mineCandidates = new HashSet<string>(StringComparer.Ordinal);
        foreach (PoolItem fish in allFish)
            if (AllMine(fish.Locations)) mineCandidates.Add(BundleParsing.NormalizeItemId(fish.ItemId));
        foreach (PoolAddition addition in PoolAdditions.Fish)
            if (AllMine(addition.Locations)) mineCandidates.Add(BundleParsing.NormalizeItemId(addition.ItemId));
        foreach (string id in AvailabilityWeeks.MineFishWeeks.Keys) mineCandidates.Add(id);
        foreach (string id in Ids(pools.MonsterDrops)) mineCandidates.Add(id);
        foreach (string id in MineOnlyForageIds) mineCandidates.Add(id);
        // A fish with a mine row AND an open-water row is not mine-only, whatever the curated lists say.
        var openWaterFish = new HashSet<string>(
            allFish.Where(f => f.Locations != null && f.Locations.Count > 0 && !AllMine(f.Locations))
                .Select(f => BundleParsing.NormalizeItemId(f.ItemId)),
            StringComparer.Ordinal);
        var mineOnly = mineCandidates.Where(id => !ShopSells(id) && !grownOrMade.Contains(id) && !openWaterFish.Contains(id));

        var otherRoutes = new HashSet<string>(
            grownOrMade.Concat(Ids(pools.Fish)).Concat(Ids(pools.Metals)).Concat(Ids(pools.MonsterDrops))
                .Concat(Ids(pools.GeodeMinerals)).Concat(Ids(pools.Artifacts)).Concat(Ids(pools.Books)),
            StringComparer.Ordinal);
        var forageOnly = Ids(pools.Forage).Where(id => !ShopSells(id) && !otherRoutes.Contains(id));

        return new ItemSourceIndex(mineOnly, forageOnly);
    }
}
