using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core;

/// <summary>One-time respec of the xp_mult family for the 2026-08-30 rebalance.
///
/// The ladder used to be x2/x3/x4/x5 per skill for 100/200/350/550 JP, with a capstone that
/// doubled everything again. It is now +25% per tier for 150/350/650/1000, with a capstone that
/// adds 50% (see <see cref="XpMultiplierRules"/> for why). Anyone who had already bought in paid
/// old prices for an effect that no longer exists, so this hands the JP back and takes the tiers
/// with it: the player re-buys under the new ladder, spending the same points they spent before.
///
/// Jeff's ruling, 2026-08-30, on the three options put to him (nerf silently / grandfather the old
/// strength / nerf and refund): refund. Grandfathering would have meant carrying two ladders in
/// MetaState and in the shrine display indefinitely.
///
/// Refunds are at the OLD prices, because that is what was actually paid. Nobody comes out ahead:
/// the same JP buys strictly less than it used to, which is the point of the rebalance.</summary>
public static class XpLadderRespec
{
    /// <summary>What each per-skill tier cost before the rebalance, indexed [1..4].</summary>
    private static readonly long[] OldPerSkillCosts = { 0, 100, 200, 350, 550 };

    /// <summary>The capstone's price is unchanged, but its effect is not (x2 became +50%), and it
    /// requires tier 4 in every skill - all of which this respec takes back - so it is refunded
    /// and removed with the rest of the family.</summary>
    public const long OldCapstoneCost = 3000;

    /// <summary>True when this save still has the respec owing.</summary>
    public static bool IsOwed(MetaState meta)
        => meta != null && !meta.XpLadderRespecDone;

    /// <summary>Refund every xp_mult upgrade this save owns at its old price, remove them, and
    /// mark the respec done. Returns the JP handed back, and 0 when there was nothing to do (a
    /// save that never bought in still gets the flag set, so this never runs twice).</summary>
    public static long Respec(MetaState meta, out int upgradesCleared)
    {
        upgradesCleared = 0;
        if (meta == null || meta.XpLadderRespecDone) return 0;

        meta.XpLadderRespecDone = true;
        meta.OwnedUpgrades ??= new List<string>();

        long refund = 0;
        var owned = meta.OwnedUpgrades.Where(IsXpMultUpgrade).ToList();
        foreach (string id in owned)
        {
            refund += OldCostOf(id);
            meta.OwnedUpgrades.Remove(id);
            upgradesCleared++;
        }

        meta.JunimoPoints += refund;
        return refund;
    }

    /// <summary>True for any id in the xp_mult family, capstone included.</summary>
    public static bool IsXpMultUpgrade(string? id)
        => id != null && id.StartsWith("xp_mult", StringComparison.Ordinal);

    /// <summary>What this upgrade cost under the old ladder. Unknown ids refund nothing rather
    /// than guessing, so a mod-added id in the family cannot mint points.</summary>
    public static long OldCostOf(string? id)
    {
        if (id == null) return 0;
        if (string.Equals(id, XpMultiplierRules.CapstoneId, StringComparison.Ordinal))
            return OldCapstoneCost;

        int underscore = id.LastIndexOf('_');
        if (underscore < 0 || !int.TryParse(id.Substring(underscore + 1), out int tier)) return 0;
        if (tier < 1 || tier >= OldPerSkillCosts.Length) return 0;

        const string prefix = "xp_mult_";
        string slug = id.Substring(0, underscore);
        if (!slug.StartsWith(prefix, StringComparison.Ordinal)) return 0;

        string skill = slug.Substring(prefix.Length);
        for (int which = 0; which <= 4; which++)
            if (string.Equals(XpMultiplierRules.SlugForVanillaSkill(which), skill, StringComparison.Ordinal))
                return OldPerSkillCosts[tier];
        return 0;
    }
}
