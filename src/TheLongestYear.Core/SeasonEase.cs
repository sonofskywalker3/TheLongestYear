using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core;

/// <summary>Keep-path season pity (spec 2026-08-25 section 2): the season the player keeps
/// failing, its ease steps, and the quota factor those steps give. Applied to a requirement
/// manifest AFTER the obtainability clamp. Only <see cref="Season"/> is eased; Winter never is.</summary>
public sealed record SeasonEase(Season Season, int Steps, double Factor)
{
    public static BundleRequirement Apply(BundleRequirement req, SeasonEase ease)
    {
        if (ease.Steps <= 0 || ease.Season == Season.Winter)
            return req;

        switch (req.Kind)
        {
            case BundleKind.Percentage:
            {
                int s = (int)ease.Season;
                int[] ramp = req.CumulativeRequiredBySeason!.ToArray();
                if (ramp[s] > 0)
                    ramp[s] = Math.Max(1, (int)Math.Ceiling(ramp[s] * ease.Factor));
                for (int i = 1; i < ramp.Length; i++)
                    ramp[i] = Math.Max(ramp[i], ramp[i - 1]);
                if (ramp.SequenceEqual(req.CumulativeRequiredBySeason!))
                    return req;
                return BundleRequirement.CreatePercentage(
                    req.Name, req.Theme, req.Ingredients, req.NumberOfSlots, ramp,
                    req.IngredientStacks, req.IngredientQualities, stretchLines: req.StretchLines,
                    bundleIndex: req.BundleIndex, slots: req.Slots);
            }

            case BundleKind.PerItem:
            {
                bool changed = false;
                var pins = new Dictionary<string, Season>(req.ItemSeasonPins!, StringComparer.Ordinal);
                foreach (KeyValuePair<string, Season> kv in req.ItemSeasonPins!)
                {
                    if (kv.Value != ease.Season) continue;
                    pins[kv.Key] = Slide(kv.Value, ease.Steps);
                    changed = true;
                }
                if (!changed) return req;
                return BundleRequirement.CreatePerItem(
                    req.Name, req.Theme, req.Ingredients, pins, req.IngredientStacks, req.IngredientQualities,
                    stretchLines: req.StretchLines, bundleIndex: req.BundleIndex, slots: req.Slots);
            }

            case BundleKind.Seasonal:
                if (req.SeasonalSeason != ease.Season) return req;
                return BundleRequirement.CreateSeasonal(
                    req.Name, req.Theme, req.Ingredients, Slide(req.SeasonalSeason!.Value, ease.Steps),
                    req.IngredientStacks, req.IngredientQualities, stretchLines: req.StretchLines,
                    bundleIndex: req.BundleIndex, slots: req.Slots);

            default:
                return req;
        }
    }

    private static Season Slide(Season from, int steps)
        => (Season)Math.Min((int)from + steps, (int)Season.Winter);
}
