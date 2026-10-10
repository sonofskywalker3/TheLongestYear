using System.Collections.Generic;

namespace TheLongestYear.Core;

/// <summary>Building type to (family, tier) for the kept-building chains. A Coop must never satisfy a
/// Cow and vice versa: the family segregates the chains, and tier comparison only happens within a
/// family. Silo is a one-tier family so the kept-building spot snapshot can key it (animals never
/// ask for it). Moved from WorldResetService.ChainInfo so the Herd Book placement can use it.</summary>
public static class AnimalHousing
{
    public const string CoopFamily = "coop";
    public const string BarnFamily = "barn";
    public const string SiloFamily = "silo";

    public static (string Family, int Tier) Chain(string? blueprint) => blueprint switch
    {
        "Coop" => (CoopFamily, 1),
        "Big Coop" => (CoopFamily, 2),
        "Deluxe Coop" => (CoopFamily, 3),
        "Barn" => (BarnFamily, 1),
        "Big Barn" => (BarnFamily, 2),
        "Deluxe Barn" => (BarnFamily, 3),
        "Silo" => (SiloFamily, 1),
        _ => ("", 0),
    };

    /// <summary>Which building on the fresh farm a kept <paramref name="blueprint"/> takes over:
    /// the same type (reuse it, move it to the player's spot), else a LOWER tier of the same chain
    /// (a farm type's starter building, e.g. Meadowlands' Coop at (54,9) under a kept Big Coop:
    /// replace it rather than build on top of it), else none. Returns the index into
    /// <paramref name="freshTypes"/>, -1 for none. A lower tier picks the highest such.</summary>
    public static (KeptBuildingMatch Match, int Index) FindOnFreshFarm(string blueprint, IReadOnlyList<string> freshTypes)
    {
        for (int i = 0; i < freshTypes.Count; i++)
            if (freshTypes[i] == blueprint)
                return (KeptBuildingMatch.Exact, i);
        var (family, tier) = Chain(blueprint);
        if (family.Length == 0)
            return (KeptBuildingMatch.None, -1);
        int best = -1, bestTier = 0;
        for (int i = 0; i < freshTypes.Count; i++)
        {
            var (f, t) = Chain(freshTypes[i]);
            if (f == family && t < tier && t > bestTier)
                (best, bestTier) = (i, t);
        }
        return best < 0 ? (KeptBuildingMatch.None, -1) : (KeptBuildingMatch.LowerTier, best);
    }
}

/// <summary>Result kind of <see cref="AnimalHousing.FindOnFreshFarm"/>.</summary>
public enum KeptBuildingMatch
{
    None,
    Exact,
    LowerTier,
}
