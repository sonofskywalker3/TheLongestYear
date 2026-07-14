using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core;

/// <summary>A complete engine-generated bundle set for one loop, plus its requirement
/// MANIFEST. The manifest is built by round-tripping each spec through the same
/// writer→parser pair the game will see and classifying with the existing
/// BundleClassifier — so classification can never drift from the written data, and
/// (because the engine authored every bundle) nothing themed is ever skipped. The
/// season-gate clamp then guarantees no season's cumulative quota demands more slots
/// than are obtainable by that season's end (spec 2026-07-14, user-ruled safety rule).</summary>
public sealed class GeneratedBundleSet
{
    public IReadOnlyList<BundleSpec> Bundles { get; }

    public GeneratedBundleSet(IReadOnlyList<BundleSpec> bundles) => Bundles = bundles;

    public IReadOnlyDictionary<string, string> ToBundleData() =>
        Bundles.ToDictionary(BundleDataWriter.Key, BundleDataWriter.Value);

    public IReadOnlyList<BundleRequirement> BuildRequirements(
        IReadOnlyDictionary<string, Season> itemSeasonPins,
        IReadOnlyDictionary<string, int[]> bundleQuotas)
    {
        var result = new List<BundleRequirement>();
        foreach (var spec in Bundles)
        {
            if (!RoomThemeMap.TryGetTheme(spec.Room, out Theme theme))
                continue; // Vault / non-themed rooms, exactly as the legacy path

            var parsed = BundleParsing.Parse(BundleDataWriter.Key(spec), BundleDataWriter.Value(spec));
            BundleRequirement? req = BundleClassifier.Classify(parsed, theme, itemSeasonPins, bundleQuotas);
            if (req == null)
                continue; // category-only bundles (none generated in Plan 1, defensive)

            // CumulativeRequiredBySeason is non-null ONLY for Kind == Percentage: the private
            // BundleRequirement constructor is only reachable through its three factories, and
            // CreateSeasonal/CreatePerItem always pass null for this field — only
            // CreatePercentage populates it. So this guard is a structurally exact Percentage
            // filter, not a convention; Seasonal/PerItem requirements pass through untouched.
            if (req.CumulativeRequiredBySeason != null)
            {
                int[] clamped = ClampRampForObtainability(
                    req.CumulativeRequiredBySeason.ToArray(), req.Ingredients,
                    req.NumberOfSlots, itemSeasonPins);
                req = BundleRequirement.CreatePercentage(
                    req.Name, req.Theme, req.Ingredients, req.NumberOfSlots, clamped,
                    req.IngredientStacks, req.IngredientQualities);
            }
            result.Add(req);
        }
        return result;
    }

    /// <summary>ramp[s] may never exceed the number of ingredient slots obtainable by the
    /// end of season s (un-pinned ingredients count as Spring-obtainable). The Winter value
    /// keeps demanding min(numberOfSlots, obtainable-ever) so completion is still required.
    /// Result stays monotonic non-decreasing.</summary>
    public static int[] ClampRampForObtainability(
        int[] cumulativeRamp, IReadOnlyList<string> ingredients, int numberOfSlots,
        IReadOnlyDictionary<string, Season> pins)
    {
        var clamped = new int[cumulativeRamp.Length];
        for (int s = 0; s < cumulativeRamp.Length; s++)
        {
            int obtainable = ingredients.Count(id =>
                !pins.TryGetValue(id, out Season pinned) || (int)pinned <= s);
            clamped[s] = Math.Min(cumulativeRamp[s], obtainable);
        }
        int last = clamped.Length - 1;
        int obtainableEver = ingredients.Count; // by Winter every pin has passed
        clamped[last] = Math.Max(clamped[last], Math.Min(numberOfSlots, obtainableEver));
        for (int s = 1; s < clamped.Length; s++)
            clamped[s] = Math.Max(clamped[s], clamped[s - 1]);
        return clamped;
    }
}
