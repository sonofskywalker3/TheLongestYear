using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core;

public enum FocusQuoteKind { Offer, KeepOnly, Done, Locked }

/// <summary>What a focus card offers right now. <c>UpgradeId</c> is the target rung's catalog id
/// (null when the chain is done); <c>Grants</c> is every rung id the purchase adds.</summary>
public sealed record FocusQuote(
    string ChainId, FocusQuoteKind Kind, int TargetRung, string? UpgradeId,
    IReadOnlyList<string> Grants, long Price);

/// <summary>Turns a chain, what the player keeps and what the world already has into a quote.</summary>
public static class FocusMorph
{
    /// <summary>The highest rung of the chain already owned in the meta (0 = none).</summary>
    public static int KeptRung(FocusChain chain, MetaState meta)
    {
        for (int i = chain.Rungs.Count; i >= 1; i--)
            if (meta.HasUpgrade(chain.Rungs[i - 1])) return i;
        return 0;
    }

    public static FocusQuote Quote(FocusChain chain, MetaState meta, int worldRung, double priceFactor)
    {
        int n = chain.Rungs.Count;
        int kept = KeptRung(chain, meta);
        if (kept >= n)
            return new FocusQuote(chain.Id, FocusQuoteKind.Done, n, null, Array.Empty<string>(), 0);

        bool keepOnly = worldRung >= n;
        int target = keepOnly ? n : Math.Max(kept, worldRung) + 1;
        string id = chain.Rungs[target - 1];
        UpgradeDefinition def = UpgradeCatalog.TryGet(id)!;
        long price = UpgradePricing.EffectiveCost(def, priceFactor, meta);

        if (!keepOnly && !meta.MeetsMetaRequirement(def.MetaRequirement))
            return new FocusQuote(chain.Id, FocusQuoteKind.Locked, target, id, Array.Empty<string>(), price);

        List<string> grants = Enumerable.Range(kept + 1, target - kept)
            .Select(r => chain.Rungs[r - 1])
            .Where(g => !meta.HasUpgrade(g))
            .ToList();
        return new FocusQuote(chain.Id, keepOnly ? FocusQuoteKind.KeepOnly : FocusQuoteKind.Offer, target, id, grants, price);
    }
}
