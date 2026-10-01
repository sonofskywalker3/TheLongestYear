using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core;

/// <summary>What happens to one kept farmhouse piece after the rewind.</summary>
public enum HouseFurnitureOutcome
{
    Place,
    DropByDoor,
}

/// <summary>What the reset does so the rebuilt farmhouse always ends with a bed.</summary>
public enum BedFallback
{
    None,
    KeptBedAtStarterSpot,
    StarterBed,
}

/// <summary>
/// Keep Farmhouse Furniture (spec 2026-10-01, Addendum 2): every Furniture piece inside the
/// farmhouse (and its cellar) comes back at the same tile and rotation, dressers included. Chests
/// and other Object-layer containers are not furniture and are never kept. Contents follow the
/// stash nesting rule: cosmetic contents stay, everything else is wiped. The kept set replaces the
/// starter furniture; a piece whose room is gone or that does not fit drops by the front door.
/// The glue is FarmhouseFurnitureCarryover.
/// </summary>
public static class FarmhouseFurnitureKeep
{
    public const string UpgradeId = "keep_farmhouse_furniture";
    public const long Cost = 250;

    private const string FurniturePrefix = "(F)";

    /// <summary>A placed piece that is lifted: furniture, not one of The Longest Year's own items.</summary>
    public static bool IsKeptPiece(string qualifiedItemId)
        => !string.IsNullOrEmpty(qualifiedItemId)
           && qualifiedItemId.StartsWith(FurniturePrefix, StringComparison.Ordinal)
           && StashNesting.IsCosmetic(qualifiedItemId);

    /// <summary>Something inside a kept piece (a dresser slot, a tank, a table top) stays only
    /// when it is cosmetic, the same rule the stash uses.</summary>
    public static bool KeepsContent(string qualifiedItemId) => StashNesting.IsCosmetic(qualifiedItemId);

    public static HouseFurnitureOutcome Decide(bool roomExists, bool fits)
        => roomExists && fits ? HouseFurnitureOutcome.Place : HouseFurnitureOutcome.DropByDoor;

    /// <summary>Indexes in placement order: rugs first (other furniture may stand on a rug, a rug
    /// may not go under furniture already placed), then the rest, each group in its original order.</summary>
    public static IReadOnlyList<int> PlacementOrder(IReadOnlyList<bool> isRug)
    {
        IEnumerable<int> all = Enumerable.Range(0, isRug.Count);
        return all.Where(i => isRug[i]).Concat(all.Where(i => !isRug[i])).ToList();
    }

    /// <summary>The house must have a bed. A kept bed with no room tries the starter bed's spot
    /// before it drops; with no kept bed at all, the starter bed comes back.</summary>
    public static BedFallback ForBed(bool bedPlaced, bool keptBedUnplaced)
    {
        if (bedPlaced)
            return BedFallback.None;
        return keptBedUnplaced ? BedFallback.KeptBedAtStarterSpot : BedFallback.StarterBed;
    }
}
