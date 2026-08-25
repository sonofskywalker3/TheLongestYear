using System;
using System.Collections.Generic;

namespace TheLongestYear.Core;

/// <summary>Pierre's year-2 seeds (Garlic, Red Cabbage, Artichoke) have no year-1 source except
/// a Junimo Shrine upgrade, so a board or weekly theme must not ask for them until the player
/// owns one (Jeff, 2026-08-25: a Garlic weekly goal on run 1 is unwinnable by construction).
/// Pure rule; the owned-upgrade set is the same at reset time and on reload because upgrades
/// are only bought at the shrine before a reset, so generation stays deterministic.</summary>
public static class YearTwoCrops
{
    public const string PierreUpgrade = "pierre_year2_seeds";
    public const string RedCabbageUpgrade = "cult_red_cabbage";

    public const string Garlic = "(O)248";
    public const string RedCabbage = "(O)266";
    public const string Artichoke = "(O)274";

    /// <summary>Qualified ids to keep out of every pool for a player with these upgrades.</summary>
    public static IReadOnlySet<string> ExcludedFor(Func<string, bool> hasUpgrade)
    {
        var excluded = new HashSet<string>(StringComparer.Ordinal);
        bool pierre = hasUpgrade(PierreUpgrade);
        if (!pierre)
        {
            excluded.Add(Garlic);
            excluded.Add(Artichoke);
        }
        if (!pierre && !hasUpgrade(RedCabbageUpgrade))
            excluded.Add(RedCabbage);
        return excluded;
    }
}
