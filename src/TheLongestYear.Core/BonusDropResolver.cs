using System;

namespace TheLongestYear.Core;

/// <summary>
/// Pure probability resolver: given the active bonus id + an item qualified id, decides
/// whether to grant an extra +1 drop. All thresholds from the v1 spec (2026-05-27).
/// Stone = "(O)390", Wood = "(O)388" — excluded from forage_yield_up and mine_drops_up
/// to avoid overloading multi-drop resource nodes.
/// </summary>
public static class BonusDropResolver
{
    private const string Stone = "(O)390";
    private const string Wood  = "(O)388";

    /// <summary>
    /// Roll for an extra drop. Returns true when the extra drop should fire.
    /// <paramref name="rng"/> is the caller's current Game1.random (or test-injected random).
    /// Returns false when <paramref name="bonusId"/> is null or unrecognised.
    /// </summary>
    public static bool ShouldGrantExtraDrop(string bonusId, string itemQualifiedId, Random rng)
    {
        if (bonusId == null) return false;
        return bonusId switch
        {
            "forage_yield_up" => itemQualifiedId != Stone
                                 && itemQualifiedId != Wood
                                 && rng.NextDouble() < 0.25,
            "mine_drops_up"   => itemQualifiedId != Stone
                                 && rng.NextDouble() < 0.30,
            "all_drops_up"    => rng.NextDouble() < 0.10,
            _                 => false
        };
    }
}
