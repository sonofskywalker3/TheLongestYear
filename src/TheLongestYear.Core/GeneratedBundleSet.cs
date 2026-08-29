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

    /// <summary>Classify every generated bundle into a requirement manifest.</summary>
    /// <param name="availability">Derived item model, forwarded to the classifier so PerItem
    /// bundles get a computed deadline per ingredient. Null keeps the legacy pin-table path;
    /// see <see cref="BundleClassifier.Classify"/>. The obtainability clamp below still uses
    /// <paramref name="itemSeasonPins"/>, which is a separate question from the due dates.</param>
    public IReadOnlyList<BundleRequirement> BuildRequirements(
        IReadOnlyDictionary<string, Season> itemSeasonPins,
        IReadOnlyDictionary<string, int[]> bundleQuotas,
        SeasonEase? ease = null,
        ItemAvailabilityModel? availability = null)
    {
        // One classification loop for the generator and the stored-board path: the set is
        // reduced to the exact bundle strings the game will see (writer -> parser), so what is
        // classified can never drift from what was written.
        return BoardRequirements.Build(ToBundleData(), itemSeasonPins, bundleQuotas, ease, availability);
    }

    /// <summary>ramp[s] may never exceed the number of ingredient slots obtainable by the
    /// end of season s (un-pinned ingredients count as Spring-obtainable). The Winter value
    /// keeps demanding min(numberOfSlots, obtainable-ever) so completion is still required.
    /// Result stays monotonic non-decreasing.</summary>
    /// <param name="stretchLines">Stretch lines (spec 2026-08-28-obtainable-board-2-stretch): an id
    /// with a stretch line counts as obtainable from its stretch season, exactly as
    /// <see cref="BundleClassifier.RampFromItems"/> reads them. Without this the clamp reads the
    /// pin table alone and flattens the very bump the stretch line was placed to create.</param>
    public static int[] ClampRampForObtainability(
        int[] cumulativeRamp, IReadOnlyList<string> ingredients, int numberOfSlots,
        IReadOnlyDictionary<string, Season> pins,
        IReadOnlyDictionary<string, Season>? stretchLines = null)
    {
        var clamped = new int[cumulativeRamp.Length];
        for (int s = 0; s < cumulativeRamp.Length; s++)
        {
            int obtainable = ingredients.Count(id =>
                !pins.TryGetValue(id, out Season pinned) || (int)pinned <= s
                || (stretchLines != null && stretchLines.TryGetValue(id, out Season stretch) && (int)stretch <= s));
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
