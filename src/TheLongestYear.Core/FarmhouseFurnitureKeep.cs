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

    /// <summary>The house must end with a bed the player can sleep in. With no player bed
    /// (FarmHouse.GetPlayerBed: a single bed in the cabin, a double bed from level 1), a legal
    /// kept adult bed with no room tries the starter bed's spot first. Otherwise the starter bed
    /// comes back, unless an adult bed is already in the house (a single bed in a bigger house
    /// still sleeps, and a second bed would be a duplicate).</summary>
    public static BedFallback ForBed(bool playerBedPlaced, bool sleepableBedPlaced, bool legalKeptBedUnplaced)
    {
        if (playerBedPlaced)
            return BedFallback.None;
        if (legalKeptBedUnplaced)
            return BedFallback.KeptBedAtStarterSpot;
        return sleepableBedPlaced ? BedFallback.None : BedFallback.StarterBed;
    }

    private const int DoubleBedMinLevel = 1;
    private const int ChildBedMinLevel = 2;

    /// <summary>Vanilla's bed rule (BedFurniture.placementAction): a double bed needs house
    /// level 1, a child bed level 2. canBePlacedHere does not check it.</summary>
    public static bool IsBedLegal(BedKind kind, int houseLevel) => kind switch
    {
        BedKind.Double => houseLevel >= DoubleBedMinLevel,
        BedKind.Child => houseLevel >= ChildBedMinLevel,
        _ => true,
    };

    /// <summary>A kept bed that may take the starter bed's spot: legal at the new level, never a child bed.</summary>
    public static bool IsStarterSpotCandidate(BedKind kind, int houseLevel)
        => kind != BedKind.Child && IsBedLegal(kind, houseLevel);

    // Where each house level's contents sit relative to the cabin, from
    // FarmHouse.moveObjectsForHouseUpgrade: L0 to L1 (6,0), L0 to L2/L3 (24,19).
    private static readonly (int X, int Y) CabinOffset = (0, 0);
    private static readonly (int X, int Y) KitchenOffset = (6, 0);
    private static readonly (int X, int Y) BigHouseOffset = (24, 19);
    private const int KitchenLevel = 1;

    private static (int X, int Y) OffsetFor(int level)
        => level <= 0 ? CabinOffset : level == KitchenLevel ? KitchenOffset : BigHouseOffset;

    /// <summary>How far a kept tile moves when the house goes from one level to another, the way
    /// vanilla shifts furniture on a house upgrade (and back on a downgrade).</summary>
    public static (int X, int Y) TileShift(int fromLevel, int toLevel)
    {
        (int X, int Y) from = OffsetFor(fromLevel), to = OffsetFor(toLevel);
        return (to.X - from.X, to.Y - from.Y);
    }
}

/// <summary>Vanilla BedFurniture.BedType without Any (the glue maps it by name).</summary>
public enum BedKind
{
    Single,
    Double,
    Child,
}
