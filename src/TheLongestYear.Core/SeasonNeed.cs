using System;

namespace TheLongestYear.Core;

/// <summary>How many more lines a bundle may be asked for THIS season by the weekly goals.
///
/// A pick-X-of-Y bundle used to offer every open line as a goal as soon as its ramp rose above
/// zero, so a goal-completing player donated its Winter share in Summer and Fall and reached
/// Winter with a dozen lines on the whole board (sim H, 2026-08-28). The goals now follow the
/// gate exactly: a percentage bundle may be asked for at most what its ramp demands by the end of
/// this season, minus what is already in. Seasonal and per-item bundles keep the plain
/// required-minus-completed cap. Filler (rule B) draws through the same cap, so Winter, whose
/// ramp is X, may ask for everything that is left.</summary>
public static class SeasonNeed
{
    public static int For(BundleRequirement requirement, Season season, int completed)
    {
        if (requirement == null) throw new ArgumentNullException(nameof(requirement));
        int required = requirement.NumberOfSlots > 0 ? requirement.NumberOfSlots : requirement.Ingredients.Count;
        if (requirement.Kind == BundleKind.Percentage && requirement.CumulativeRequiredBySeason != null)
            required = Math.Min(required, requirement.CumulativeRequiredBySeason[(int)season]);
        return Math.Max(0, required - Math.Max(0, completed));
    }
}
