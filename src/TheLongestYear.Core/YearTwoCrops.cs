using System;
using System.Collections.Generic;

namespace TheLongestYear.Core;

/// <summary>Pierre's year-2 seeds (Garlic, Red Cabbage, Artichoke), and which of them a year-1
/// run can actually get hold of.
///
/// Only the SHOP listing is year-gated: Data/Shops puts <c>YEAR 2</c> on all three at Pierre and
/// the Night Market. Any route that is not a shop ignores that condition, and the three do not
/// have the same routes (verified against Data/Crops, Data/Shops and the decompiled
/// <c>Crop.getRandomLowGradeCropForThisSeason</c> on 2026-09-10):
///
/// - <b>Garlic (476)</b> has no year-1 route at all. Spring's Mixed Seeds roll is
///   <c>Next(472, 476)</c>, which stops one short of it, and no cart entry lists it.
/// - <b>Red Cabbage (485)</b> is guaranteed from the Traveling Cart once in year 1
///   (<c>VisitsUntilY1Guarantee 0 0</c>) — but this mod caps the cart to the slots a player's
///   cart_slot upgrades have unlocked (one by default, see CartSlotLimitPatch), which turns that
///   guarantee back into luck.
/// - <b>Artichoke (489)</b> comes out of Fall's Mixed Seeds roll, <c>Next(487, 491)</c>, at 25%
///   per seed in any year, free and untouched by this mod. It is therefore NEVER excluded here.
///
/// So the two crops with no free random route each get a Cultivation upgrade that adds one, and
/// Artichoke gets none because it already has the best route of the three. A board or weekly
/// theme must not ask for a crop the player has no way to grow (Jeff, 2026-08-25: a Garlic
/// weekly goal on run 1 is unwinnable by construction).
///
/// Pure rule; the owned-upgrade set is the same at reset time and on reload because upgrades are
/// only bought at the shrine before a reset, so generation stays deterministic.</summary>
public static class YearTwoCrops
{
    public const string PierreUpgrade = "pierre_year2_seeds";
    public const string GarlicUpgrade = "cult_garlic";
    public const string RedCabbageUpgrade = "cult_red_cabbage";

    public const string Garlic = "(O)248";
    public const string RedCabbage = "(O)266";
    public const string Artichoke = "(O)274";

    /// <summary>Qualified ids to keep out of every pool for a player with these upgrades, on this
    /// difficulty step. Only Easy still refuses year-2 crops outright once the pools carry them at
    /// weight 1 (spec 2026-08-28-obtainable-board, section 3); Normal and above ask for them like
    /// any other addition. Artichoke is never excluded — see the class remarks.</summary>
    public static IReadOnlySet<string> ExcludedFor(Func<string, bool> hasUpgrade, DifficultyStep step)
    {
        var excluded = new HashSet<string>(StringComparer.Ordinal);
        if (step != DifficultyStep.Easy)
            return excluded;
        bool pierre = hasUpgrade(PierreUpgrade);
        if (!pierre && !hasUpgrade(GarlicUpgrade))
            excluded.Add(Garlic);
        if (!pierre && !hasUpgrade(RedCabbageUpgrade))
            excluded.Add(RedCabbage);
        return excluded;
    }
}
