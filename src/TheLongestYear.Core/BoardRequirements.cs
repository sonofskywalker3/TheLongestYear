using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core;

/// <summary>
/// Builds the run's <see cref="BundleRequirement"/> list from a board expressed as vanilla bundle
/// data (<c>"Room/Index" -> value</c>), the same strings the engine writes into
/// <c>netWorldState.BundleData</c>. This is the single classification loop behind both
/// <see cref="GeneratedBundleSet.BuildRequirements"/> (fresh from the generator) and the stored
/// board path (<see cref="MetaState.WrittenBoard"/>): the mod persists exactly what it wrote and
/// rebuilds requirements from that on every later load, so a data mod whose Content Patcher
/// edits shift the item pools after the reset (SVE audit, TODO 2026-08-29) can no longer make a
/// seed-based re-derivation disagree with the live board and demote the save to the legacy path.
/// </summary>
public static class BoardRequirements
{
    /// <summary>The room name from a bundle-data key (<c>"Fish Tank/9"</c> -> <c>"Fish Tank"</c>).</summary>
    public static string RoomOf(string key)
    {
        int slash = key.IndexOf('/');
        return slash < 0 ? key : key.Substring(0, slash);
    }

    public static IReadOnlyList<BundleRequirement> Build(
        IReadOnlyDictionary<string, string> board,
        IReadOnlyDictionary<string, Season> itemSeasonPins,
        IReadOnlyDictionary<string, int[]> bundleQuotas,
        SeasonEase? ease = null,
        ItemAvailabilityModel? availability = null)
    {
        var result = new List<BundleRequirement>();
        // Input order is kept: the generator's list order is what the stored dictionary preserves
        // (Newtonsoft keeps insertion order), and downstream goal sampling walks this list.
        foreach (KeyValuePair<string, string> entry in board)
        {
            if (!RoomThemeMap.TryGetTheme(RoomOf(entry.Key), out Theme theme))
                continue; // Vault / non-themed rooms, exactly as the legacy path

            var parsed = BundleParsing.Parse(entry.Key, entry.Value);
            BundleRequirement? req = BundleClassifier.Classify(
                parsed, theme, itemSeasonPins, bundleQuotas, availability);
            if (req == null)
                continue; // category-only bundles (none generated, defensive)

            // CumulativeRequiredBySeason is non-null ONLY for Kind == Percentage (see the
            // factories on BundleRequirement), so this is an exact Percentage filter.
            if (req.CumulativeRequiredBySeason != null)
            {
                int[] clamped = GeneratedBundleSet.ClampRampForObtainability(
                    req.CumulativeRequiredBySeason.ToArray(), req.Ingredients,
                    req.NumberOfSlots, itemSeasonPins, req.StretchLines);
                req = BundleRequirement.CreatePercentage(
                    req.Name, req.Theme, req.Ingredients, req.NumberOfSlots, clamped,
                    req.IngredientStacks, req.IngredientQualities, stretchLines: req.StretchLines,
                    bundleIndex: req.BundleIndex, slots: req.Slots);
            }
            if (ease != null)
                req = SeasonEase.Apply(req, ease);   // season pity, keep path (spec 2026-08-25)
            result.Add(req);
        }
        return result;
    }

    /// <summary>Season pins as persisted (<see cref="Season"/> stored as its int value).</summary>
    public static Dictionary<string, Season> PinsFromStored(IReadOnlyDictionary<string, int>? stored)
    {
        var pins = new Dictionary<string, Season>(StringComparer.Ordinal);
        if (stored == null) return pins;
        foreach (KeyValuePair<string, int> pair in stored)
            pins[pair.Key] = (Season)pair.Value;
        return pins;
    }

    public static Dictionary<string, int> PinsToStored(IReadOnlyDictionary<string, Season> pins)
        => pins.ToDictionary(p => p.Key, p => (int)p.Value, StringComparer.Ordinal);
}
