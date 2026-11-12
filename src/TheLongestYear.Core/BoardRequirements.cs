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

    /// <summary>For one bundle, the season before which each flavored ingredient cannot be had:
    /// the season the model (or, without one, the pin table) gives the fruit or fish the slot
    /// names. Null when the bundle names none. A dictionary lookup per flavored slot, so it costs
    /// nothing at load. Two slots of one good take the later season.</summary>
    /// <param name="flavors">Slot to named input, keyed <see cref="FlavoredSlotPass.KeyFor"/>.</param>
    public static IReadOnlyDictionary<string, Season>? NotBefore(
        ParsedBundle parsed, IReadOnlyDictionary<string, string>? flavors,
        ItemAvailabilityModel? availability, IReadOnlyDictionary<string, Season> itemSeasonPins)
    {
        if (flavors == null || flavors.Count == 0) return null;
        Dictionary<string, Season>? result = null;
        for (int i = 0; i < parsed.Ingredients.Count; i++)
        {
            if (FlavoredSlotPass.FlavorAt(flavors, parsed.Index, i) is not string named) continue;
            string input = BundleParsing.NormalizeItemId(named);
            Season? season = availability != null && availability.IsPlaced(input)
                ? availability.For(input).Gate
                : itemSeasonPins.TryGetValue(input, out Season pinned) ? pinned : (Season?)null;
            if (season is not Season earliest) continue;
            string id = BundleParsing.NormalizeItemId(parsed.Ingredients[i].ItemRef);
            result ??= new Dictionary<string, Season>(StringComparer.Ordinal);
            if (!result.TryGetValue(id, out Season have) || earliest > have)
                result[id] = earliest;
        }
        return result;
    }

    /// <summary>The pin table with each <paramref name="notBefore"/> season applied where it is
    /// later than the id's own pin (or the id has none).</summary>
    private static IReadOnlyDictionary<string, Season> WithLaterPins(
        IReadOnlyDictionary<string, Season> pins, IReadOnlyDictionary<string, Season> notBefore)
    {
        var merged = new Dictionary<string, Season>(StringComparer.Ordinal);
        foreach (KeyValuePair<string, Season> pin in pins) merged[pin.Key] = pin.Value;
        foreach (KeyValuePair<string, Season> late in notBefore)
            if (!merged.TryGetValue(late.Key, out Season have) || late.Value > have)
                merged[late.Key] = late.Value;
        return merged;
    }

    public static IReadOnlyList<BundleRequirement> Build(
        IReadOnlyDictionary<string, string> board,
        IReadOnlyDictionary<string, Season> itemSeasonPins,
        IReadOnlyDictionary<string, int[]> bundleQuotas,
        ItemAvailabilityModel? availability = null,
        IReadOnlyDictionary<string, string>? flavors = null)
    {
        var result = new List<BundleRequirement>();
        // Input order is kept: the generator's list order is what the stored dictionary preserves
        // (Newtonsoft keeps insertion order), and downstream goal sampling walks this list.
        foreach (KeyValuePair<string, string> entry in board)
        {
            if (!RoomThemeMap.TryGetTheme(RoomOf(entry.Key), out Theme theme))
                continue; // Vault / non-themed rooms, exactly as the legacy path

            var parsed = BundleParsing.Parse(entry.Key, entry.Value);
            // A flavored slot (Dried Cranberries) is reachable from the season of the fruit or
            // fish it names, which can be later than the good's own season.
            IReadOnlyDictionary<string, Season>? notBefore = NotBefore(parsed, flavors, availability, itemSeasonPins);
            IReadOnlyDictionary<string, Season> pins = notBefore == null ? itemSeasonPins : WithLaterPins(itemSeasonPins, notBefore);
            BundleRequirement? req = BundleClassifier.Classify(
                parsed, theme, itemSeasonPins, bundleQuotas, availability, notBefore);
            if (req == null)
                continue; // category-only bundles (none generated, defensive)

            // CumulativeRequiredBySeason is non-null ONLY for Kind == Percentage (see the
            // factories on BundleRequirement), so this is an exact Percentage filter.
            if (req.CumulativeRequiredBySeason != null)
            {
                int[] clamped = GeneratedBundleSet.ClampRampForObtainability(
                    req.CumulativeRequiredBySeason.ToArray(), req.Ingredients,
                    req.NumberOfSlots, pins, req.StretchLines);
                req = BundleRequirement.CreatePercentage(
                    req.Name, req.Theme, req.Ingredients, req.NumberOfSlots, clamped,
                    req.IngredientStacks, req.IngredientQualities, stretchLines: req.StretchLines,
                    bundleIndex: req.BundleIndex, slots: req.Slots);
            }
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
