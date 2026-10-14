using System.Collections.Generic;

namespace TheLongestYear.Core;

/// <summary>One farm building as the kept-building spot snapshot sees it: its family ("coop",
/// "barn", "silo", "Greenhouse"), its tier inside the family, and its top-left tile.</summary>
public sealed record FamilyBuilding(string Family, int Tier, BuildingSpot Spot);

/// <summary>
/// Picks which building's tile each kept-building family remembers before a rewind
/// (MetaState.KeptBuildingSpots). With several buildings of one family (two coops, two silos) the
/// rewind rebuilds only one, so the spot must be the kept one's, not whichever came last in the
/// farm's list (bug 2026-09-25: a kept Big Coop at (52,20) came back on a second plain Coop's spot
/// at (60,27)).
///
/// Rule per family: the highest tier wins (the kept building is rebuilt at the top tier the player
/// paid for, and is the one they upgraded). Among equal tiers, the one standing on the spot
/// remembered last rewind wins (that is the building the rewind put back). Otherwise the first in
/// the farm's building list wins (the rewind adds the kept building before the player builds
/// anything, so it comes first). A family with no live building keeps its previous entry.
/// </summary>
public static class KeptBuildingSpotPicker
{
    public static Dictionary<string, BuildingSpot> Pick(
        IReadOnlyList<FamilyBuilding> buildingsInFarmOrder,
        IReadOnlyDictionary<string, BuildingSpot> previous)
    {
        var best = new Dictionary<string, FamilyBuilding>();
        foreach (FamilyBuilding b in buildingsInFarmOrder)
        {
            if (string.IsNullOrEmpty(b.Family))
                continue;
            if (!best.TryGetValue(b.Family, out FamilyBuilding? current) || Beats(b, current, previous))
                best[b.Family] = b;
        }

        var result = new Dictionary<string, BuildingSpot>(previous);
        foreach (var (family, b) in best)
            result[family] = b.Spot;
        return result;
    }

    // True when the challenger (later in farm order) should replace the current pick.
    private static bool Beats(FamilyBuilding challenger, FamilyBuilding current,
        IReadOnlyDictionary<string, BuildingSpot> previous)
    {
        if (challenger.Tier != current.Tier)
            return challenger.Tier > current.Tier;
        if (previous.TryGetValue(challenger.Family, out BuildingSpot? remembered))
            return challenger.Spot == remembered && current.Spot != remembered;
        return false;
    }
}
