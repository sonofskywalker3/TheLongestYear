using System;
using System.Linq;

namespace TheLongestYear.Core.Obtainability;

/// <summary>Whether Marnie sells an animal. The game lists one only when its PurchasePrice is 0 or
/// more AND its UnlockCondition holds (Utility.getPurchaseAnimalStock, Utility.cs 4705). The
/// answers cannot depend on a save's progress, and a condition on the player's progress names
/// something the build cannot date (a mail flag from a late friendship scene, an event that needs
/// five million gold earned), so an animal behind one is read as not sold: its produce is then an
/// owned-only route, exactly as a hatch-only animal's is. A condition that only asks for a building
/// is no lock: the animal's route already needs its building (Stardew Valley Expanded's Goose and
/// Camel ask for the Premium Coop and Barn they live in). No vanilla animal that is sold carries a
/// condition.</summary>
public static class AnimalSale
{
    /// <summary>The PurchasePrice the game itself uses for "not for sale".</summary>
    public const int NotSold = -1;

    private const string AlwaysTrue = "TRUE";
    private const string BuildingQuery = "BUILDINGS_CONSTRUCTED";
    private const char ClauseSeparator = ',';

    public static bool IsLocked(string? unlockCondition)
        => !string.IsNullOrWhiteSpace(unlockCondition)
            && unlockCondition.Split(ClauseSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Any(clause => !string.Equals(clause, AlwaysTrue, StringComparison.OrdinalIgnoreCase)
                               && !clause.StartsWith(BuildingQuery + " ", StringComparison.Ordinal));

    /// <summary>The price the readers store: the animal's own, or <see cref="NotSold"/> behind a condition.</summary>
    public static int Price(int purchasePrice, string? unlockCondition)
        => IsLocked(unlockCondition) ? NotSold : purchasePrice;
}
