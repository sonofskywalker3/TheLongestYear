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
    /// Mixed-week <c>all_drops_up</c> roll chance — the single source of truth for the
    /// generalist "+1 any drop" bonus, referenced by every patch that applies it
    /// (AllDropsPatch, MineDropsPatch fallback, TerrainBonusPatches tree/clump/monster/fish).
    /// 10% is the v1 baseline. (Briefly bumped to 50% on 2026-05-30 to sanity-test the wiring,
    /// then reverted to 10% the same session — the constant stays so the rate can't drift
    /// across the five call sites again.)
    /// </summary>
    public const double MixedAllDropsChance = 0.10;

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
            // 2026-05-29 user spec rebalance: 25%/30% → 20% for both themed roll-ins.
            // all_drops_up (Mixed) uses MixedAllDropsChance (10% baseline).
            "forage_yield_up" => itemQualifiedId != Stone
                                 && itemQualifiedId != Wood
                                 && rng.NextDouble() < 0.20,
            "mine_drops_up"   => itemQualifiedId != Stone
                                 && rng.NextDouble() < 0.20,
            "all_drops_up"    => rng.NextDouble() < MixedAllDropsChance,
            _                 => false
        };
    }
}
