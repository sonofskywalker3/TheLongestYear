using System;
using System.Linq;

namespace TheLongestYear.Core;

/// <summary>How many more lines a bundle may be asked for by the weekly goals in a season.
///
/// Jeff's rule (2026-08-28): the floor only stops an item showing up too early; nothing forces
/// one to show up. So a reachable item may be a goal whenever the board still needs it, but the
/// goals may not empty the board ahead of the gates either (sim H: a goal-completing player
/// reached Winter with a dozen lines left), nor go quiet once a season's share is in (sim L:
/// weeks 3 and 4 asked for nothing). The bound is half a season ahead: by the end of season s a
/// bundle may be asked for what its gate demands by s plus half of what it demands in s + 1.
/// Winter's demand is the whole bundle, so Winter may ask for everything left.</summary>
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
            var ramp = requirement.CumulativeRequiredBySeason;
            int now = ramp[s];
            int next = s + 1 < ramp.Count ? ramp[s + 1] : required;
            allowed = Math.Min(required, now + HalfUp(next - now));
        }
        else if (requirement.Kind == BundleKind.PerItem && requirement.ItemSeasonPins != null)
        {
            int now = requirement.ItemSeasonPins.Count(p => (int)p.Value <= s);
            int next = requirement.ItemSeasonPins.Count(p => (int)p.Value == s + 1);
            int unpinned = requirement.Ingredients.Count(id => !requirement.ItemSeasonPins.ContainsKey(id));
            allowed = Math.Min(required, now + HalfUp(next) + (s == (int)Season.Winter ? unpinned : 0));
        }
        return Math.Max(0, allowed - Math.Max(0, completed));
    }

    private static int HalfUp(int n) => (Math.Max(0, n) + 1) / 2;
}
