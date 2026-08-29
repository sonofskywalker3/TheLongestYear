using System;
using System.Linq;

namespace TheLongestYear.Core;

/// <summary>How many more lines a bundle may be asked for by the weekly goals in a season.
///
/// Jeff's rule (2026-08-28): goals follow the gate exactly, no look-ahead. A bundle may be
/// asked for only what the current season's gate already demands (Percentage: the cumulative
/// ramp entry for this season; PerItem: the pins due by now, plus every unpinned ingredient once
/// Winter arrives, since Winter's gate is the whole bundle). A player who is ahead of the gate
/// sees quiet cards until the next season opens more of the bundle up; that is by design, not a
/// bug (the earlier half-season look-ahead is retired).</summary>
public static class SeasonNeed
{
    public static int For(BundleRequirement requirement, Season season, int completed)
    {
        if (requirement == null) throw new ArgumentNullException(nameof(requirement));
        int required = requirement.NumberOfSlots > 0 ? requirement.NumberOfSlots : requirement.Ingredients.Count;
        int s = (int)season;
        int allowed = required;
        if (requirement.Kind == BundleKind.Percentage && requirement.CumulativeRequiredBySeason != null)
        {
            allowed = Math.Min(required, requirement.CumulativeRequiredBySeason[s]);
        }
        else if (requirement.Kind == BundleKind.PerItem && requirement.ItemSeasonPins != null)
        {
            int due = requirement.ItemSeasonPins.Count(p => (int)p.Value <= s);
            int unpinned = requirement.Ingredients.Count(id => !requirement.ItemSeasonPins.ContainsKey(id));
            allowed = Math.Min(required, due + (s == (int)Season.Winter ? unpinned : 0));
        }
        return Math.Max(0, allowed - Math.Max(0, completed));
    }
}
