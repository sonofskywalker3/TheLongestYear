using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core;

/// <summary>Deals the three focus hands for a run from its seed, so a reload deals the same cards.</summary>
public static class FocusDeal
{
    private const int FocusSalt = 0x2F0C5A17;
    private const int ThemePrime = 7919;
    private const int HandSize = 3;

    /// <summary>Whether a chain quoted this way can be dealt: only an offer. A keep-only card (the
    /// world already has its rung) would do nothing until the fail night, so it is never dealt; a
    /// dealt card that turns keep-only later in the loop stays in its hand.</summary>
    public static bool IsDealable(FocusQuote quote) => quote.Kind == FocusQuoteKind.Offer;

    public static List<FocusHand> Deal(int runSeed, Func<FocusChain, bool> feasible)
    {
        var hands = new List<FocusHand>();
        foreach (FocusTheme theme in Enum.GetValues(typeof(FocusTheme)))
        {
            var rng = new Random(unchecked(runSeed ^ FocusSalt ^ (((int)theme + 1) * ThemePrime)));
            List<FocusChain> pool = FocusChains.ForTheme(theme).Where(feasible).ToList();
            int take = Math.Min(HandSize, pool.Count);
            for (int i = 0; i < take; i++)
            {
                int j = rng.Next(i, pool.Count);
                (pool[i], pool[j]) = (pool[j], pool[i]);
            }
            var hand = new FocusHand { Theme = theme };
            hand.Chains.AddRange(pool.Take(take).Select(c => c.Id));
            hands.Add(hand);
        }
        return hands;
    }
}
